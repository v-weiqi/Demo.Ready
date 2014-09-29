using System;
using System.Collections.Generic;
using System.Data.Linq;
using System.IO;
using System.Linq;
using System.Xml;
using System.Globalization;


namespace XmlGenCore
{
    struct ProductMetaDataInfo
    {
        public string Title;
        public string TitleResourceName;
        public string Summary;
        public string SummaryResourceName;
        public string LongSummary;
        public string LongSummaryResourceName;
    };

    public class FeedUtil
    {
        public sealed class AppStates
        {
            public const string ReadyToPublish = "Ready To Publish";
            public const string Testing = "Testing";
            public const string All = "All";
        }

        public const string OnPremReadyV1 = "KatalReady";
        public const string AzureReady = "AzureReady";
        public const string Keywords = "keywords";
        public const string KeywordId = "keywordId";
        public const string OnPremReadyV2 = "OnPremReadyV2";

        private readonly Dictionary<string, string> _appMapList = new Dictionary<string, string>();
        private readonly Dictionary<int, string> _currentProducts = new Dictionary<int, string>();
        private Dictionary<int, string> _categoriesMapping;
        private Dictionary<int, string> _frameworksAndRuntimesMapping;
        private Dictionary<int, string> _databaseServersMapping;
        private SortedDictionary<string, string> _relatedProducts;

        private readonly string[] _knownStartPages = { String.Empty, "index.php", "default.aspx" };

        private Dictionary<int, string> _webServerExtensionMappings = new Dictionary<int, string>();
        private readonly string[] _supportLanguages = Properties.Settings.Default.languages.Split(',');

        private int _count;
        //private bool katalflag = false;
        private bool _useMapFileStream = false;
        //private bool _computeShaForPackage = false;
        private Stream _mapFile = null;
        private LogClass _logWriter = null;

        private const string ImageUrlPrefix = "http://www.microsoft.com/web/handlers/webpi.ashx?command=getimage&guid=";
        private readonly CoreGenerationSettings _settings;
        //private const string MergeTypeCommand = "-merge";
        //private const string KatalOption = "-katal";
        //private const string AzureOption = "-azure";
        //private const string MergeInstallersOption = "-mergeInstallers";

        public FeedUtil(CoreGenerationSettings settings)
            : this(settings, null)
        {
        }

        public FeedUtil(CoreGenerationSettings settings, Stream mapFile)
            : this(settings, mapFile, Console.OpenStandardOutput())
        {
        }

        public FeedUtil(CoreGenerationSettings settings, Stream mapFile, Stream logFile)
        {
            _settings = settings;
            _useMapFileStream = (mapFile != null);
            _mapFile = mapFile;
            _logWriter = LogClass.Logger();
        }

        private XmlDocument LoadMapFile()
        {
            XmlDocument xDoc = new XmlDocument();
            try
            {
                if (_useMapFileStream)
                {
                    if (_mapFile.CanSeek)
                    {
                        _mapFile.Seek(0, SeekOrigin.Begin);
                    }
                    xDoc = Helper.LoadXmlDocFromFeed(_mapFile);
                }
                else
                {
                    xDoc = Helper.LoadXmlDocFromFeed(Helper.AppNameMap);
                }
            }
            catch { }

            return xDoc;
        }


        public bool WriteFile(string states, string fileName)
        {
            using (FileStream stream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                return WriteFile(states, stream);
            }
        }

