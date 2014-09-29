using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Microsoft.WindowsAzure.Management.Marketplace.Rest.WebAppGallery;

namespace WAGFeedValidator
{
    public class Validator
    {

        public WebAppGallerySynchronizer ValidateFeed(string officialWebPIFeedUrl, string feedUrl)
        {
            var synchronizer = new WebAppGallerySynchronizer();

            synchronizer.WebPIOfficialFeedUri = new Uri(officialWebPIFeedUrl);
            synchronizer.FeedUrl = string.IsNullOrEmpty(feedUrl) ? null : new Uri(feedUrl);
            synchronizer.CatalogConnectionString = String.Empty;

            synchronizer.Synchronize();
            return synchronizer;
        }


        private void ReportStatus(Collection<Error> errors, BlockingCollection<string> apps, BlockingCollection<string> keywords)
        {
            if (errors.Count == 0)
            {
                if (apps.Count == 0)
                {
                    Console.WriteLine("No applications were found. This typically indicates that no application have the AzureReady keyword or the given feed has an XML syntax error.");
                }
                else
                {
                    Console.WriteLine("Validation PASSED! The feed appears to be suitable for deployment.\n\n");
                    Console.WriteLine("Number of applications found: " + apps.Count);
                }
            }
            else
            {
                Console.WriteLine("Errors and / or Warnings were found. Fix errors and carefuly review warning before deploying.\n");
                Console.WriteLine("Errors and Warning:\n\n");

                foreach (Error error in errors)
                {
                    Console.WriteLine(error.Message);
                    Console.WriteLine("\n");
                }
            }

            if (apps.Count != 0)
            {
                Console.WriteLine("The following applications where found. If an application does not appear in this list, it will not be available in the portal.\n");
                foreach (string appName in apps.OrderBy(s => s))
                {
                    Console.WriteLine("\t" + appName);
                }
            }
            else
            {
                Console.WriteLine("No applications were found.");
            }

            if (keywords.Count != 0)
            {
                Console.WriteLine("\n\nThe following keywords where found. If a keyword does not appear in this list, it will not be shown in the portal.\n");
                foreach (string keyword in keywords.OrderBy(s => s))
                {
                    Console.WriteLine("\t" + keyword);
                }
            }
            else
            {
                Console.WriteLine("No keywords were found.");
            }

            if (Debugger.IsAttached)
            {
                Console.ReadLine();
            }
        }

        private void Usage()
        {
            Console.WriteLine("Validates a web application feed before deploying it to the portal.\n");
            Console.WriteLine("Usage:\n");
            Console.WriteLine("\tWAGFeedValidator <path_to_feed.xml>|<url to feed>\n");
            Console.WriteLine("Examples:\n");
            Console.WriteLine("\tWAGFeedValidator c:\\theFeedIWantToDeployToPortal.xml");
            Console.WriteLine("\tWAGFeedValidator http://go.microsoft.com/fwlink/?LinkID=233772");

            if (Debugger.IsAttached)
            {
                Console.ReadLine();
            }
        }
    }
}
