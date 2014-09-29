using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;

namespace XmlGenCore
{
    public enum FeedType
    {
        Live,
        TC2,
        PROD,
        Katal,
    }

    public enum FeedActivity
    {
        GenerateWithAllApps,
        GenerateWithReadyToPublishApps,
        GenerateWithTestingApps,
        List
    }

    public class FeedInterface
    {
        private static string _feedLocation;
        private static XmlNamespaceManager _nsMgr;
        private static string _katalFeedLocation;

        private static string ProductFeedUrl
        {
            set
            {
                _feedLocation = value;
            }
            get { return _feedLocation ?? (_feedLocation = Helper.ProductFeedLocation()); }
        }

        public static string KatalFeedLocation
        {
            set
            {
                _katalFeedLocation = value;
            }
            get { return _katalFeedLocation ?? (_katalFeedLocation = Properties.Settings.Default.KatalFeed); }
        }
      
        public static string AppFeedLocation
        {
            set
            {
                _feedLocation = value;
            }
            get { return _feedLocation ?? (_feedLocation = Helper.AppFeedLocation()); }
        }

        // todo: can I generate this using reflection?
        public static string GetFeed(FeedType type)
        {
            switch (type)
            {
                case FeedType.Katal:
                    return Properties.Settings.Default.KatalFeed;
                case FeedType.Live:
                    return Properties.Settings.Default.LiveAppFeed;
                case FeedType.PROD:
                    return Properties.Settings.Default.AzureProdFeed;
                case FeedType.TC2:
                    return Properties.Settings.Default.AzureTC2Feed;
                   
                default:
                    throw new ArgumentException(String.Format("The type {0} is not currently supported.", type));
            }
        }

        public static Dictionary<string, MemoryStream> GenerateResourceFeed(CoreGenerationSettings settings)
        {
            var resourceFiles = new Dictionary<string, MemoryStream>(16);
            ReourceGenerator.CreateResourceFiles(resourceFiles, settings.AppIdsCasePreserved[0]);

            return resourceFiles;
        }

        public static XmlDocument GenerateFeed(CoreGenerationSettings settings)
        {
            using (MemoryStream appMapFile = new MemoryStream(), testingAppFeed = new MemoryStream(), mergedFeed = new MemoryStream())
            {
                var fUtil = new FeedUtil(settings, appMapFile);
                var appStatesToUse = FeedUtil.AppStates.All;
                
                switch (settings.FeedGenerationActivity)
                {
                    case FeedActivity.GenerateWithAllApps:
                        appStatesToUse = FeedUtil.AppStates.All;
                        break;
                    case FeedActivity.GenerateWithReadyToPublishApps:
                        appStatesToUse = FeedUtil.AppStates.ReadyToPublish;
                        break;
                    case FeedActivity.GenerateWithTestingApps:
                        appStatesToUse = FeedUtil.AppStates.Testing;                      
                        break;
                    case FeedActivity.List:
                        appStatesToUse = null;
                        break;
                }

                fUtil.WriteFile(appStatesToUse, testingAppFeed);
                XmlDocument xTestApps = Helper.LoadXmlDocFromFeed(testingAppFeed);
                _nsMgr = new XmlNamespaceManager(xTestApps.NameTable);
                _nsMgr.AddNamespace("atom", Helper.Namespace);
                RemoveUnrequestedApps(xTestApps, settings.TestAppIdFilter);                
                if (settings.MarkTestAppsAzureReady)
                {
                    MarkAppsWithAzureFlag(xTestApps);
                }

                if (settings.MarkTestAppsKatalReady)
                {
                    MarkAppsWithKatalFlag(xTestApps);
                }
                Helper.SaveXmlDocWithFormatting(xTestApps, testingAppFeed);

                MergeFeed.MergeFeeds(settings.ProductFeedUrl, testingAppFeed, mergedFeed, settings.FeedGenerationType, true);

                XmlDocument xDoc = Helper.LoadXmlDocFromFeed(mergedFeed);
                _nsMgr = new XmlNamespaceManager(xDoc.NameTable);
                _nsMgr.AddNamespace("atom", Helper.Namespace);

                RemoveUnrequestedApps(xDoc, settings.AppIds);

                return xDoc;
            }
        }

        private static void RemoveUnrequestedApps(XmlDocument mergedFeed, ICollection<string> requestedApps)
        {
            if (requestedApps != null && requestedApps.Count != 0)
            {                
                XmlNodeList appNodes = GetWebApps(mergedFeed);

                foreach (XmlNode app in appNodes)
                {
                    var appId = GetProductId(app);
                    if (!requestedApps.Contains(appId))
                    {
                        mergedFeed.DocumentElement.RemoveChild(app);
                    }
                }
            }
        }

        public static List<string> GetInstallers(XmlDocument xDoc, string appId)
        {            
            var installerUrls = new List<string>();
            var applications = GetWebApps(xDoc);            
            _nsMgr = new XmlNamespaceManager(xDoc.NameTable);
            _nsMgr.AddNamespace("atom", Helper.Namespace);

            foreach (XmlNode application in applications)
            {
                var applicationId = GetProductId(application);
               
                if (applicationId.Equals(appId, StringComparison.OrdinalIgnoreCase))
                {
                    var installers = application.SelectNodes(Helper.InstallerXPath, _nsMgr);
                    foreach (XmlNode installer in installers)
                    {
                        var installerUrl = installer.SelectSingleNode(Helper.InstallerUrlXPath, _nsMgr);
                        installerUrls.Add(installerUrl.InnerText);
                    }
                    return installerUrls;
                }
            }
            return null;
        }