        public bool WriteFile(string states, Stream fileStream)
        {
            var successful = true;
            var feedUri = FeedInterface.GetFeed(_settings.FeedGenerationType);

            var feedoc = Helper.LoadXmlDocFromFeed(feedUri);
            var feedNSMgr = new XmlNamespaceManager(feedoc.NameTable);
            feedNSMgr.AddNamespace("atom", Helper.Namespace);

            //Read from AppNameMap
            var mapdoc = LoadMapFile();

            var feedUpdateTime = feedoc.SelectSingleNode(Helper.UpdateTime, feedNSMgr).InnerText;
            var mapUpdatedTime = String.Empty;
            var mapUpdatedTimeNode = mapdoc.SelectSingleNode("//updated");
            if (mapUpdatedTimeNode != null)
            {
                mapUpdatedTime = mapUpdatedTimeNode.InnerText;
            }

            if (String.Compare(feedUpdateTime, mapUpdatedTime) != 0)
            {
                if (_useMapFileStream)
                {
                    Helper.GenerateMapFile(Properties.Settings.Default.LiveAppFeed, _mapFile);
                }
                else
                {
                    Helper.GenerateMapFile(Properties.Settings.Default.LiveAppFeed);
                }
                mapdoc = LoadMapFile();
            }

            var mappingList = mapdoc.DocumentElement.SelectNodes("//app");
            foreach (XmlNode appmap in mappingList)
            {
                var id = appmap.Attributes["appname"].Value.Trim();
                var submitter = appmap.Attributes["submitter"].Value.Trim();

                _appMapList.Add(id, submitter);
            }

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            _logWriter.WriteLine("Reading Apps in Queue  ...");
            fileStream.Seek(0, SeekOrigin.Begin);
            XmlWriter writer = XmlTextWriter.Create(fileStream, settings);
            Helper.WriteFileHeading(writer, Helper.Namespace);
            writer.WriteStartElement("resourcesList");
            writer.WriteEndElement();
            writer.WriteStartElement("languages");
            writer.WriteStartElement("language");
            writer.WriteAttributeString("default", "true");
            writer.WriteElementString("languageId", "en");
            writer.WriteElementString("name", "English");
            writer.WriteEndElement();
            writer.WriteEndElement();

            using (MSCOMDataContext db = new MSCOMDataContext(XmlGenCore.Properties.Settings.Default.MSCOMWebConnectionString))
            {
                successful = SyncAndGenerate(db, writer, states, _settings.TargetFeedIsAzure);
            }

            writer.WriteEndElement();
            writer.WriteEndDocument();
            writer.Flush();

            if (!successful)
            {
                return successful;
            }

            Dictionary<Helper.NodeType, XmlNode> docSections = Helper.NeededDocumentSections(feedoc, feedNSMgr);
            XmlDocument doc = Helper.LoadXmlDocFromFeed(fileStream);
            XmlElement root = doc.DocumentElement;            

            XmlNode resourcelistNode = doc.ImportNode(docSections[Helper.NodeType.ResourceList], true);
            XmlNode oldresourceList = root.SelectSingleNode("/atom:feed/atom:resourcesList", feedNSMgr);

            root.ReplaceChild(resourcelistNode, oldresourceList);
            root.AppendChild(doc.ImportNode(docSections[Helper.NodeType.KeyWords], true));
            root.AppendChild(doc.ImportNode(docSections[Helper.NodeType.OSes], true));
            root.AppendChild(doc.ImportNode(docSections[Helper.NodeType.OSList], true));
            root.AppendChild(doc.ImportNode(docSections[Helper.NodeType.Dependencies], true));
            root.AppendChild(doc.ImportNode(docSections[Helper.NodeType.Featured], true));

            Helper.SaveXmlDocWithFormatting(doc, fileStream);

            fileStream.Seek(0, SeekOrigin.Begin);
            string text = null;
            using (StreamReader sr = new ClosableStreamReader(fileStream))
            {
                text = sr.ReadToEnd();
            }


            //Remove all xmlns 
            text = text.Replace("xmlns=\"\"", "");

            fileStream.Seek(0, SeekOrigin.Begin);
            fileStream.SetLength(0);
            using (StreamWriter sw = new ClosableStreamWriter(fileStream))
            {
                sw.Write(text);
                sw.Flush();
            }

            return successful;
        }

