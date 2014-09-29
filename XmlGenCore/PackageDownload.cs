using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Security.Cryptography;

namespace XmlGenCore
{
    public class PackageDownload
    {
        internal class DownloadClient : WebClient
        {
            private string _referer;
            private string _userAgent;

            public DownloadClient()
            {
                _referer = "wpi://2.1.0.0/" + Environment.OSVersion.VersionString;
                _userAgent = "Platform-Installer/3.0.0.0(" + Environment.OSVersion.VersionString + ")";
            }

            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                HttpWebRequest httpRequest = request as HttpWebRequest;
                if (httpRequest != null)
                {
                    httpRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    httpRequest.Timeout = 30000;
                    httpRequest.UserAgent = _userAgent;
                    httpRequest.Referer = _referer;
                    httpRequest.AllowAutoRedirect = true;
                }

                return request;
            }
        }

        public string GetSHA1(string url)
        {
            LogClass logger = LogClass.Logger();
            string stringHashValue = null;
            string tempFile = String.Empty;
            try
            {
                tempFile = Path.GetTempFileName();
                logger.WriteLine("Downloading {0} to {1} for SHA1 computation.", url, tempFile);
                using (DownloadClient client = new DownloadClient())
                {
                        client.DownloadFile(url, tempFile);
                }

                SHA1CryptoServiceProvider sha1Hasher = new SHA1CryptoServiceProvider();
                byte[] hashValue = null;
                using (FileStream stream = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    hashValue = sha1Hasher.ComputeHash(stream);
                }
                stringHashValue = BitConverter.ToString(hashValue).Replace("-", "").ToLower();
            }
            catch (Exception ex)
            {
                logger.WriteLine("Failed to download file at '{0}'{1}Because of:{2}", url, Environment.NewLine, ex.ToString());
            }
            finally
            {
                if (!String.IsNullOrEmpty(tempFile) && File.Exists(tempFile))
                {
                    try
                    {
                        File.Delete(tempFile);
                        logger.WriteLine("Deleted file {0}.", tempFile);
                    }
                    catch
                    {
                        logger.WriteLine("Failed to delete file {0}.", tempFile);
                    }
                }
            }

            return stringHashValue;
        }
    }
}
