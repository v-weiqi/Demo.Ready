using System;
using System.Net;
using System.Collections.Generic;
using System.Xml ;
using System.IO;


namespace XmlGenCore
{
    public class MergeFeed
    {
        private readonly XmlDocument _liveFeedDoc;
        private readonly XmlDocument _testFeedDoc;
        private readonly XmlNamespaceManager _liveFeedNSMgr;
        private readonly XmlNamespaceManager _testFeedNSMgr;
        private readonly Stream _newFeedStream;
        private readonly LogClass _logWriter;
        
        private MergeFeed(string liveFeed, Stream testFeed, Stream newFeed, TextWriter logWriter)
        {
            _liveFeedDoc = Helper.LoadXmlDocFromFeed(liveFeed);
            _liveFeedNSMgr = new XmlNamespaceManager(_liveFeedDoc.NameTable);
            _liveFeedNSMgr.AddNamespace("atom", Helper.Namespace);

            if (testFeed.CanSeek)
            {
                testFeed.Seek(0, SeekOrigin.Begin);
            }
            _testFeedDoc = Helper.LoadXmlDocFromFeed(testFeed);
            _testFeedNSMgr = new XmlNamespaceManager(_testFeedDoc.NameTable);
            _testFeedNSMgr.AddNamespace("atom", Helper.Namespace);

            newFeed.SetLength(0);
            if (newFeed.CanSeek)
            {
                newFeed.Seek(0, SeekOrigin.Begin);
            }
            _newFeedStream = newFeed;
            _logWriter = LogClass.Logger();
        }