        private bool SyncAndGenerate(MSCOMDataContext db, XmlWriter writer, string states, bool azureflag)
        {
            var allSubmissions = db.GetAllSubmissionsInBrief();

            _frameworksAndRuntimesMapping = db.GetAllFrameWorks();
            _databaseServersMapping = db.GetAllDatabaseServers();
            _webServerExtensionMappings = db.GetAllExtensions();
            _categoriesMapping = db.GetAllProductCategories();

            bool found = false;

            foreach (GetAllSubmissionsInBriefResult result in allSubmissions)
            {
                // simple string comparison to check for submission states
                switch (result.SubmissionState)
                {
                    case AppStates.ReadyToPublish:
                    case AppStates.Testing:
                        if (states == AppStates.All || states == result.SubmissionState)
                        {
                            found = true;
                            WriterProductEntry(db, writer, result, azureflag);
                            writer.Flush();
                        }
                        break;
                    default:
                        break;
                }
            }
            if (!found)
            {
                _logWriter.WriteLine("There are no apps in Queue");
            }

            return found;
        }

        private void WriterProductEntry(MSCOMDataContext db, XmlWriter writer, GetAllSubmissionsInBriefResult result, bool azureflag)
        {
            ISingleResult<GetSubmittedDataForProductOrAppResult> submissionDataAll = db.GetSubmittedDataForProductOrApp(result.SubmissionID);
            if (submissionDataAll != null)
            {
                List<string> seenApps = new List<string>();
                foreach (GetSubmittedDataForProductOrAppResult submissionData in submissionDataAll)
                {
                    _logWriter.WriteLine("Adding " + submissionData.Nickname);
                    _currentProducts.Add(_count++, submissionData.Nickname);

                    List<Package> packages = db.GetPackagesForID(submissionData.SubmissionID);
                    List<SubmissionLocalizedMetaData> MetaData = db.GetLocalizedMetaDataForID(submissionData.SubmissionID);
                    List<string> appNames = new List<string>(new string[] { submissionData.Nickname });

                    try
                    {
                        if (!_appMapList.ContainsKey(appNames[0]))
                        {
                            appNames[0] = _appMapList.FindKeyByValue(submissionData.SubmittingEntityURL);
                        }

                        string iisAppName = appNames[0] + "_IIS";
                        if (_appMapList.ContainsKey(iisAppName))
                        {
                            appNames.Add(iisAppName);
                        }
                    }
                    catch { }

                    foreach (string appName in appNames)
                    {
                        if (seenApps.Contains(appName))
                        {
                            continue;
                        }
                        else
                        {
                            seenApps.Add(appName);
                        }
                        writer.WriteComment(" " + submissionData.Nickname + " ");
                        writer.WriteStartElement("entry");
                        writer.WriteAttributeString("type", "application");
                        writer.WriteElementString("productId", appName);

                        ProductMetaDataInfo info = GetMetaDataInfo(writer, result, MetaData);

                        XmlUtil.WriteResourceElement(writer, "title", info.Title, "resourceName", info.TitleResourceName);

                        string atomID = "http://www.microsoft.com/web/webpi/2.0/" + submissionData.Nickname;

                        writer.WriteElementString("id", atomID);

                        XmlUtil.WriteResourceElement(writer, "summary", info.Summary, "resourceName", info.SummaryResourceName);

                        // date updated is now:
                        string DateUpdated = DateTime.UtcNow.ToString("s", new DateTimeFormatInfo()) + "Z";
                        writer.WriteElementString("updated", DateUpdated);

                        // date released is the ReleaseDate from Submission Data
                        // This was just doing a DateTime.Now and not looking at the submission data, so using the above value.
                        writer.WriteElementString("published", DateUpdated);

                        XmlUtil.WriteResourceElement(writer, "longSummary", info.LongSummary, "resourceName", info.LongSummaryResourceName);
                        XmlUtil.WriteIfNotNull(writer, "version", submissionData.Version);

                        WriteLink(writer, submissionData.SupportURL);
                        WriteProductMedia(writer, submissionData);
                        WriteProductKeywords(writer, submissionData);

                        // write author
                        writer.WriteStartElement("author");
                        writer.WriteElementString("name", submissionData.SubmittingEntity);
                        writer.WriteElementString("uri", GetUrlWithHttp(submissionData.SubmittingEntityURL));
                        writer.WriteEndElement();

                        List<string> childDependencies = GetDependencyAndRelatedProductSettings(submissionData);

                        WriteRelatedProducts(writer, submissionData);
                        WriteDependency(writer, submissionData, childDependencies, appName);
                        WriteProductInstallers(writer, submissionData, packages, azureflag);

                        // HARDCODED Values for newCategory and productFamily:
                        String add2feeddate = String.Format("{0:MM-dd-yyyy}", DateTime.UtcNow.Date);
                        writer.WriteElementString("addToFeedDate", add2feeddate);

                        writer.WriteElementString("pageName", submissionData.Nickname);

                        writer.WriteStartElement("productFamily");
                        writer.WriteAttributeString("resourceName", "Applications");
                        writer.WriteString("Applications");
                        writer.WriteEndElement();
                        
                        writer.WriteEndElement();
                    }
                }
            }
        }

