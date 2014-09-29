using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XmlGenCore
{
    public partial class MSCOMDataContext : System.Data.Linq.DataContext
    {
        public List<SubmissionLocalizedMetaData> GetLocalizedMetaDataForID(int submissionID)
        {
            List<SubmissionLocalizedMetaData> result = (from s in SubmissionLocalizedMetaDatas
                                                        where s.SubmissionID == submissionID
                                                        select s).ToList();

            return result;
        }

        public List<SubmissionLocalizedMetaData> GetLocalizedMetaDataForID(int submissionID, string lang)
        {
            List<SubmissionLocalizedMetaData> result = (from s in SubmissionLocalizedMetaDatas
                                                        where s.SubmissionID == submissionID && (String.Compare (s.Language,lang ,true )==0 )
                                                        select s).ToList();

            return result;
        }


        public List<Package> GetPackagesForID(int submissionID)
        {
            List<Package> result = (from p in Packages
                                    where p.SubmissionID == submissionID 
                                    select p).ToList();

            return result;
        }

        public Dictionary<int, string> GetAllFrameWorks()
        {
            List<FrameworksAndRuntime> result = (from f in FrameworksAndRuntimes
                                                 select f).ToList();

            Dictionary<int, string> dictionary = new Dictionary<int, string>();

            foreach (FrameworksAndRuntime fr in result)
            {
                dictionary.Add(fr.FrameworkOrRuntimeID, fr.FeedIDRef);
            }

            return dictionary;
        }

        public Dictionary<int, string> GetAllDatabaseServers()
        {
            List<DatabaseServer> result = (from d in DatabaseServers
                                           select d).ToList();


            Dictionary<int, string> dictionary = new Dictionary<int, string>();

            foreach (DatabaseServer ds in result)
            {
                dictionary.Add(ds.DatabaseServerID, ds.FeedIDRef);
            }

            return dictionary;
        }

        public Dictionary<int, string> GetAllExtensions()
        {
            List<WebServerExtension> result = (from e in WebServerExtensions
                                               select e).ToList();

            Dictionary<int, string> dictionary = new Dictionary<int, string>();

            foreach (WebServerExtension we in result)
            {
                dictionary.Add(we.WebServerExtensionID, we.FeedIDRef);
            }

            return dictionary;
        }

        public Dictionary<int, string> GetAllProductCategories()
        {

            List<ProductOrAppCategory> result = (from p in ProductOrAppCategories
                                                 select p).ToList();

            Dictionary<int, string> dictionary = new Dictionary<int, string>();


            foreach (ProductOrAppCategory category in result)
            {
                dictionary.Add(category.CategoryID, category.Name);
            }

            return dictionary;
        }
    }
}
