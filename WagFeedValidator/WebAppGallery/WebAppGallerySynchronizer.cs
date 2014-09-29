//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using WAGFeedValidator.WebAppGallery;

namespace Microsoft.WindowsAzure.Management.Marketplace.Rest.WebAppGallery
{
    using Microsoft.Web.PlatformInstaller;
    using Microsoft.WindowsAzure.Management.Marketplace.Common.WebAppGallery;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;
    using WAGFeedValidator;

    public class WebAppGallerySynchronizer
    {
        private class ETagDescription
        {
            public string ETag { get; set; }

            public string LastModified { get; set; }

            public void Set(string etag, string lastModified)
            {
                this.ETag = etag;
                this.LastModified = lastModified;
            }

            public void Reset()
            {
                this.ETag = null;
                this.LastModified = null;
            }
        }

        private static readonly string AtomNamespace = "http://www.w3.org/2005/Atom";

        public Uri FeedUrl { get; set; }

        public Uri WebPIOfficialFeedUri { get; set; }

        public string CatalogConnectionString { get; set; }

        private object errorsSync = new object();
        public Collection<Error> Errors { get; private set; }

        public BlockingCollection<string> Applications { get; private set; }
        public BlockingCollection<string> Keywords { get; private set; }

        private ETagDescription OfficialFeedETag { get; set; }

        private ETagDescription WebApplicationFeedETag { get; set; }

        private string WebPICacheDirectoryPath { get; set; }

        private SatelliteResourceManager SatelliteResourceManager { get; set; }

        private object keywordsLock = new object();
        private object consoleLock = new object();

        public WebAppGallerySynchronizer()
        {
            this.Errors = new Collection<Error>();
            this.Applications = new BlockingCollection<string>();
            this.Keywords = new BlockingCollection<string>();

            this.WebPICacheDirectoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            this.OfficialFeedETag = new ETagDescription();
            this.WebApplicationFeedETag = new ETagDescription();

            SetupWebPITraceListeners();
        }

        public void Synchronize()
        {
            try
            {
                // Get the list of Azure Ready applications
                ProductManager productManager = this.GetProductManagerForFeed(this.FeedUrl);
                List<Product> azureReadyWebApplications = DonwloadAzureReadyApplicationsFromFeed(productManager);
                this.SynchronizeCatalog(productManager, azureReadyWebApplications);
            }
            catch (Exception ex)
            {
                Errors.Add(new Error(ex.Message, ex));
            }
        }