        public static void MergeFeeds(string liveFeed, string testFeed, string fileName, bool mergeFeed = false, TextWriter logWriter = null)
        {
            using (Stream testFeedStream = new FileStream(testFeed, FileMode.Open, FileAccess.Read, FileShare.None),
                newFeedStream = new FileStream(fileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                if (logWriter == null)
                {
                    logWriter = Console.Out;
                }
                var mf = new MergeFeed(liveFeed, testFeedStream, newFeedStream, logWriter);
                mf.Merge(FeedType.Live, mergeFeed);
            }
        }

        public static void MergeFeeds(string liveFeed, Stream testFeed, Stream newFeed, FeedType typeOfFeed, bool mergeInstallers = false, TextWriter logWriter = null)
        {
            var mf = new MergeFeed(liveFeed, testFeed, newFeed, logWriter);
            mf.Merge(typeOfFeed, mergeInstallers);
        }

        public static void MergeResourceFeed(string livefeed, Stream testFeed, Stream mergedFeed, TextWriter logWriter = null)
        {
            var mf = new MergeFeed(livefeed, testFeed, mergedFeed, logWriter);
            mf.MergeResources();
        }

        public void Merge(FeedType typeOfFeed, bool mergeInstallers = false)
        {
            Dictionary<string, XmlNode> testFeedAppEntries = GetTestFeedEntries();
            _logWriter.WriteLine("Reading from live feed ... ");
            Dictionary<Helper.NodeType, XmlNode> docSections = Helper.NeededDocumentSections(_liveFeedDoc, _liveFeedNSMgr);
            docSections.Add(Helper.NodeType.Languages, _liveFeedDoc.DocumentElement.SelectSingleNode("/atom:feed/atom:languages", _liveFeedNSMgr));

            var liveFeedApps = _liveFeedDoc.DocumentElement.SelectNodes(Helper.ApplicationXPath, _liveFeedNSMgr);
            _logWriter.WriteLine("Merging Live Feed content with new apps ...  ");

            var newFeedDoc = CreateNewFeedDoc();

            AddNodesOutXmlToFeed(newFeedDoc, docSections[Helper.NodeType.ResourceList]);
            AddNodesOutXmlToFeed(newFeedDoc, docSections[Helper.NodeType.Languages]);

            AddAppNodes(liveFeedApps, newFeedDoc, testFeedAppEntries, mergeInstallers, typeOfFeed);            

            AddNodesOutXmlToFeed(newFeedDoc, docSections[Helper.NodeType.KeyWords], "Adding keywords to the feed .. ");
            AddNodesOutXmlToFeed(newFeedDoc, docSections[Helper.NodeType.OSes], "Adding OSes to the feed .. ");
            AddNodesOutXmlToFeed(newFeedDoc, docSections[Helper.NodeType.OSList], "Adding OSLists to the feed .. ");
            AddNodesOutXmlToFeed(newFeedDoc, docSections[Helper.NodeType.Dependencies], "Adding dependencies to the feed .. ");
            AddNodesOutXmlToFeed(newFeedDoc, docSections[Helper.NodeType.Featured]);

            Helper.SaveXmlDocWithFormatting(newFeedDoc, _newFeedStream);

            _newFeedStream.Seek(0, SeekOrigin.Begin);
            var sr = new StreamReader(_newFeedStream);
            var text = sr.ReadToEnd();

            var rtext = text.Replace(
                "entry type=\"application\" xmlns=\"http://www.w3.org/2005/Atom\"", 
                "entry type=\"application\"");

            var result = rtext.Replace("xmlns=\"\"", "");
            result = result.Replace("entry xmlns=\"http://www.w3.org/2005/Atom\"", "entry");
            result = result.Replace("resourcesList xmlns=\"" + Helper.Namespace + "\"", "resourcesList");
            result = result.Replace("keywords xmlns=\"" + Helper.Namespace + "\"", "keywords");
            result = result.Replace("dependencies xmlns=\"" + Helper.Namespace + "\"", "dependencies");
            result = result.Replace("oses xmlns=\"" + Helper.Namespace + "\"", "oses");
            result = result.Replace("osLists xmlns=\"" + Helper.Namespace + "\"", "osLists");
            result = result.Replace("featured xmlns=\"" + Helper.Namespace + "\"", "featured");
            result = result.Replace("languages xmlns=\"" + Helper.Namespace + "\"", "languages");

            _newFeedStream.SetLength(0);
            _newFeedStream.Seek(0, SeekOrigin.Begin);

            var sw = new StreamWriter(_newFeedStream);
            sw.Write(result);
            sw.Flush();
            _newFeedStream.Flush();
        }

        public XmlNodeList GetTestFeedApplications()
        {
            XmlNodeList apps = null;
            if (_testFeedDoc != null && _testFeedDoc.DocumentElement != null)
            {
                apps = _testFeedDoc.DocumentElement.SelectNodes(Helper.ApplicationXPath, _testFeedNSMgr);
            }

            return apps;
        }

        private Dictionary<string, XmlNode> GetTestFeedEntries()
        {
            XmlNodeList testFeedAppEntriesList = GetTestFeedApplications();

            Dictionary<string, XmlNode> testFeedAppEntries = new Dictionary<string, XmlNode>();
            if (testFeedAppEntriesList != null)
            {
                foreach (XmlNode y in testFeedAppEntriesList)
                {
                    string productid = y.FirstChild.InnerText;
                    // todo: need to handle duplicate app ids as it appears the db does not stay in a good state.
                    try
                    {
                        testFeedAppEntries.Add(productid, y);
                    }
                    catch { }
                }
            }

            return testFeedAppEntries;
        }

        private XmlDocument CreateNewFeedDoc(bool official=true)
        {
            _newFeedStream.SetLength(0);
            _newFeedStream.Seek(0, SeekOrigin.Begin);
            XmlTextWriter writer = new XmlTextWriter(_newFeedStream, System.Text.Encoding.UTF8);
            writer.Formatting = Formatting.Indented;
            if (official)
            {
                Helper.WriteFileHeading(writer, Helper.Namespace);
            }
            else
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("root", Helper.Namespace);
            }
            writer.WriteEndElement();
            writer.WriteEndDocument();
            writer.Flush();

            return Helper.LoadXmlDocFromFeed(_newFeedStream);
        }

        private XmlNode KeepDependencyListWithoutUpdates(XmlNode originalNode, XmlNode newNode)
        {
            XmlNode combinedNode = newNode.Clone();
            string dependencyListXPath = "atom:dependency/@idref";

            XmlNode originalDependencyNode = originalNode.SelectSingleNode(dependencyListXPath, _liveFeedNSMgr);
            if (originalDependencyNode != null)
            {
                XmlNode newDependencyNode = combinedNode.SelectSingleNode(dependencyListXPath, _testFeedNSMgr);
                if (newDependencyNode != null)
                {
                    newDependencyNode.InnerXml = originalDependencyNode.InnerXml;
                }
            }

            return combinedNode;
        }

        private XmlNode KeepOsListWithoutUpdates(XmlNode originalNode, XmlNode newNode)
        {
            const string installerOsNode = "atom:installers/atom:installer/atom:osList";
            
            XmlNode combinedNode = newNode.Clone();
            XmlNode combinedOsNode = combinedNode.SelectSingleNode(installerOsNode, _testFeedNSMgr);
            XmlNode originalOsNode = originalNode.SelectSingleNode(installerOsNode, _liveFeedNSMgr);

   

            if (combinedOsNode != null && originalOsNode != null && originalOsNode.HasChildNodes)
            {
                XmlNode installerNode = combinedOsNode.ParentNode;
                installerNode.ReplaceChild(installerNode.OwnerDocument.ImportNode(originalOsNode, true), combinedOsNode);               
             }
           

            return combinedNode;
        }

   
        private string GetPackageName(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            string packageName = null;
           
            try
            {
                if (String.IsNullOrWhiteSpace(packageName) && url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    packageName = Path.GetFileName(url);
                }
                else
                {
                    packageName = DownloadHelper.GetContentDispositionHeader(new Uri(url));

                    if (packageName == null)
                    {
                        packageName = DownloadHelper.GetContentDispositionHeaderWininet(new Uri(url));
                    }
                }
            }
            catch
            {
            }
            
            return packageName;
        }