        private void WriteRelatedProducts(XmlWriter writer, GetSubmittedDataForProductOrAppResult submissionData)
        {
            if (_relatedProducts.Count > 0)
            {
                writer.WriteStartElement("related");

                foreach (KeyValuePair<string, string> kv in _relatedProducts)
                {
                    writer.WriteStartElement("relatedProduct");

                    writer.WriteElementString("productId", kv.Key);
                    if (!String.IsNullOrEmpty(kv.Value) && !kv.Value.Equals("Default"))
                    {
                        writer.WriteElementString("relatedDiscoveryHint", kv.Value);
                    }
                    writer.WriteEndElement(); // relatedProduct
                }
                writer.WriteEndElement(); // related
            }
        }

        private void WriteDependency(XmlWriter writer, GetSubmittedDataForProductOrAppResult submissionData, List<string> childDependencies, string appName)
        {
            if (childDependencies.Count == 1 )
            {
                if (!childDependencies.Contains ("NodeJSApp")  && !childDependencies.Contains ("PythonApp"))
                Helper.WriteSingleDependency(writer, childDependencies[0]);
            } 
            else 
            {
                List<String> dependencylist = new List<string> (); 

                for  (int i =0; i < childDependencies.Count ; i++)
                {
                    string dependency = childDependencies[i];
                 
                    switch (dependency)
                    {
                        case "MySQLApp" :
                                if(!childDependencies.Contains("SQLApp"))
                                   dependencylist.Add ("MySQLApp");
                            
                            
                            break;                      
                        case "PHP53App":
                            if (childDependencies.Contains("SQLApp") && !childDependencies.Contains("MySQLApp"))
                            {
                                Helper.WriteSingleDependency(writer, "PHP53_SQL_App");
                            }
                            else if (childDependencies.Contains("SQLApp") && childDependencies.Contains("MySQLApp"))
                            {
                                Helper.WriteSingleDependency(writer, "PHP53_MySQL_SQL_App");
                            }                           
                           
                            else 
                                dependencylist.Add ("PHP53App");
                            break;
                      
                       case "PHP54App":
                            if (childDependencies.Contains("SQLApp") && !childDependencies.Contains("MySQLApp") )
                            {
                                Helper.WriteSingleDependency(writer, "PHP54_SQL_App");
                            }
                            else if (childDependencies.Contains("SQLApp") && childDependencies.Contains("MySQLApp"))
                            {
                                Helper.WriteSingleDependency(writer, "PHP54_MySQL_SQL_App");
                            }
                            else 
                                dependencylist.Add ("PHP54App");
                            break;
                        case "ASPNET4App":
                            if (childDependencies.Contains("SQLApp"))
                            {
                                if (appName.EndsWith("IIS"))
                                {
                                    Helper.WriteSingleDependency(writer, "SQL_ASPNET4App_IIS");
                                }
                                else
                                    Helper.WriteSingleDependency(writer, "SQL_ASPNET4App");
                            }                                                        
                            else
                            {
                                dependencylist.Add ("ASPNET4App");
                            }
                            break;
                        case "ASPNET45App":
                            if (childDependencies.Contains("SQLApp"))
                            {
                                if (appName.EndsWith("IIS"))
                                {

                                    dependencylist.Add("SQL_ASPNET4App_IIS");
                                }
                                else
                                    dependencylist.Add("SQL_ASPNET4App_IIS");

                             }
                            else
                            {
                                dependencylist.Add("ASPNET45App");
                                                         }
                            dependencylist.Add("ASPNET4App");
                            break;
                        case "ASPNET35App":
                            if (childDependencies.Contains("SQLApp"))
                            {
                                if (appName.EndsWith("IIS"))
                                {
                                    Helper.WriteSingleDependency(writer, "SQL_ASPNET35App_IIS");
                                }
                                else
                                    Helper.WriteSingleDependency(writer, "SQL_ASPNET35App");
                                

                            }                            
                            else
                                dependencylist.Add("ASPNET35App");
                            break;
                        case "NodeJSApp" : break;
                        case "PythonApp" : break;
                        case "SQLCEApp": break;
                        case "SQLDriverPHPApp": break;
                        case "SQLApp": break;

                        case "MVC4ASPNET45App":
                            if (childDependencies.Contains("SQLApp"))
                            {
                                dependencylist.Add("MVC4App");
                                dependencylist.Add("SQL_ASPNET45App");
                                dependencylist.Add("ASPNET4App");
                            }
                            else
                            {
                                dependencylist.Add("MVC4App");
                                dependencylist.Add("ASPNET45App");
                                dependencylist.Add("ASPNET4App");
                            }
                            break;
                        case "MVC4ASPNET4App":
                            if (childDependencies.Contains("SQLApp"))
                            {
                                dependencylist.Add("MVC4App");
                                dependencylist.Add("SQL_ASPNET4App");
                            }
                            else
                            {
                                dependencylist.Add("MVC4App");
                                dependencylist.Add("ASPNET4App");
                            }
                                
                            break;
                        case "MVC2ASPNET4App":
                            if (childDependencies.Contains("SQLApp"))
                            {
                                dependencylist.Add("MVC2App");
                                dependencylist.Add("SQL_ASPNET4App");
                            }
                            else
                            {
                                dependencylist.Add("MVC2App");
                                dependencylist.Add("ASPNET4App");
                            }

                            break;
                        
                         default:
                            dependencylist.Add(dependency);

                            break;

                     }
                   
                }
                if (dependencylist.Count > 1)
                {
                    Helper.WriteAndDependencies(writer, dependencylist.ToArray());
                }

               
            }
            

        }