        private void SynchronizeCatalog(ProductManager productManager, List<Product> products)
        {
            if ((products != null) && (products.Count > 0))
            {
                // List of products we have seen in the feed
                List<string> productIdsInFeed = new List<string>();
                List<Task> importPackageTasks = new List<Task>();

                // TODO: Eventually support applications which do not install in en
                Language english = productManager.GetLanguage("en");
                // Get the list of all eligible products
                List<Product> allEligibleProducts = GetEligibleProducts(products, english);

                // Acquire localization related data
                HashSet<string> visibleKeywordIds = new HashSet<string>(GetVisibleKeywordIds(productManager));

                Dictionary<string, Dictionary<string, string>> allLocalizedKeywords = null;
                Dictionary<string, Dictionary<int, Dictionary<string, string>>> allTitleAndDescriptionsForAllAvailableLanguages = null;

                try 
                {
                    allLocalizedKeywords = SatelliteResourceManager.GetLocalizedKeywords(visibleKeywordIds.ToList());
                    allTitleAndDescriptionsForAllAvailableLanguages = this.GetTitleAndDescriptionsForProductIds(allEligibleProducts);

                    ValidateAllKeywordsAreLocalized(allLocalizedKeywords);
                }
                catch (Exception ex)
                {
                    AddError(new Error(String.Format(CultureInfo.CurrentUICulture, "LOCALIZATION - BLOCKING ERROR: {0}", ex.Message), ex));
                    throw;
                }

                int englishLCID = 9;
                foreach (Product product in allEligibleProducts)
                {
                    Installer installer = product.GetInstaller(english);
                    if ((installer == null) && (product.Installers.Count > 0))
                    {
                        installer = product.Installers[0];
                    }

                    // Only look at web applications which can be installed
                    if ((installer != null) && (installer.InstallerFile != null))
                    {
                        // Keep track of what we saw in the feed
                        productIdsInFeed.Add(product.ProductId);

                        // Package the context so the task can easily close over it
                        var context = new { Product = product, Installer = installer };
                        Task importPackage = new Task(
                            (state) =>
                            {
                                try
                                {


                                    var localContext = context;

                                    ValidatePackageInstallerUrl(localContext.Product.ProductId, localContext.Installer);

                                    string parametersJson =
                                        WebDeployPackageParametersTranslator.GetParametersJson(localContext.Product,
                                                                                               localContext.Installer);
                                    HashSet<string> keywordsForApp = GetVisibleKeywords(visibleKeywordIds,
                                                                                        localContext.Product,
                                                                                        allLocalizedKeywords);

                                    // Insert keywords this app will be using
                                    lock (keywordsLock)
                                    {
                                        foreach (string kw in keywordsForApp)
                                        {
                                            if (!this.Keywords.Contains(kw))
                                            {
                                                this.Keywords.Add(kw);
                                            }
                                        }
                                    }

                                    string iconUrl = GetAdequateIconUrl(localContext.Product.IconUrl);

                                    // Get product localized description for all known languages
                                    Dictionary<int, Dictionary<string, string>> localizedNamesAndDescriptions = null;
                                    if (
                                        !allTitleAndDescriptionsForAllAvailableLanguages.TryGetValue(product.ProductId,
                                                                                                     out
                                                                                                         localizedNamesAndDescriptions))
                                    {
                                        localizedNamesAndDescriptions =
                                            new Dictionary<int, Dictionary<string, string>>();
                                    }

                                    // Add "en" to the list of languages. "9" is the LCID for en
                                    if (!localizedNamesAndDescriptions.ContainsKey(englishLCID))
                                    {
                                        localizedNamesAndDescriptions[englishLCID] = new Dictionary<string, string>();
                                        localizedNamesAndDescriptions[englishLCID]["Title"] = product.Title;
                                        localizedNamesAndDescriptions[englishLCID]["Summary"] = product.Summary;
                                        localizedNamesAndDescriptions[englishLCID]["LongSummary"] =
                                            product.LongDescription;
                                    }
                                    else
                                    {
                                        // Something quite bad happened - we already have en in the localized list
                                        throw new InvalidOperationException(
                                            "LOCALIZATION - BLOCKING: Error - Locale 'en' is being presented as Sattelite file. It MUST BE embedded in WebApplicationList.xml instead.");
                                    }

                                    lock (consoleLock)
                                    {
                                        Console.Write(".");
                                    }

                                    this.Applications.Add(localContext.Product.ProductId);
                                }
                                catch (Exception ex)
                                {
                                    string s = "";
                                }
                            },
                            context);

                        importPackageTasks.Add(importPackage);
                        importPackage.Start();
                    }
                }

                try
                {
                    // Wait for all tasks to complete
                    Task.WaitAll(importPackageTasks.ToArray());
                }
                catch (AggregateException aggregateEx)
                {
                    // Log error
                    foreach (Exception ex in aggregateEx.InnerExceptions)
                    {
                        AddError(new Error(ex.Message, ex));
                    }
                }
                finally
                {
                    foreach (Task task in importPackageTasks)
                    {
                        task.Dispose();
                    }
                }
            }
        }

        private void ValidatePackageInstallerUrl(string productId, Installer installer)
        {
            Uri installerUri = installer.InstallerFile.InstallerUrl;
            string hostName = installerUri.Authority.ToLowerInvariant();;
            if ((hostName != "auxmktplceprod.blob.core.windows.net") && (hostName != "webpitest.blob.core.windows.net"))
            {
                Errors.Add(new Error(String.Format(CultureInfo.CurrentUICulture, "Product '{0}' installer url MUST BE staged on BLOB (webpitest or auxmktplceprod). It is currently pointing to '{1}'.", productId, installerUri), null));
            }
        }

