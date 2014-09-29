using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Text;
using System.IO;

namespace XmlGenCore
{
    public class ReourceGenerator
    {
        private class LocalizedRecord
        {
            public string Language { get; set; }
            public string Title { get; set; }
            public string Summary { get; set; }
            public string LongSummary { get; set; }
        }

        public static void CreateResourceFiles(Dictionary<string, MemoryStream> resourceFiles, string appId)
        {
            var resourceUris = GetResourceUris();
            var localizedLanguages = GetLocalizedLanguages();
            var localizedRecords = GetLocalizedRecords(appId);

            foreach (var language in localizedLanguages)
            {
                if (language == "en-us")
                {
                    continue;
                }

                var lang = Helper.ConvertMachineLocaleToWebPILocale(language);
                var mergedFile = new MemoryStream();
                LocalizedRecord localizedRecord;
                if (!localizedRecords.TryGetValue(lang, out localizedRecord))
                {
                    continue;
                }
                

                resourceFiles.Add(lang, mergedFile);

                string ruri;
                if (resourceUris.TryGetValue(lang, out ruri))
                {
                    var builtResourceSnipit = GetResourceSnipit(appId, localizedRecord);
                    MergeFeed.MergeResourceFeed(ruri, builtResourceSnipit, mergedFile);
                }
                else
                {
                    using (TextWriter writer = new ClosableStreamWriter(mergedFile))
                    {
                        writer.WriteLine("No resource file for the language '{0}' was found.", lang);
                    }
                }
            }
        }

        private static Dictionary<string, string>  GetResourceUris()
        {
            var doc = Helper.LoadXmlDocFromFeed(Properties.Settings.Default.LiveAppFeed);
            var namespaceManager = new XmlNamespaceManager(doc.NameTable);
            namespaceManager.AddNamespace("atom", Helper.Namespace);

            if (doc.DocumentElement == null)
            {
                throw new Exception("Live feed did not load properly.");
            }

            var resourceUrls = doc.DocumentElement.SelectNodes("/atom:feed/atom:resourcesList/atom:resources", namespaceManager);
            var resourceUris = new Dictionary<string, string>();

            if (resourceUrls == null)
            {
                throw  new Exception("No resource urls found.");
            }

            foreach (XmlNode resource in resourceUrls)
            {
                var culture = resource.ChildNodes[0].InnerText;
                var uri = resource.ChildNodes[1].InnerText;
                resourceUris.Add(culture, uri);
            }

            return resourceUris;
        }

        private static Dictionary<string, LocalizedRecord> GetLocalizedRecords(string appid)
        {
            var db = new MSCOMDataContext(Properties.Settings.Default.MSCOMWebConnectionString);
            const string queryFormat =
 @"select app.[Language], app.Name as Title, app.[Description] as LongSummary, app.[BriefDescription] as Summary 
from dbo.Submissions as sub
inner join dbo.SubmissionLocalizedMetaData as app
	on app.SubmissionId = sub.SubmissionId
inner join (select Max(sub2.submissionID) as submissionId
	from dbo.Submissions as sub2
	inner join dbo.SubmissionsStatus as subStat
		on subStat.SubmissionId = sub2.SubmissionId
	where sub2.NickName like '{0}' and (SubStat.SubmissionStateID=7 or SubStat.SubmissionStateID=6)) as sub2
	on sub2.submissionID = sub.submissionId";

            var query = String.Format(queryFormat, appid);
            var localizedQueryRecords = db.ExecuteQuery<LocalizedRecord>(query);

            return localizedQueryRecords.ToDictionary(localizedQueryRecord => Helper.ConvertMachineLocaleToWebPILocale(localizedQueryRecord.Language));
        }

        private static IEnumerable<string> GetLocalizedLanguages()
        {
            var db = new MSCOMDataContext(Properties.Settings.Default.MSCOMWebConnectionString);

            var localizedLanguages = (from app in db.SubmissionLocalizedMetaDatas
                                      join p in db.Packages
                                          on app.SubmissionID equals p.SubmissionID
                                      where app.Language == p.Language
                                      select app.Language).Distinct();

            if (localizedLanguages == null)
            {
                throw new Exception("No data could be found ");
            }

            return localizedLanguages;
        }

        private static MemoryStream GetResourceSnipit(string appId, LocalizedRecord data)
        {
            var doc = Helper.LoadXmlDocFromFeed(Properties.Settings.Default.LiveAppFeed);
            doc.DocumentElement.RemoveAll();
            const string writeFormat = "<data xml:space=\"preserve\" name=\"Entry_{0}_{1}\"><value>{2}</value></data>";

            var sb = new StringBuilder();
            sb.AppendFormat(writeFormat, appId, "Title", data.Title);
            sb.AppendFormat(writeFormat, appId, "LongSummary", data.LongSummary);
            sb.AppendFormat(writeFormat, appId, "Summary", data.Summary);
            var root = doc.CreateElement("root");
            root.InnerXml = sb.ToString();
            doc.DocumentElement.AppendChild(root);

            var file = new MemoryStream();
            using (var stringWriter = new StringWriter())
            using (var xmlTextWriter = new XmlTextWriter(stringWriter))
            using (var writer = new ClosableStreamWriter(file))
            {
                doc.WriteTo(xmlTextWriter);

                var contents = stringWriter.ToString();
                writer.Write(contents);
            }

            return file;
        }
    }
}