        private List<string> GetDependencyAndRelatedProductSettings(GetSubmittedDataForProductOrAppResult submissionData)
        {
            _relatedProducts = new SortedDictionary<string, string>();
            List<string> childDependencies = new List<string>();
            string databasePlatform = null;

            // Get framework dependency
            if (submissionData.FrameworkOrRuntimeID.HasValue)
            {
                string dependencyName = _frameworksAndRuntimesMapping[submissionData.FrameworkOrRuntimeID.Value];

                // Add the related product to the feedDb
                // if the framework is PHP, we need to add WinCache as a related Product
                childDependencies.Add(dependencyName);
            }

            // Get DB dependency
            int databaseServerId = 0;

            if (!string.IsNullOrEmpty(submissionData.DatabaseServerIDs))
            {
                var ids = submissionData.DatabaseServerIDs.Split('|');

                foreach (string s in ids)
                {
                    if (!string.IsNullOrEmpty(s)
                        && int.TryParse(s, out databaseServerId))
                    {
                        string dependencyName = _databaseServersMapping[databaseServerId];
                        databasePlatform = dependencyName;

                        // Adds the related product to the feedDb
                        childDependencies.Add(dependencyName);
                    }
                }
            }

            // URL Rewriter
            int webServerExtensionID = 0;
            if (!string.IsNullOrEmpty(submissionData.WebServerExtensionIDs)
                && int.TryParse(submissionData.WebServerExtensionIDs, out webServerExtensionID))
            {
                string dependencyName = _webServerExtensionMappings[webServerExtensionID];
                // Not to be added twice
                if (!dependencyName.Contains("Rewrite"))
                {
                    childDependencies.Add(dependencyName);
                }
            }

            return childDependencies;
        }