        private XmlNode ReplaceInstallerUrl(XmlNode node, FeedType typeOfFeed)
        {            
            XmlNodeList installers = node.SelectNodes(Helper.InstallerXPath, _testFeedNSMgr);

            string productId = node.SelectSingleNode("atom:productId", _liveFeedNSMgr).InnerText.ToLower();
            if (typeOfFeed != FeedType.TC2 && typeOfFeed != FeedType.PROD) return node;

            foreach (XmlNode installer in installers)
            {
                XmlNode originalInstallUrl = installer.SelectSingleNode(Helper.InstallerUrlXPath, _liveFeedNSMgr);
                string packageName = GetPackageName(originalInstallUrl.InnerText);
                if (String.IsNullOrWhiteSpace(packageName))
                {
                    throw new InvalidSubmissionDataException(productId,
                        "Installer URL",
                        String.Format("The package name cannot be obtaining from the installer URL. Please check the URL '{0}'.",
                            originalInstallUrl.InnerText));
                }
                packageName = packageName.TrimStart('/', '\\');                
                string baseUrl = typeOfFeed == FeedType.TC2 ? Properties.Settings.Default.AzureTC2Blob : Properties.Settings.Default.AzureProdFeed;
                baseUrl = baseUrl.TrimEnd('/', '\\');

                if (packageName == null)
                {
                    baseUrl = originalInstallUrl.InnerText;
                }

                string url = String.Format("{0}/{1}", baseUrl, packageName).TrimEnd('/');

                // We should see if the file is there or not and determine what url to use.
                if (true)
                {
                    installer.SelectSingleNode(Helper.InstallerUrlXPath, _testFeedNSMgr).InnerText = url;
                }
            }

            return node;
        }

        private XmlNode KeepInstallersWithoutUpdates(XmlNode originalNode, XmlNode newNode, FeedType typeOfFeed)
        {
            XmlNode combinedNode = newNode.Clone();            
            XmlNodeList newInstallers = combinedNode.SelectNodes(Helper.InstallerXPath, _testFeedNSMgr);

            foreach (XmlNode installer in newInstallers)
            {
                XmlNode id = installer.SelectSingleNode("atom:id", _testFeedNSMgr);
                XmlNode newInstallUrl = installer.SelectSingleNode(Helper.InstallerUrlXPath, _testFeedNSMgr);
                XmlNode originalInstaller = originalNode.SelectSingleNode(Helper.InstallerXPath + "[atom:id="+id.InnerText+"]", _liveFeedNSMgr);
                if (originalInstaller != null)
                {
                    XmlNode originalInstallUrl = originalInstaller.SelectSingleNode(Helper.InstallerUrlXPath, _liveFeedNSMgr);
                    string baseUrl = null;
                    string packageName = GetPackageName(originalInstallUrl.InnerText);
                    if (!String.IsNullOrWhiteSpace(packageName))
                    {
                        packageName = packageName.TrimStart('/', '\\');
                    }

                    if (typeOfFeed == FeedType.TC2)
                    {
                        baseUrl = Properties.Settings.Default.AzureTC2Blob;
                    }
                    else if (typeOfFeed == FeedType.PROD)
                    {
                        baseUrl = Properties.Settings.Default.AzurePRODBlob;
                    }

                    if (!String.IsNullOrWhiteSpace(baseUrl))
                    {
                        newInstallUrl.InnerText = String.Format("{0}/{1}", baseUrl.TrimEnd('/', '\\'), packageName);
                    }

                    if (originalInstallUrl.InnerText == newInstallUrl.InnerText)
                    {
                        newInstallUrl.InnerXml = originalInstaller.SelectSingleNode(Helper.InstallerUrlXPath, _liveFeedNSMgr).InnerXml;
                    }                    
                }
            }

            return combinedNode;
        }
        