        private void ValidateAllKeywordsAreLocalized(Dictionary<string, Dictionary<string, string>> allLocalizedKeywords)
        {
            // Iterate throuhg all languages, all keywords, looking for keywords with an empty translation
            foreach (string lang in allLocalizedKeywords.Keys)
            {
                Dictionary<string, string> keywordsForCurrentLang = allLocalizedKeywords[lang];
                foreach (string keywordId in keywordsForCurrentLang.Keys)
                {
                    if (String.IsNullOrEmpty(keywordsForCurrentLang[keywordId]))
                    {
                        Errors.Add(new Error(String.Format(CultureInfo.CurrentUICulture, "LOCALIZATION WARNING - Keyword '{0}' is not translated in '{1}'. It will show as '{0}' in Portal.", keywordId, lang), null));
                    }
                }
            }
        }

        private Dictionary<string, Dictionary<int, Dictionary<string, string>>> GetTitleAndDescriptionsForProductIds(List<Product> allEligibleProducts)
        {
            List<string> allproductIds = new List<string>();

            // Collect the producId of all products we are interested in
            foreach (Product product in allEligibleProducts)
            {
                allproductIds.Add(product.ProductId);
            }

            // Hunt for all strings - Some may not exist
            return this.SatelliteResourceManager.GetTitleAndDescriptionsForProductIds(allproductIds);
        }

        private static List<Product> GetEligibleProducts(List<Product> allProducts, Language language)
        {
            List<Product> eligibleProducts = new List<Product>();

            // Filter products which have an installer
            foreach (Product product in allProducts)
            {
                Installer installer = product.GetInstaller(language);
                if ((installer == null) && (product.Installers.Count > 0))
                {
                    installer = product.Installers[0];
                }

                // Only look at web applications which can be installed
                if ((installer != null) && (installer.InstallerFile != null))
                {
                    eligibleProducts.Add(product);
                }
            }

            return eligibleProducts;
        }

        private static string GetAdequateIconUrl(Uri uri)
        {
            Uri tentativeUri = uri;

            // Prefer uris over https:// but fall back to http:// if we have to
            if ((tentativeUri != null) && (string.CompareOrdinal(tentativeUri.Scheme, Uri.UriSchemeHttps) != 0))
            {
                // Scheme
                UriBuilder uriBuilder = new UriBuilder(tentativeUri);
                uriBuilder.Scheme = Uri.UriSchemeHttps;

                // Port
                uriBuilder.Port = 443;
                tentativeUri = uriBuilder.Uri;
            }

            return tentativeUri != null ? tentativeUri.ToString() : string.Empty;
        }

        private static List<Product> DonwloadAzureReadyApplicationsFromFeed(ProductManager productManager)
        {
            // We need all applications with the "AzureReady" tag
            IEnumerable<Product> webApplications = productManager.Products.Where<Product>(product => product.IsApplication);
            var list = webApplications.ToList();
            return webApplications.Where<Product>(webApp => IsAzureReady(webApp)).ToList();
        }