        private void GenerateOsList(XmlWriter writer, string nickname)
        {
            writer.WriteStartElement("osList");
            string[] IIS7apps = XmlGenCore.Properties.Settings.Default.IIS7Apps.Split(',');
            if (IIS7apps.Contains(nickname))
            {
                writer.WriteStartElement("os");
                writer.WriteAttributeString("idref", "VistaSP1PlusNoHomeBasic");
                writer.WriteEndElement();
            }
            else
            {
                writer.WriteAttributeString("idref", "SupportedAppPlatforms");
            }
            writer.WriteEndElement();
        }

        private void GenerateInstallerFileElement(XmlWriter writer, Package p, Dictionary<string, string> hashed, string trackingURL)
        {
            PackageDownload shaVerifier = new PackageDownload();
            writer.WriteStartElement("installerFile");            
            writer.WriteElementString("fileSize", (p.FileSize / 1000).ToString());
            writer.WriteElementString("trackingURL", trackingURL);
            writer.WriteElementString("installerURL", p.PackageURL);

            if (!hashed.ContainsKey(p.PackageURL))
            {
                string computedSHA = p.SHA1Hash;                
                if (computedSHA != p.SHA1Hash)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.BackgroundColor = ConsoleColor.Black;
                    _logWriter.WriteLine("For package {0} the listed SHA1 is {1}, but was computed as {2}.", 
                        p.PackageURL, p.SHA1Hash, computedSHA);
                    Console.ResetColor();
                }

                hashed.Add(p.PackageURL, computedSHA);
            }
            writer.WriteElementString("sha1", hashed[p.PackageURL]);

            writer.WriteEndElement(); // installer file
        }

        private void GenerateMSDeployElement(XmlWriter writer, string StartPage)
        {
            writer.WriteStartElement("msDeploy");

            if (StartPage != null && !_knownStartPages.Contains(StartPage.ToLower().TrimStart('/')))
            {
                writer.WriteElementString("startPage", StartPage);
            }

            writer.WriteEndElement(); // msDeploy
        }

        private void GenerateInstallerElement(XmlWriter writer, string language, int counter)
        {
            writer.WriteStartElement("installer");
            writer.WriteElementString("id", counter.ToString());
            if (!_supportLanguages.Contains(language))
            {
                writer.WriteComment("Language " + language + " not supported . Hence changed to 'en' for testing purposes ");
                language = "en";
            }
            writer.WriteElementString("languageId", language);
        }

