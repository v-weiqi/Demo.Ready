using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Data.Linq;
using System.Globalization;
using System.IO;
using System.Net;

namespace XmlGenCore
{
    public class Helper
    {
        public const string AppNameMap = "AppNameMap.xml";
        public const string Namespace = "http://www.w3.org/2005/Atom";
        public const string ApplicationXPath = "//atom:entry[@type='application']";
        public const string AppDetectionEntriesXPath = "//atom:entry[not(@type)]";
        public const string UpdateTime = "/atom:feed/atom:updated";
        public const string KeyWordXPath = "atom:keywords/atom:keywordId";
        public const string InstallerUrlXPath = "atom:installerFile/atom:installerURL";
        public const string InstallerXPath = "atom:installers/atom:installer";
        public const string TrackingUrlXPath = "atom:installerFile/atom:trackingURL";
        public const string OsListXPath = "atom:installers/atom:installer/atom:osList/@idref";

        public static List<string> GetAzureAppsFromLiveFeed()
        {
            List<string> azureApplications = new List<string>();
            XmlDocument xDoc = Helper.LoadXmlDocFromFeed(FeedInterface.AppFeedLocation);
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xDoc.NameTable);
            nsmgr.AddNamespace("atom", Helper.Namespace);
            
            XmlNodeList apps = FeedInterface.GetWebApps(xDoc);            

            foreach (XmlNode app in apps)
            {
                XmlNodeList keywords = app.SelectNodes(KeyWordXPath, nsmgr);

                foreach (XmlNode keyword in keywords)
                {
                    if (keyword.InnerText.Contains("AzureReady"))
                    {
                        azureApplications.Add(app.SelectSingleNode("atom:productId", nsmgr).InnerText.ToLower());
                        break;
                    }
                }
            }

            return azureApplications;
        }

        public static List<string> GetKatalAppsFromLiveFeed()
        {
            List<string> katalApplications = new List<string>();
            XmlDocument xDoc = Helper.LoadXmlDocFromFeed(FeedInterface.KatalFeedLocation);
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xDoc.NameTable);
            nsmgr.AddNamespace("atom", Helper.Namespace);

            XmlNodeList apps = FeedInterface.GetWebApps(xDoc);

            foreach (XmlNode app in apps)
            {
                XmlNodeList keywords = app.SelectNodes(KeyWordXPath, nsmgr);

                foreach (XmlNode keyword in keywords)
                {
                    if (keyword.InnerText.Contains("KatalReady"))
                    {
                        katalApplications.Add(app.SelectSingleNode("atom:productId", nsmgr).InnerText.ToLower());
                        break;
                    }
                }
            }