        private void AddAppNodes(
            XmlNodeList liveFeedApps, 
            XmlDocument newFeedDoc, 
            Dictionary<string, XmlNode> testFeedAppEntries, 
            bool mergeInstallers,
            FeedType typeOfFeed)
        {
            List<string> addedentries = new List<string>();
            int count = liveFeedApps.Count;
            XmlNode node;            

            foreach (XmlNode entry in liveFeedApps)
            {
                string productid = entry.FirstChild.InnerText;
                bool fromTestFeed = testFeedAppEntries.TryGetValue(productid, out node);

                if (fromTestFeed)
                {
                    addedentries.Add(productid);
                    if (mergeInstallers)
                    {
                        node = KeepDependencyListWithoutUpdates(entry, node);
                        node = KeepOsListWithoutUpdates(entry, node);
                        node = KeepInstallersWithoutUpdates(entry, node, typeOfFeed);
                    }

                    XmlNode testNodeAddedToFeedDate = node.SelectSingleNode("atom:addToFeedDate", _testFeedNSMgr);
                    XmlNode originalAddedToFeedDate = entry.SelectSingleNode("atom:addToFeedDate", _liveFeedNSMgr);

                    if (testNodeAddedToFeedDate != null && originalAddedToFeedDate != null)
                    {
                        testNodeAddedToFeedDate.InnerXml = originalAddedToFeedDate.InnerXml;
                    }
                }
                else
                {
                    // TODO: Uncomment below depending on source (TC2/Live)
                    //node = ReplaceInstallerUrl(entry, typeOfFeed);
                    node = entry;
                }

                AddNodesOutXmlToFeedWithComment(newFeedDoc, node, "Adding" + productid, "<!-- " + productid + "-->");
            }
            
            foreach (KeyValuePair<string, XmlNode> pair in testFeedAppEntries)
            {
                string productId = pair.Key;
                if (!addedentries.Contains(productId))
                {
                    count++;
                    XmlNode newAppnode = ReplaceInstallerUrl(pair.Value, typeOfFeed);
                    AddNodesOutXmlToFeedWithComment(newFeedDoc, newAppnode, "Adding" + productId, "<!-- " + productId + "-->");
                }
            }

            XmlNodeList appDetectionNodeList = Helper.GetOriginalAppDetectionEntries();
            foreach (XmlNode entry in appDetectionNodeList)
            {
                AddNodesOutXmlToFeed(newFeedDoc, entry);
            }


            _logWriter.WriteLine("-------------------------------------------------------------------");
            _logWriter.WriteLine("Added {0} apps to the feed ", count);
            _logWriter.WriteLine("-------------------------------------------------------------------");
        }

        private void AddNodesOutXmlToFeed(XmlDocument xDoc, XmlNode node, string infoMessage)
        {
            _logWriter.WriteLine(infoMessage);
            AddNodesOutXmlToFeed(xDoc, node);
        }

        private void AddNodesOutXmlToFeed(XmlDocument xDoc, XmlNode node)
        {
            AddNodesOutXmlToFeedWithComment(xDoc, node, String.Empty);
        }

        private void AddNodesOutXmlToFeedWithComment(XmlDocument xDoc, XmlNode node, string infoMessage, string comment)
        {
            _logWriter.WriteLine(infoMessage);
            AddNodesOutXmlToFeedWithComment(xDoc, node, comment);
        }

        private void AddNodesOutXmlToFeedWithComment(XmlDocument xDoc, XmlNode node, string comment)
        {
            if (comment == null)
            {
                comment = String.Empty;
            }

            XmlDocumentFragment fragment = xDoc.CreateDocumentFragment();
            fragment.InnerXml = comment + node.OuterXml;
            xDoc.DocumentElement.AppendChild(fragment);
        }

        public void MergeResources()
        {
            _logWriter.WriteLine("Reading from test feed ");
            XmlNodeList testFeedDataNodes = _testFeedDoc.DocumentElement.SelectNodes("//data");

            Dictionary<string, XmlNode> appentries = new Dictionary<string, XmlNode>();
            foreach (XmlNode y in testFeedDataNodes)
            {
                appentries.Add(y.Attributes[1].Value.ToLower(), y);
            }

            _logWriter.WriteLine("reading from live feed");
            XmlNodeList liveFeedDataNodes = _liveFeedDoc.DocumentElement.SelectNodes("//data");

            _logWriter.WriteLine("writing merged feed...  ");
            XmlDocument mergedFeedDoc = CreateNewFeedDoc(false);
            XmlNode entry = null;
            foreach (XmlNode oneEntry in liveFeedDataNodes)
            {
                if (!appentries.TryGetValue(oneEntry.Attributes[0].Value.ToLower(), out entry))
                {
                    entry = oneEntry;
                }
                AddNodesOutXmlToFeed(mergedFeedDoc, entry);
            }

            _newFeedStream.SetLength(0);
            _newFeedStream.Seek(0, SeekOrigin.Begin);
            mergedFeedDoc.Save(_newFeedStream);

            _logWriter.WriteLine("DONE...");
        }
    }
}