        private ProductManager GetProductManagerForFeed(Uri feedUrl)
        {
            // Ensure our cache dir root is created
            if (!Directory.Exists(this.WebPICacheDirectoryPath))
            {
                Directory.CreateDirectory(this.WebPICacheDirectoryPath);
            }

            // Ensure the WEB PI Product Manager cache is created
            string webPIProductManagerCacheRoot = Path.Combine(this.WebPICacheDirectoryPath, "ProductsManagerCache");
            if (!Directory.Exists(webPIProductManagerCacheRoot))
            {
                Directory.CreateDirectory(webPIProductManagerCacheRoot);
            }

            string pathToOfficialFeed = Path.Combine(this.WebPICacheDirectoryPath, "officialFeed.xml");
            string pathToWebApplicationsFeed = Path.Combine(this.WebPICacheDirectoryPath, "webApplication.xml");
            this.DownloadFeedIfNeeded(pathToOfficialFeed, this.WebPIOfficialFeedUri, this.OfficialFeedETag);

            // Web applications feed
            if (this.FeedUrl != null)
            {
                this.DownloadFeedIfNeeded(pathToWebApplicationsFeed, this.FeedUrl, this.WebApplicationFeedETag);
                this.ApplyWebApplicationFeedToOfficialFeed(pathToOfficialFeed, pathToWebApplicationsFeed);
            }

            // Now, attempt to load the feed
            try
            {
                // Load the feed in an XML document, just to check if it is structurally correct.
                // ProductManager actually catches and swallows those wrong XML errors
                try
                {
                    XmlDocument xmlDom = new XmlDocument();
                    xmlDom.Load(pathToWebApplicationsFeed);
                }
                catch (Exception ex)
                {
                    AddError(new Error(String.Format(CultureInfo.CurrentUICulture, "The feed pointed to by '{0}' does not appear to be a valid WebPI feed.", feedUrl.ToString()), ex));
                }
                    
                ProductManager productManager = new ProductManager();
                productManager.Load(new Uri("file://" + pathToOfficialFeed), false, true, false, webPIProductManagerCacheRoot);

                // Load keyword localizer
                SatelliteResourceManager satteliteManager = new SatelliteResourceManager(pathToWebApplicationsFeed, Path.Combine(webPIProductManagerCacheRoot, "Sattelites"));
                try
                {
                    satteliteManager.Initialize();
                }
                catch(Exception ex)
                {
                    AddError(new Error(ex.Message, ex));
                    throw;
                }

                this.SatelliteResourceManager = satteliteManager;


                return productManager;
            }
            catch (Exception e)
            {
                AddError(new Error(String.Format(CultureInfo.CurrentUICulture, "The feed pointed to by '{0}' does not appear to be a valid WebPI feed.", feedUrl.ToString()), e));
                throw;
            }
        }

        private void ApplyWebApplicationFeedToOfficialFeed(string pathToOfficialFeed, string pathToWebApplicationsFeed)
        {
            // Change the Web application enclosure Uri to point to the Uri we were given
            XDocument document = null;
            using (FileStream webPiFeedSFileStream = new FileStream(pathToOfficialFeed, FileMode.Open, FileAccess.Read))
            {
                document = XDocument.Load(webPiFeedSFileStream);
            }

            XElement webAppEnclosureElement = document.Element(XName.Get("feed", AtomNamespace)).Elements(XName.Get("link", AtomNamespace)).Where<XElement>(
                element => element.Attribute("href").Value.EndsWith("webapplicationlist.xml", StringComparison.OrdinalIgnoreCase)).SingleOrDefault();
            if (webAppEnclosureElement != null)
            {
                webAppEnclosureElement.Attribute("href").Value = (new Uri("file://" + pathToWebApplicationsFeed)).ToString();
                document.Save(pathToOfficialFeed);
            }
        }