            return katalApplications;
        }


        public static string ProductFeedLocation()
        {
            return Properties.Settings.Default.LiveProductFeed;
        }

        public static string AppFeedLocation()
        {
            return Properties.Settings.Default.LiveAppFeed;
        }

        public static string KatalFeedLocation()
        {
            return Properties.Settings.Default.KatalFeed;
        }



        // todo: Fix these two entries up
        public static string AzureFeedLocation()
        {
            return Properties.Settings.Default.KatalFeed;
        }

        public static string TC2FeedLocation()
        {
            return Properties.Settings.Default.LiveAppFeed;
        }

        public static string ConvertMachineLocaleToWebPILocale(string language)
        {
            switch (language)
            {
                case "zh-chs":
                    return ("zh-cn");
                case "zh-cht":
                    return ("zh-tw");
                case "zh-hk":
                    return ("zh-tw");
                case "pt-br":
                case "pt-pt":
                    return language;
                default:
                    return language.Split('-')[0];
            }
        }

        public static XmlDocument LoadXmlDocFromFeed(string feedLocation)
        {
            if (feedLocation.ToLower().StartsWith("http"))
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(feedLocation);
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    return LoadXmlDocFromFeed(response.GetResponseStream());
                }
            }
            else
            {
                using (FileStream feedStream = new FileStream(feedLocation, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return LoadXmlDocFromFeed(feedStream);
                }
            }
        }
        
        public static XmlDocument LoadXmlDocFromFeed(Stream feedStream)
        {
            if (feedStream.CanSeek)
            {
                feedStream.Seek(0, SeekOrigin.Begin);
            }
            XmlDocument xDoc = new XmlDocument();
            try
            {
                xDoc.Load(feedStream);
            }
            catch (Exception e)
            {
                LogClass.Logger().WriteLine("Could not load xml document. Received the following error: {0}", e.ToString());
            }

            return xDoc;
        }

        public static void GenerateMapFile(string liveFeed)
        {
            using (Stream appMapFileStream = new FileStream(AppNameMap, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                GenerateMapFile(liveFeed, appMapFileStream);
            }
        }

        public static void GenerateMapFile(string liveFeed, Stream toUse)
        {
            XmlDocument feedoc = LoadXmlDocFromFeed(liveFeed);
            XmlNamespaceManager namespaceManager = new XmlNamespaceManager(feedoc.NameTable);
            namespaceManager.AddNamespace("atom", Namespace);
            
            XmlNodeList apps = feedoc.DocumentElement.SelectNodes(ApplicationXPath, namespaceManager);
            XmlNode updated = feedoc.SelectSingleNode(UpdateTime, namespaceManager);
            Dictionary<string, string> applist = new Dictionary<string, string>();
            foreach (XmlNode app in apps)
            {
                string id = app.FirstChild.InnerText;
                XmlNode submittingurl = app.SelectSingleNode("atom:author/atom:uri", namespaceManager);
                string submitter;
                if (submittingurl != null)
                {
                    submitter = submittingurl.InnerText;
                    applist.Add(id, submitter);
                }
            }

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;

            LogClass.Logger().WriteLine("Generating AppNameMap  ...");
            toUse.Seek(0, SeekOrigin.Begin);
            toUse.SetLength(0);
            XmlWriter writer = XmlTextWriter.Create(toUse, settings);
            writer.WriteStartDocument();
            writer.WriteStartElement("root");
            writer.WriteElementString("updated", updated.InnerText);

            foreach (KeyValuePair<string, string> appmap in applist)
            {
                writer.WriteStartElement("app");
                writer.WriteAttributeString("submitter", appmap.Value);
                writer.WriteAttributeString("appname", appmap.Key);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndDocument();
            writer.Flush();
        }

        public static XmlNodeList GetOriginalAppDetectionEntries()
        {
            XmlDocument xDocument = LoadXmlDocFromFeed(FeedInterface.AppFeedLocation);
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xDocument.NameTable);
            nsmgr.AddNamespace("atom", Helper.Namespace);

            return xDocument.SelectNodes(Helper.AppDetectionEntriesXPath, nsmgr);
        }
        
        private static string WebAppListLocation()
        {
            return FeedInterface.AppFeedLocation;
        }

        public static void SaveXmlDocWithFormatting(XmlDocument xDoc, string fileName)
        {
            using (FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                SaveXmlDocWithFormatting(xDoc, stream);
            }
        }

        public static void SaveXmlDocWithFormatting(XmlDocument xDoc, Stream fileStream)
        {
            fileStream.Seek(0, SeekOrigin.Begin);
            fileStream.SetLength(0);
            xDoc.Save(fileStream);

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "  ";
            settings.NewLineChars = "\r\n";
            settings.NewLineHandling = NewLineHandling.Replace;
            fileStream.Seek(0, SeekOrigin.Begin);
            xDoc.Load(fileStream);
            fileStream.Seek(0, SeekOrigin.Begin);
            fileStream.SetLength(0);
            XmlWriter fwriter = XmlWriter.Create(fileStream, settings);
            xDoc.Save(fwriter);
            fwriter.Flush();
            fileStream.Flush();
        }

        public static void WriteFileHeading(XmlWriter writer, string Namespace)
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("feed", Namespace);

            // todo: Need to open up the real feed and get the version!!!
            string version = "4.5";

            writer.WriteStartElement("webpiFile");
            writer.WriteAttributeString("version", version + ".0.0");
            writer.WriteEndElement();
            writer.WriteElementString("title", "Web Platform Installer " + version + " Application Feed");
            writer.WriteStartElement("link", "");
            writer.WriteAttributeString("rel", "self");
            writer.WriteAttributeString("href", WebAppListLocation());
            writer.WriteEndElement();
            DateTimeFormatInfo di = new DateTimeFormatInfo();
            DateTime dt = DateTime.UtcNow;
            string DateUpdated = dt.ToString("s", di) + "Z";
            writer.WriteElementString("updated", DateUpdated);
            writer.WriteStartElement("author");
            writer.WriteElementString("name", "Microsoft");
            writer.WriteEndElement();
            writer.WriteElementString("id", WebAppListLocation());
        }

        public static void WriteSingleDependency(XmlWriter writer, string dependencyName)
        {
            writer.WriteStartElement("dependency");
            writer.WriteAttributeString("idref", dependencyName);
            writer.WriteEndElement();
        }

        public static void WriteAndDependencies(XmlWriter writer, params string[] dependencyNames)
        {
            writer.WriteStartElement("dependency");
            writer.WriteStartElement("and");

            foreach (string dependency in dependencyNames)
            {
                WriteSingleDependency(writer, dependency);
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        public enum NodeType { ResourceList, Dependencies, OSList, OSes, KeyWords, Featured, Languages };
        public static Dictionary<NodeType, XmlNode>NeededDocumentSections(XmlDocument xDoc, XmlNamespaceManager xNSMgr)
        {
            Dictionary<NodeType, XmlNode> d = new Dictionary<NodeType, XmlNode>();

            d.Add(NodeType.ResourceList, xDoc.DocumentElement.SelectSingleNode("/atom:feed/atom:resourcesList", xNSMgr));
            d.Add(NodeType.Dependencies, xDoc.DocumentElement.SelectSingleNode("/atom:feed/atom:dependencies", xNSMgr));
            d.Add(NodeType.OSList, xDoc.DocumentElement.SelectSingleNode("/atom:feed/atom:osLists", xNSMgr));
            d.Add(NodeType.OSes, xDoc.DocumentElement.SelectSingleNode("/atom:feed/atom:oses", xNSMgr));
            d.Add(NodeType.KeyWords, xDoc.DocumentElement.SelectSingleNode("/atom:feed/atom:keywords", xNSMgr));
            d.Add(NodeType.Featured, xDoc.DocumentElement.SelectSingleNode("/atom:feed/atom:featured", xNSMgr));

            return d;
        }        
    }

    public static class IDictionaryExtensions
    {
        public static TKey FindKeyByValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TValue value)
        {
            if (dictionary == null)
                throw new ArgumentNullException("dictionary");

            foreach (KeyValuePair<TKey, TValue> pair in dictionary)
                if (value.Equals(pair.Value)) return pair.Key;

            throw new Exception("the value is not found in the dictionary");
        }
    }
}