        private void WriteProductInstallers(XmlWriter writer, GetSubmittedDataForProductOrAppResult submissionData, List<Package> packages, bool azureflag)
        {
            List<string> usedlanguages = new List<string>();
            string appid = submissionData.Nickname;
            string TrackingURLPrefix = String.Format("http://www.microsoft.com/web/handlers/webpi.ashx?command=incrementappdownloadcount&appid={0}{1}&version={2}",
                appid, String.Empty, submissionData.Version);

            writer.WriteStartElement("installers");

            Dictionary<string, string> hashed = new Dictionary<string, string>(packages.Count);
            int counter = 0;
            foreach (Package p in packages)
            {
                if (p.SubmissionID == submissionData.SubmissionID && !usedlanguages.Contains(p.Language) && (p.FileSize != 0 || packages.Count == 1))
                {
                    usedlanguages.Add(p.Language);
                    string language = Helper.ConvertMachineLocaleToWebPILocale(p.Language);
                    counter++;

                    GenerateInstallerElement(writer, language, counter);
                    GenerateOsList(writer, submissionData.Nickname);
                    GenerateInstallerFileElement(writer, p, hashed, TrackingURLPrefix + "&applang=" + language);
                    GenerateMSDeployElement(writer, p.StartPage);

                    writer.WriteElementString("helpLink", GetUrlWithHttp(submissionData.SupportURL));
                    writer.WriteEndElement();
                }
            }

            writer.WriteEndElement();
        }

        private static string GetUrlWithHttp(string url)
        {
            return String.Format("{0}{1}", url.ToLower().StartsWith("http://") ? String.Empty : "http://", url);
        }

        private void AddCategories(XmlWriter writer, params string[] categories)
        {
            List<string> usedCategory = new List<string>();
            foreach (string category in categories)
            {
                int categoryId = 0;
                Int32.TryParse(category, out categoryId);

                if (categoryId > 0)
                {
                    string mappedCategory = _categoriesMapping[categoryId];
                    if (String.Compare(mappedCategory, "CMS", true) == 0)
                    {
                        mappedCategory = "ContentMgmt";
                    }
                    if (!usedCategory.Contains(mappedCategory))
                    {
                        writer.WriteElementString(KeywordId, mappedCategory);
                        usedCategory.Add(mappedCategory);
                    }
                }
            }
        }

        private void WriteProductKeywords(XmlWriter writer, GetSubmittedDataForProductOrAppResult submissionData)
        {
            List<string> usedCategory = new List<string>();

            writer.WriteStartElement(Keywords);

            string appname = submissionData.Nickname;

            /*String[] SQLCEapps = XmlGenCore.Properties.Settings.Default.SQLCEApps.ToLower().Trim().Split(',');
            if (SQLCEapps.Contains(submissionData.Nickname.ToLower().Trim()) 
                || (!String.IsNullOrEmpty(appname) && SQLCEapps.Contains(appname.ToLower().Trim())))
                writer.WriteElementString(KeywordId, "SQLCE"); */

            AddCategories(writer, submissionData.CategoryID1, submissionData.CategoryID2);

            if (submissionData.FrameworkOrRuntimeID.HasValue)
            {
                string framework = _frameworksAndRuntimesMapping[submissionData.FrameworkOrRuntimeID.Value];

                switch (framework)
                {
                    case "PHP52App":
                    case "PHP53App":
                        writer.WriteElementString(KeywordId, "PHP");
                        break;
                    default: break;
                }
            }
            if (!string.IsNullOrEmpty(submissionData.DatabaseServerIDs))
            {
                int databaseServerId;
                var ids = submissionData.DatabaseServerIDs.Split('|');

                foreach (string s in ids)
                {
                    if (!string.IsNullOrEmpty(s) && int.TryParse(s, out databaseServerId))
                    {
                        switch (_databaseServersMapping[databaseServerId])
                        {
                            case "MySQLApp":
                                writer.WriteElementString(KeywordId, "MySQL");
                                break;
                            case "SQLApp":
                                writer.WriteElementString(KeywordId, "SQL");
                                break;
                            case "SQLCEApp": 
                                writer.WriteElementString(KeywordId, "SQLCE");
                                break;
                            default: break;
                        }
                    }
                }
            }

            writer.WriteEndElement();
        }