        private void DownloadFeedIfNeeded(string pathToFile, Uri feedUrl, ETagDescription etag)
        {
            if (feedUrl.Scheme == Uri.UriSchemeFile)
            {
                // Simple file copy
                try
                {
                    if (File.Exists(pathToFile))
                    {
                        File.Delete(pathToFile);
                    }
                }
                catch(Exception)
                {
                    
                }

                string sourceFilePath = feedUrl.ToString().Substring(Uri.UriSchemeFile.Length + 4);
                File.Copy(sourceFilePath, pathToFile);
            }
            else
            {
                // Http with Etag
                HttpWebRequest webRequest = HttpWebRequest.Create(feedUrl) as HttpWebRequest;
                webRequest.Method = "GET";

                // Do we already have a copy ? If so, we may use ETags, if exist
                if (File.Exists(pathToFile))
                {
                    // We only want to download the feed if our is not up to date
                    if (!string.IsNullOrEmpty(etag.ETag))
                    {
                        webRequest.Headers["If-None-Match"] = etag.ETag;
                        webRequest.IfModifiedSince = DateTime.Parse(etag.LastModified).ToUniversalTime();
                    }
                }

                // Get the response
                try
                {
                    using (HttpWebResponse webResponse = webRequest.GetResponse() as HttpWebResponse)
                    {
                        switch (webResponse.StatusCode)
                        {
                            case HttpStatusCode.OK:
                                using (Stream responseStream = webResponse.GetResponseStream())
                                {
                                    using (FileStream fileStream = new FileStream(pathToFile, FileMode.OpenOrCreate, FileAccess.Write))
                                    {
                                        responseStream.CopyTo(fileStream);
                                    }
                                }

                                etag.Set(webResponse.Headers["ETag"], webResponse.Headers["Last-Modified"]);
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    WebException webException = ex as WebException;
                    if ((webException != null) && (((HttpWebResponse)webException.Response).StatusCode == HttpStatusCode.NotModified))
                    {
                        // Not modified  - What we have is up to date
                    }
                    else
                    {
                        // Delete file - Clear Etag
                        if (File.Exists(pathToFile))
                        {
                            File.Delete(pathToFile);
                        }

                        etag.Reset();

                        throw;
                    }
                }
            }
        }

        private static bool IsAzureReady(Product product)
        {
            // Test if there is the AzureReady keyword
            if (product.Keywords.Count<Keyword>(keyword => string.CompareOrdinal(keyword.Text, WellKnownKeywords.AzureReadyKeyword) == 0) > 0)
            {
                return true;
            }

            // Use reflection to locate products
            FieldInfo keywordIdsField = typeof(Product).GetField("_keywordIds", BindingFlags.NonPublic | BindingFlags.Instance);
            List<string> keywordIds = keywordIdsField.GetValue(product) as List<string>;
            return keywordIds.Count<string>(keywordId => string.CompareOrdinal(keywordId, WellKnownKeywords.AzureReadyKeyword) == 0) > 0;
        }

        private static HashSet<string> GetVisibleKeywords(HashSet<string> visibleKeywords, Product product, Dictionary<string, Dictionary<string, string>> allLocalizedKeywords)
        {
            HashSet<string> visible = new HashSet<string>();

            foreach (Keyword keyword in product.Keywords)
            {
                if (visibleKeywords.Contains(keyword.Id))
                {
                    string theKW = String.Format(CultureInfo.CurrentUICulture, "{0} ({1})", allLocalizedKeywords["en"][keyword.Id] , keyword.Id);
                    visible.Add(theKW);
                }
            }

            return visible;
        }

        private static List<string> GetVisibleKeywordIds(ProductManager productManager)
        {
            List<string> hashSet = new List<string>();
            foreach (Keyword keyword in (productManager.GetTab("WebApplications") as KeywordTab).Keywords)
            {
                hashSet.Add(keyword.Id);
            }

            return hashSet;
        }

        private void AddError(Error theError)
        {
            lock (errorsSync)
            {
                Errors.Add(theError);
            }
        }

        private void SetupWebPITraceListeners()
        {
            TraceSource downloadMgrTraceSource = new TraceSource("DownloadManager");
            foreach (TraceListener listener in downloadMgrTraceSource.Listeners)
            {
                InMemoryTraceListener inMemoryTrace = listener as InMemoryTraceListener;
                if (inMemoryTrace != null)
                {
                    inMemoryTrace.Filter = new EventTypeFilter(SourceLevels.Error);
                    inMemoryTrace.WebPITraceEmitted += new InMemoryTraceListener.WebPITrace(theWebPITraceListener_WebPITraceEmitted);
                }
            }
        }

        void theWebPITraceListener_WebPITraceEmitted(object sender, WebPIErrorEventArgs e)
        {
            AddError(new Error(e.Message, null));
        }
    }
}