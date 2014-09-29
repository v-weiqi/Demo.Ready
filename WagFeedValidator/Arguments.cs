using System;
using System.IO;
using System.Net;
using System.Globalization;

namespace WAGFeedValidator
{
    internal class Arguments
    {
        private static string DefaultWebPIOfficialFeedUrl = "http://go.microsoft.com/?linkid=9823756";

        public string WebAppFeedUrl { get; private set; }
        public string WebPIOfficialFeedUrl { get; private set; }

        public Arguments()
        {
            // Default official web pi feed
            WebPIOfficialFeedUrl = DefaultWebPIOfficialFeedUrl;
        }

        public bool Parse(string[] args)
        {
            if (args.Length == 1)
            {
                string candidateFeedUrl = args[0];
                if (!String.IsNullOrEmpty(candidateFeedUrl))
                {
                    string actualFeedLocation = SniffFeedLocation(candidateFeedUrl);
                    if (!String.IsNullOrEmpty(actualFeedLocation))
                    {
                        WebAppFeedUrl = actualFeedLocation;
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            return false;
        }

        private string SniffFeedLocation(string candidateFeedUrl)
        {
            if (candidateFeedUrl.StartsWith("http"))
            {
                // Appears to be a url - Download it and create a local temp file
                string feedTempFile = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
                try
                {
                    Console.WriteLine(String.Format(CultureInfo.CurrentCulture, "Given feed '{0}' appears to be a url. Downloading to temporary file '{1}'", candidateFeedUrl, feedTempFile));
                    DownloadFeed(candidateFeedUrl, feedTempFile);
                    Console.WriteLine("Download complete.");
                    return "file://" +  feedTempFile;
                }
                catch(Exception)
                {
                    Console.Write("'{0}' does not appear to be a valid url and/or does not seem to point to a resource.", candidateFeedUrl);
                    return null;
                }
            }
            else
            {
                // Appears to be a file
                if (File.Exists(candidateFeedUrl))
                {
                    return "file://" + Path.GetFullPath(candidateFeedUrl);
                }
                else
                {
                    Console.Write("'{0}' does not appear to be a valid file path.", candidateFeedUrl);
                    return null;
                }
            }
        }

        private void DownloadFeed(string candidateFeedUrl, string feedTempFile)
        {
            Uri feedUrl = new Uri(candidateFeedUrl);

            HttpWebRequest webRequest = HttpWebRequest.Create(feedUrl) as HttpWebRequest;
            webRequest.Method = "GET";

            // Get the response
            using (HttpWebResponse webResponse = webRequest.GetResponse() as HttpWebResponse)
            {
                switch (webResponse.StatusCode)
                {
                    case HttpStatusCode.OK:
                        using (Stream responseStream = webResponse.GetResponseStream())
                        {
                            using (FileStream fileStream = new FileStream(feedTempFile, FileMode.OpenOrCreate, FileAccess.Write))
                            {
                                responseStream.CopyTo(fileStream);
                            }
                        }
                        break;

                    default:
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                                                                          "HttpWebRequest.GetResponse() returned '{0}' - Considering this as an error.",
                                                                          webResponse.StatusCode));
                }
            }
        }
    }
}