        private void AddNullableGuidtElements(XmlWriter writer, string type, params Guid?[] elements)
        {
            foreach (Guid? element in elements)
            {
                if (element.HasValue)
                {
                    string url = ImageUrlPrefix + element.ToString();
                    writer.WriteElementString(type, url);
                }
            }
        }

        private void AddScreenShootElements(XmlWriter writer, params Guid?[] screenShotGuids)
        {
            AddNullableGuidtElements(writer, "screenshot", screenShotGuids);
        }

        private void AddLogoElements(XmlWriter writer, params Guid?[] logoGuids)
        {
            AddNullableGuidtElements(writer, "icon", logoGuids);
        }

        private void WriteProductMedia(XmlWriter writer, GetSubmittedDataForProductOrAppResult submissionData)
        {
            if (submissionData.LogoGUID.HasValue 
                || submissionData.ScreenshotGUID1.HasValue 
                || submissionData.ScreenshotGUID2.HasValue 
                || submissionData.ScreenshotGUID3.HasValue 
                || submissionData.ScreenshotGUID4.HasValue 
                || submissionData.ScreenshotGUID5.HasValue 
                || submissionData.ScreenshotGUID6.HasValue)
            {
                writer.WriteStartElement("images");
                AddLogoElements(writer, submissionData.LogoGUID);
                AddScreenShootElements(writer, submissionData.ScreenshotGUID1, submissionData.ScreenshotGUID2, submissionData.ScreenshotGUID3,
                    submissionData.ScreenshotGUID4, submissionData.ScreenshotGUID5, submissionData.ScreenshotGUID6);
                writer.WriteEndElement();
            }
        }

        private ProductMetaDataInfo GetMetaDataInfo(XmlWriter writer, GetAllSubmissionsInBriefResult result, List<SubmissionLocalizedMetaData> MetaData)
        {
            string productID = result.Nickname;
            ProductMetaDataInfo info = new ProductMetaDataInfo();

            foreach (SubmissionLocalizedMetaData entry in MetaData)
            {

                /*
                 *  In the MSComDB (external facing website backend database, 
                 *  the language naming conventions are as follows - 
                 *  en-us
                 *  ja-jp
                 *  ...
                 *  ...
                 *  zh-chs
                 *  zh-cht
                 * 
                 *  However, in WebPI XML files, the conventions are different.
                 *  WebPI consideres the first part of the language string as "culture"
                 *  e.g., for "en-us", the culture is "en". The resources are per culture.
                 * 
                 * The exceptions occur for zh-chs, and zh-cht-
                 * the corresponding culture in the WebPI XML are "zh-cn" and "zh-tw"
                 * 
                 * ConvertMachineLocaleToWebPILocale does this conversion
                 */
                string culture = Helper.ConvertMachineLocaleToWebPILocale(entry.Language);

                // English resources goes to the main feed file.
                if (string.Equals(culture, "en"))
                {
                    info.Title = entry.Name;
                    info.TitleResourceName = string.Format("Entry_{0}_Title", result.Nickname);
                    info.Summary = entry.BriefDescription;
                    info.SummaryResourceName = string.Format("Entry_{0}_Summary", result.Nickname);
                    info.LongSummary = entry.Description;
                    info.LongSummaryResourceName = string.Format("Entry_{0}_LongSummary", result.Nickname);

                    continue;
                }
            }

            return info;
        }

        private static void WriteLink(XmlWriter writer, string url)
        {
            if (!String.IsNullOrEmpty(url))
            {
                writer.WriteStartElement("link");
                writer.WriteAttributeString("href", GetUrlWithHttp(url));
                writer.WriteEndElement();
            }
        }
        
        public static bool IsResourceType(string type)
        {
            return (string.Compare(type, "-resources", true) == 0);
        }

        public static bool IsMapType(string type)
        {
            return (string.Compare(type, "-generatemap", true) == 0);
        }

        public static bool IsForAzure(string version)
        {
            return (String.Compare(version, "-azure", true) == 0);
        }

           
    }
}
