//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;


namespace Microsoft.WindowsAzure.Management.Marketplace.Common.WebAppGallery
{
    internal class SatelliteResourceManager
    {
        private class SatelliteDescription
        {
            public string LanguageCode { get; set; }

            public string FilePath { get; set; }
        }

        private static readonly string CacheSubDirectoryName = "Languages";

        private static readonly string KeywordPrefix = "Keyword_";

        private static readonly string EntryPrefix = "Entry_";

        private static readonly XNamespace defaultNs = "http://www.w3.org/2005/Atom";

        private string WebApplicationsFeedFilePath { get; set; }

        private string CacheRoot { get; set; }

        private Dictionary<string, string> AllCachedSatteliteFiles { get; set; }

        public SatelliteResourceManager(string webAppFeedFilePath, string cacheRootPath)
        {
            this.WebApplicationsFeedFilePath = webAppFeedFilePath;
            this.CacheRoot = Path.Combine(cacheRootPath, CacheSubDirectoryName);
        }

        public void Initialize()
        {
            ClearResXCache(this.CacheRoot);

            try
            {
                // Locate the uri of all sattelites
                Dictionary<string, Uri> satelliteFiles = GetSatelliteFiles(this.WebApplicationsFeedFilePath);

                // Remember all satellite files for future use
                this.AllCachedSatteliteFiles = this.CacheAllResourceFiles(satelliteFiles, this.CacheRoot);
            }
            catch (Exception e)
            {
                // Upon failure, do not leave downloaded files behind
                ClearResXCache(this.CacheRoot);

                throw;
            }
        }

        public Dictionary<string, Dictionary<string, string>> GetLocalizedKeywords(List<string> visibleKeywordIds)
        {
            return  this.GetLocalizedKeywordsForAllLanguages(this.AllCachedSatteliteFiles, visibleKeywordIds);
        }

        public Dictionary<string, Dictionary<int, Dictionary<string, string>>> GetTitleAndDescriptionsForProductIds(List<string> productIds)
        {
            Dictionary<string, Dictionary<int, Dictionary<string, string>>> allTitleAndDescriptionsForAllAvailableLanguages = new Dictionary<string, Dictionary<int, Dictionary<string, string>>>();

            // Iterate through all languages, looking for the given keys
            foreach (string languageCode in this.AllCachedSatteliteFiles.Keys)
            {
                // Get the LCID
                CultureInfo cultureInfo = new CultureInfo(languageCode);

                // Load all localized keywords we care about
                XDocument xmlDocument;
                using (FileStream fileStream = new FileStream(this.AllCachedSatteliteFiles[languageCode], FileMode.Open, FileAccess.Read))
                {
                    xmlDocument = XDocument.Load(fileStream);
                    List<XElement> allEntriesInLocFile = xmlDocument.Root.Elements("data").Where(x => x.Attribute("name") != null && x.Attribute("name").Value != null && x.Attribute("name").Value.StartsWith(EntryPrefix)).ToList();

                    // Get all keywords we care about for this language
                    foreach (string productId in productIds)
                    {
                        // Initialize product entry, if not yet initialized
                        Dictionary<int, Dictionary<string, string>> localizedProductEntry;
                        if (!allTitleAndDescriptionsForAllAvailableLanguages.TryGetValue(productId, out localizedProductEntry))
                        {
                            localizedProductEntry = new Dictionary<int, Dictionary<string, string>>();
                            allTitleAndDescriptionsForAllAvailableLanguages[productId] = localizedProductEntry;
                        }

                        // Initialize entry for the current language, if not yet created
                        Dictionary<string, string> currentLanguageProductEntry;
                        if (!localizedProductEntry.TryGetValue(cultureInfo.LCID, out currentLanguageProductEntry))
                        {
                            currentLanguageProductEntry = new Dictionary<string, string>();
                            localizedProductEntry[cultureInfo.LCID] = currentLanguageProductEntry;
                        }

                        // Title
                        {
                            string resXId = string.Format(CultureInfo.InvariantCulture, "Entry_{0}_Title", productId);
                            string theValue = GetResourceValueByName(resXId, allEntriesInLocFile);

                            // Extract the value, if found
                            if (!string.IsNullOrEmpty(theValue))
                            {
                                currentLanguageProductEntry["Title"] = theValue;
                            }
                        }

                        // Short Description
                        {
                            string resXId = string.Format(CultureInfo.InvariantCulture, "Entry_{0}_Summary", productId);
                            string theValue = GetResourceValueByName(resXId, allEntriesInLocFile);

                            // Extract the value, if found
                            if (!string.IsNullOrEmpty(theValue))
                            {
                                currentLanguageProductEntry["Summary"] = theValue;
                            }
                        }

                        // Long Description
                        {
                            string resXId = string.Format(CultureInfo.InvariantCulture, "Entry_{0}_LongSummary", productId);
                            string theValue = GetResourceValueByName(resXId, allEntriesInLocFile);

                            // Extract the value, if found
                            if (!string.IsNullOrEmpty(theValue))
                            {
                                currentLanguageProductEntry["LongSummary"] = theValue;
                            }
                        }

                        // Enforce our business logic - If one entry exits, they all must exist
                        if ((currentLanguageProductEntry.Keys.Count != 0) && (currentLanguageProductEntry.Keys.Count != 3))
                        {
                            string expectedKeys = string.Format(CultureInfo.InvariantCulture, "Entry_{0}_Summary, Entry_{0}_LongSummary, Entry_{0}_Title", productId);
                            string foundKeys = string.Empty;
                            foreach (string key in currentLanguageProductEntry.Keys)
                            {
                                string foundKey = string.Format(CultureInfo.InvariantCulture, "Entry_{0}_{1}", productId, key);
                                if (string.IsNullOrEmpty(foundKeys))
                                {
                                    foundKeys = foundKey;
                                }
                                else 
                                {
                                    foundKeys = foundKeys + ", " + foundKey;
                                }
                            }

                            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "LOCALIZATION - BLOCKING ERROR: Missing key(s) for culture {0}. Found keys:{1} - Expected keys:{2}", languageCode, foundKeys, expectedKeys));
                        }
                    }
                }
            }