        private static void MarkAppsWithAzureFlag(XmlDocument xDoc)
        {
            var apps = GetWebApps(xDoc);
            XmlNode keywords = null;

            foreach (XmlNode app in apps)
            {
                string appId = GetProductId(app);
                keywords = app.SelectSingleNode("atom:" + FeedUtil.Keywords, _nsMgr);
                if (keywords == null)
                {
                    keywords = xDoc.CreateNode(XmlNodeType.Element, FeedUtil.Keywords, Helper.Namespace);
                    app.AppendChild(keywords);
                }

                XmlNode azureReady = 
                    keywords.SelectSingleNode("atom:" + FeedUtil.KeywordId + "[.='" + FeedUtil.AzureReady + "']", _nsMgr);
                if (azureReady == null)
                {
                    azureReady = xDoc.CreateNode(XmlNodeType.Element, FeedUtil.KeywordId, Helper.Namespace);
                    azureReady.InnerText = FeedUtil.AzureReady;
                    keywords.AppendChild(azureReady);
                }
            }

            keywords = xDoc.DocumentElement.SelectSingleNode("atom:" + FeedUtil.Keywords, _nsMgr);
            if (keywords != null)
            {
                XmlNode keyword = keywords.SelectSingleNode("atom:keyword[@id='" + FeedUtil.AzureReady + "']", _nsMgr);
                if (keyword == null)
                {
                    keyword = xDoc.CreateNode(XmlNodeType.Element, "keyword", Helper.Namespace);
                    XmlAttribute attrib1 = xDoc.CreateAttribute("id");
                    attrib1.Value = FeedUtil.AzureReady;
                    XmlAttribute attrib2 = xDoc.CreateAttribute("resourceName");
                    attrib2.Value = "Keyword_AzureReady";
                    keyword.Attributes.Append(attrib1);
                    keyword.Attributes.Append(attrib2);
                    keyword.InnerText = FeedUtil.AzureReady;
                    keywords.AppendChild(keyword);
                }
            }
        }

        private static void MarkAppsWithKatalFlag(XmlDocument xDoc)
        {
            XmlNodeList apps = GetWebApps(xDoc);            
            XmlNode keywords = null;

            foreach (XmlNode app in apps)
            {
                string appId = GetProductId(app);
                keywords = app.SelectSingleNode("atom:" + FeedUtil.Keywords, _nsMgr);
                if (keywords == null)
                {
                    keywords = xDoc.CreateNode(XmlNodeType.Element, FeedUtil.Keywords, Helper.Namespace);

                    app.AppendChild(keywords);
                }
            }

            GenerateKatalVersionKeywords(FeedUtil.OnPremReadyV1, xDoc, keywords);
            GenerateKatalVersionKeywords (FeedUtil.OnPremReadyV2 , xDoc, keywords );

            keywords = xDoc.DocumentElement.SelectSingleNode("atom:" + FeedUtil.Keywords, _nsMgr);
            if (keywords != null)
            {
                XmlNode keyword = keywords.SelectSingleNode("atom:keyword[@id='" + FeedUtil.OnPremReadyV1 + "']", _nsMgr);
                if (keyword == null)
                {
                    keyword = xDoc.CreateNode(XmlNodeType.Element, "keyword", Helper.Namespace);
                    XmlAttribute attrib1 = xDoc.CreateAttribute("id");
                    attrib1.Value = FeedUtil.OnPremReadyV1;
                    XmlAttribute attrib2 = xDoc.CreateAttribute("resourceName");
                    attrib2.Value = "Keyword_KatalReady";
                    keyword.Attributes.Append(attrib1);
                    keyword.Attributes.Append(attrib2);
                    keyword.InnerText = FeedUtil.OnPremReadyV1;
                    keywords.AppendChild(keyword);
                }
            }
        }        

        private static void GenerateKatalVersionKeywords (string katalKeyword , XmlDocument xDoc, XmlNode keywordsNode)
        {
            if (keywordsNode != null)
            {
                XmlNode keyword = keywordsNode.SelectSingleNode("atom:" + FeedUtil.KeywordId + "[.='" + katalKeyword + "']", _nsMgr);
                if (keyword == null)
                {
                    keyword = xDoc.CreateNode(XmlNodeType.Element, FeedUtil.KeywordId, Helper.Namespace);
                    keyword.InnerText = katalKeyword;
                    keywordsNode.AppendChild(keyword);
                }
            }
        }
        public static XmlNodeList GetWebApps(XmlDocument xDoc)
        {
            var nsmgr = new XmlNamespaceManager(xDoc.NameTable);
            nsmgr.AddNamespace("atom", Helper.Namespace);

            return xDoc.SelectNodes(Helper.ApplicationXPath, nsmgr);
        }

        public static XmlDocument GetModifiedProductFeed(Uri requestPath)
        {
            var xDoc = Helper.LoadXmlDocFromFeed(ProductFeedUrl);
            var nsMgr = new XmlNamespaceManager(xDoc.NameTable);
            nsMgr.AddNamespace("atom", Helper.Namespace);

            var webAppLink = xDoc.SelectSingleNode("//atom:link[contains(@href, 'webapplicationlist.xml')]", nsMgr);
            webAppLink.Attributes["href"].Value = requestPath.AbsoluteUri.Replace(".wpf", ".waf");

            return xDoc;
        }

        private static string GetProductId(XmlNode node)
        {            
            return node.SelectSingleNode("atom:productId", _nsMgr).InnerText.ToLower();
        }      
    }
}