            // Clear up languages for which we did not find any translation
            List<string> productsToRemove = new List<string>();
            foreach (string productId in allTitleAndDescriptionsForAllAvailableLanguages.Keys)
            {
                Dictionary<int, Dictionary<string, string>> currentProduct = allTitleAndDescriptionsForAllAvailableLanguages[productId];
                List<int> languagesCodeToRemove = new List<int>();

                // If we did not find anything for this language, then remove the entry alltoegether
                foreach (int lcid in currentProduct.Keys)
                {
                    if (currentProduct[lcid].Keys.Count == 0)
                    {
                        languagesCodeToRemove.Add(lcid);
                    }
                }

                // Apply removal
                foreach (int lcid in languagesCodeToRemove)
                {
                    currentProduct.Remove(lcid);
                }

                // If we did not find any localized value, remove the product
                if (allTitleAndDescriptionsForAllAvailableLanguages[productId].Keys.Count == 0)
                {
                    productsToRemove.Add(productId);
                }
            }

            // Remove all products for which we have not found any translation
            foreach (string productToRemove in productsToRemove)
            {
                allTitleAndDescriptionsForAllAvailableLanguages.Remove(productToRemove);
            }

            return allTitleAndDescriptionsForAllAvailableLanguages;
        }

        private static Dictionary<string, Uri> GetSatelliteFiles(string webAppFilePath)
        {
            Dictionary<string, Uri> satellites = new Dictionary<string, Uri>();
            XDocument xmlDocument;

            using (FileStream fileStream = new FileStream(webAppFilePath, FileMode.Open, FileAccess.Read))
            {
                xmlDocument = XDocument.Load(fileStream);

                // Get all languages available
                List<XElement> allXLanguages = xmlDocument.Root.Descendants(defaultNs + "resources").ToList();
                var allLanguages = from resource in allXLanguages
                                       select new
                                       {
                                           xLang = resource.Descendants(defaultNs + "culture").FirstOrDefault(),
                                           xUrl = resource.Descendants(defaultNs + "url").FirstOrDefault()
                                       };
                foreach (var language in allLanguages)
                {
                    if ((language.xLang != null) && (language.xUrl != null))
                    {
                        string lang = language.xLang.Value;
                        string url = language.xUrl.Value;
                        if (!string.IsNullOrEmpty(lang) && !string.IsNullOrEmpty(url))
                        {
                            satellites[lang] = new Uri(url);
                        }
                    }
                }
            }
             
           return satellites;
        }

        private static string GetResourceValueByName(string resXId, List<XElement> allEntriesInLocFile)
        {
            XElement localeElement = allEntriesInLocFile.Where(x => string.CompareOrdinal(x.Attribute("name").Value.Trim(), resXId) == 0).FirstOrDefault();

            // Extract the value, if we found it otherwise ... fallback - Keyword not localized in this language so emit the raw keywordId
            if (localeElement != null)
            {
                return localeElement.Value.Trim();
            }
            else
            {
                return null;
            }
        }

        private Dictionary<string, string> CacheAllResourceFiles(Dictionary<string, Uri> satelliteFiles, string stagingDirectory)
        {
            List<Task<SatelliteDescription>> allDownloadTasks = new List<Task<SatelliteDescription>>();

            // Kick off all tasks
            foreach (string key in satelliteFiles.Keys)
            {
                var context = new { lang = key, url = satelliteFiles[key], destinationFile = Path.Combine(stagingDirectory, key.Trim() + ".xml") };
                Task<SatelliteDescription> downloadResXTask = new Task<SatelliteDescription>(() =>
                {
                    var capturedContext = context;

                    try
                    {
                        HttpWebRequest webRequest = HttpWebRequest.Create(capturedContext.url) as HttpWebRequest;
                        webRequest.Method = "GET";

                        using (HttpWebResponse webResponse = webRequest.GetResponse() as HttpWebResponse)
                        {
                            switch (webResponse.StatusCode)
                            {
                                case HttpStatusCode.OK:
                                    using (Stream responseStream = webResponse.GetResponseStream())
                                    {
                                        using (FileStream fileStream = new FileStream(capturedContext.destinationFile, FileMode.OpenOrCreate, FileAccess.Write))
                                        {
                                            responseStream.CopyTo(fileStream);
                                        }
                                    }

                                    break;

                                default:
                                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentUICulture, "LOCALIZATION - BLOCKING ERROR: Error '{0}' when downloading resource file '{1}' for language code '{2}' - WebApplicationList.xml contains invalid links to resource files which must be fixed.", webResponse.StatusCode, capturedContext.url, capturedContext.lang));
                            }
                        }

                        return new SatelliteDescription() { LanguageCode = capturedContext.lang, FilePath = capturedContext.destinationFile };
                    }
                    catch(Exception ex)
                    {
                        string error = string.Empty;
                        WebException webEx = ex as WebException;
                        if (webEx != null)
                        {
                            error = "'" + webEx.Message + "'";
                        }
                        throw new InvalidOperationException(string.Format(CultureInfo.CurrentUICulture, "LOCALIZATION - BLOCKING ERROR: Error {0} when downloading resource file '{1}' for language code '{2}' - WebApplicationList.xml contains invalid links to resource files which must be fixed.", error, capturedContext.url, capturedContext.lang));
                    }
                });

                allDownloadTasks.Add(downloadResXTask);
                downloadResXTask.Start();
            }

            try
            {
                // Wait for all tasks to complete
                Task.WaitAll(allDownloadTasks.ToArray());

                // Remember all 
                Dictionary<string, string> allSatelliteFiles = new Dictionary<string, string>();
                foreach (Task<SatelliteDescription> task in allDownloadTasks)
                {
                    if ((task.Exception == null) && (task.Result != null))
                    {
                        allSatelliteFiles[task.Result.LanguageCode] = task.Result.FilePath;
                    }
                }

                return allSatelliteFiles;
            }
            catch (AggregateException aggregateEx)
            {
                // Log error
                StringBuilder stringBuilder = new StringBuilder();
                foreach (Exception ex in aggregateEx.InnerExceptions)
                {
                    stringBuilder.AppendLine(ex.Message);
                }

                throw new InvalidOperationException(stringBuilder.ToString());
            }
            finally
            {
                foreach (Task task in allDownloadTasks)
                {
                    task.Dispose();
                }
            }
        }

        private Dictionary<string, Dictionary<string, string>> GetLocalizedKeywordsForAllLanguages(Dictionary<string, string> cachedSatelliteFiles, List<string> keywordIds)
        {
            Dictionary<string, Dictionary<string, string>> allKeywordsAllLanguages = new Dictionary<string, Dictionary<string, string>>();

            // Iterate through all languages, looking for the given keys
            foreach (string languageCode in cachedSatelliteFiles.Keys)
            {
                // Initialize the top level dictionary for the current language
                allKeywordsAllLanguages[languageCode] = new Dictionary<string, string>();

                // Load all localized keywords we care about
                XDocument xmlDocument;
                using (FileStream fileStream = new FileStream(cachedSatelliteFiles[languageCode], FileMode.Open, FileAccess.Read))
                {
                    xmlDocument = XDocument.Load(fileStream);
                    List<XElement> allKeywordsInLocFile = xmlDocument.Root.Elements("data").Where(x => x.Attribute("name") != null && x.Attribute("name").Value != null && x.Attribute("name").Value.Contains(KeywordPrefix)).ToList();

                    // Get all keywords we care about for this language
                    foreach (string keywordId in keywordIds)
                    {
                        string resXId = string.Concat(KeywordPrefix, keywordId);
                        XElement localeElement = allKeywordsInLocFile.Where(x => string.CompareOrdinal(x.Attribute("name").Value.Trim(), resXId) == 0).FirstOrDefault();
                        
                        // Extract the value, if we found it otherwise ... fallback - Keyword not localized in this language so emit empty
                        allKeywordsAllLanguages[languageCode][keywordId] = localeElement != null ? localeElement.Value.Trim() : string.Empty;
                    }
                }
            }
                
            // Add en-us only if we do not yet have it - EN is marked as <!-- included in file --> in WebPI feed
            if (!allKeywordsAllLanguages.ContainsKey("en"))
            {
                allKeywordsAllLanguages.Add("en", this.GetEnUsKeywords(this.WebApplicationsFeedFilePath, keywordIds));
            }

            return allKeywordsAllLanguages;
        }

        private Dictionary<string, string> GetEnUsKeywords(string webAppFeedPath, List<string> visibleKeywordsId)
        {
            XDocument xmlDocument;
            Dictionary<string, string> allEnUsKeywords = new Dictionary<string, string>();

            using (FileStream fileStream = new FileStream(webAppFeedPath, FileMode.Open, FileAccess.Read))
            {
                xmlDocument = XDocument.Load(fileStream);

                // Get all keywords known in the XML
                List<XElement> allEnUsKeywordsInFeed = xmlDocument.Root.Descendants(defaultNs + "keywords").Descendants(defaultNs + "keyword").Where(x => x.Attribute("resourceName") != null).ToList();
                foreach (string keywordId in visibleKeywordsId)
                {
                    string resXId = string.Concat(KeywordPrefix, keywordId);
                    XElement localeElement = allEnUsKeywordsInFeed.Where(x => string.CompareOrdinal(x.Attribute("resourceName").Value.Trim(), resXId) == 0).FirstOrDefault();

                    // Fall back to the keyword id if we could not find a localized version
                    allEnUsKeywords.Add(keywordId, localeElement != null ? localeElement.Value.Trim() : string.Empty);
                }
            }

            return allEnUsKeywords;
        }

        private string SerializeForOneKeyword(string theKeyword, Dictionary<string, Dictionary<string, string>> allLanguages)
        {
            StringBuilder stringBuidler = new StringBuilder(theKeyword + ":");
            bool isFirst = true;

            foreach (string langCode in allLanguages.Keys)
            {
                string theTranslation;
                if (allLanguages[langCode].TryGetValue(theKeyword, out theTranslation))
                {
                    CultureInfo cultureInfo = new CultureInfo(langCode);
                    string separated = cultureInfo.LCID.ToString() + "," + theTranslation;
                    if (!isFirst)
                    {
                        stringBuidler.Append(",");
                    }

                    stringBuidler.Append(separated);
                    isFirst = false;
                }
            }

            return stringBuidler.ToString();
        }

        private static void ClearResXCache(string rootCache)
        {
            try
            {
                Directory.Delete(rootCache, true);
            }
            catch (Exception)
            {
                // It is OK if we cannot clean up
            }

            try
            {
                Directory.CreateDirectory(rootCache);
            }
            catch (Exception)
            {
                // It is OK if we can't create it - this will be caught elsehwere
            }
        }
    }
}