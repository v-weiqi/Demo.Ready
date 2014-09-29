using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace XmlGenCore
{
    public static class DownloadHelper
    {
        /// <summary>
        /// This is the filename of a 'tag' file that we use to record the name of the
        /// installer in some cases. 
        /// </summary>
        /// <remarks>
        /// When an installer is a bootstrapper (offline action), it won't be the only file in the directory
        /// so we don't immediately know which file is the installer. We use a text file in the directory
        /// with this name to denote which file is the installer -- this avoids the need to hit
        /// the server to find the filename.
        /// </remarks>
        internal const string TagFilename = "_InstallerFilename_.txt";

        private const int MaxRetryLimit = 10;
        private const int DownloadBufferSize = 32768; // 32k

        public static string DownloadFile(Uri uri, string targetDirectory)
        {
            return DownloadFile(uri, false, targetDirectory);
        }

        /// <summary>
        /// Attempts to see if a file has already been downloaded
        /// </summary>
        /// <param name="targetRoot"></param>
        /// <param name="sha"></param>
        /// <param name="?"></param>
        /// <returns></returns>
        internal static bool TryGetFilePath(string targetRoot, string sha, out string filepath)
        {
            string destination = Path.Combine(targetRoot, sha);
            if (Directory.Exists(destination))
            {
                // we don't actually check that any files matching the sha1 value exist
                // this is safe as an optimization because we don't actually create the 
                // target directory unless the download was successful
                string[] files = Directory.GetFiles(destination);
                string tagFilePath = Path.Combine(destination, TagFilename);
                if (files.Length == 1)
                {
                    filepath = files[0];
                    return true;
                }
                else if (File.Exists(tagFilePath))
                {
                    // if our'tag' file exists, it will identify the name of the installer
                    string installerFile = Path.Combine(destination, File.ReadAllText(tagFilePath));
                    if (File.Exists(installerFile))
                    {
                        filepath = installerFile;
                        return true;
                    }
                }
            }

            filepath = null;
            return false;
        }

        /// <summary>
        /// Creates a 'tag' file that contains the filename of the installer
        /// </summary>
        /// <param name="installerFullPath">The full path of the installer</param>
        internal static void WriteTagFile(string installerFullPath)
        {
            string installerFilename = Path.GetFileName(installerFullPath);
            string tagFileName = Path.Combine(Path.GetDirectoryName(installerFullPath), TagFilename);

            File.WriteAllText(tagFileName, installerFilename);
        }

        internal static string DownloadFile(Uri uri, bool useSha1Value, string targetDirectory)
        {            
            try
            {
                string downloadedFilePath = DownloadFilePrivate(uri, useSha1Value, targetDirectory);
                if (!String.IsNullOrEmpty(downloadedFilePath))
                {                    
                    return downloadedFilePath;
                }
            }
            catch (Exception e)
            {                                
                throw new WebException(e.ToString());
            }

            return null;
        }

        private static string DownloadFilePrivate(Uri uri, bool useSha1Value, string targetDirectory)
        {
            string downloadedFilePath = null;
            
            if (DownloadFileWinInet(uri, targetDirectory, useSha1Value, out downloadedFilePath))
            {
                return downloadedFilePath;
            }

            if (DownloadFileWebClient(uri, targetDirectory, useSha1Value, out downloadedFilePath))
            {
                return downloadedFilePath;
            }

            return downloadedFilePath;
        }

        
        private static bool DownloadFileWinInet(Uri uri, string targetDirectory, bool useSha1Value, out string downloadedFilePath)
        {
            bool done = false;
            string localTemp = Path.GetTempFileName();
            downloadedFilePath = string.Empty;

            using (ManagedWinInet winInet = new ManagedWinInet(""))
            {
                winInet.InternetOpen();
                try
                {
                    string contentDispositionFileName;

                    winInet.DownloadFile(uri, localTemp, out contentDispositionFileName);
                    if (useSha1Value)
                    {
                        targetDirectory = Path.Combine(targetDirectory, GetSHA1Hash(localTemp));
                    }

                    if (!winInet.CancelDownload)
                    {
                        Debug.Assert(File.Exists(localTemp), "file does not exist: " + localTemp);

                        // look for content disposition name first
                        if (!string.IsNullOrEmpty(contentDispositionFileName))
                        {
                            downloadedFilePath = Path.Combine(targetDirectory, contentDispositionFileName);
                        }
                        else
                        {
                            string respondingUrl;

                            if (winInet.RespondingUri != null)
                            {
                                respondingUrl = winInet.RespondingUri.AbsoluteUri;
                            }
                            else
                            {
                                respondingUrl = uri.AbsoluteUri;
                            }

                            string[] splitInstallLocation = respondingUrl.Split('/');
                            if (splitInstallLocation.Length < 0)
                            {
                                // don't think this can happen
                                throw new InvalidOperationException();
                            }

                            string fileName = splitInstallLocation[splitInstallLocation.Length - 1];

                            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) > 0)
                            {
                                fileName = Path.GetFileName(localTemp);
                            }

                            downloadedFilePath = Path.Combine(targetDirectory, fileName);
                        }

                        if (!Directory.Exists(targetDirectory))
                        {
                            Directory.CreateDirectory(targetDirectory);
                        }

                        File.Copy(localTemp, downloadedFilePath, true);

                        done = true;
                    }
                }
                catch
                {                    
                }
                finally
                {
                    if (File.Exists(localTemp))
                    {
                        File.Delete(localTemp);
                    }

                    winInet.InternetClose();
                }
            }

            return done;
        }

        public static string GetContentDispositionHeaderWininet(Uri uri)
        {
            using (ManagedWinInet winInet = new ManagedWinInet(""))
            {                
                winInet.InternetOpen();
                return winInet.GetContentDispositionHeader(uri);
            }

        }


        public static string GetContentDispositionHeader(Uri uri)
        {
            try
            {
                using (WebClient webClient = new WebClient())
                {                    
                    webClient.OpenRead(uri);
                    return GetContentDispositionFileName(webClient);                  
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool DownloadFileWebClient(Uri uri, string targetDirectory, bool useSha1Value, out string downloadedFilePath)
        {
            bool worked = false;

            Stream resStream = null;
            FileStream fs = null;
            string localTemp = Path.GetTempFileName();
            downloadedFilePath = String.Empty;
            string fileName = null;

            try
            {
                using (WebClient webClient = new WebClient())
                {
                    fs = new FileStream(localTemp, FileMode.Create);
                    resStream = webClient.OpenRead(uri);

                    byte[] readBuf = new byte[DownloadBufferSize];
                    int bytesRead;
                    for (int i = 0;
                        ((bytesRead = resStream.Read(readBuf, 0, DownloadBufferSize)) > 0);
                        i++)
                    {
                        fs.Write(readBuf, 0, bytesRead);
                    }

                    if (!(bytesRead < 0))
                    {
                        fileName = GetContentDispositionFileName(webClient);

                        worked = true;
                    }
                }
            }
            catch (Exception e)
            {                
            }
            finally
            {
                if (fs != null)
                {
                    fs.Close();
                }

                if (resStream != null)
                {
                    resStream.Close();
                }
            }

            try
            {
                if (worked)
                {
                    Debug.Assert(File.Exists(localTemp), "file does not exist: " + localTemp);

                    if (string.IsNullOrEmpty(fileName))
                    {
                        fileName = GetResponseUriFileName(uri, fileName);
                    }

                    if (String.IsNullOrEmpty(fileName))
                    {
                        fileName = GetAbsoluteUriFileName(uri, fileName);
                    }

                    if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) > 0)
                    {
                        fileName = Path.GetFileName(localTemp);
                    }

                    if (useSha1Value)
                    {
                        targetDirectory = Path.Combine(targetDirectory, GetSHA1Hash(localTemp));
                    }

                    downloadedFilePath = Path.Combine(targetDirectory, fileName);

                    if (!Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }
                    
                    File.Copy(localTemp, downloadedFilePath, true);
                }
            }
            finally
            {
                if (File.Exists(localTemp))
                {
                    File.Delete(localTemp);
                }
            }

            return worked;
        }

        private static string GetAbsoluteUriFileName(Uri uri, string fileName)
        {
            string respondingUrl = uri.AbsoluteUri;

            string[] splitInstallLocation = respondingUrl.Split('/');
            if (splitInstallLocation.Length < 0)
            {             
                throw new InvalidOperationException();
            }

            fileName = splitInstallLocation[splitInstallLocation.Length - 1];
            return fileName;
        }

        private static string GetResponseUriFileName(Uri uri, string fileName)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
                request.Method = "GET";
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (response != null &&
                    response.ResponseUri != null &&
                    !string.IsNullOrEmpty(response.ResponseUri.PathAndQuery))
                {
                    string filePath = response.ResponseUri.PathAndQuery;
                    int idx = filePath.LastIndexOf('/');
                    if (idx > 0 &&
                        idx < filePath.Length)
                    {
                        fileName = filePath.Substring(idx + 1);
                    }
                }
            }
            catch (Exception e)
            {                
            }

            return fileName;
        }

        private static string GetContentDispositionFileName(WebClient webClient)
        {
            string fileName = null;

            if (webClient.ResponseHeaders["Content-Disposition"] != null)
            {
                string contentDispositionString = webClient.ResponseHeaders["Content-Disposition"];
                
                int fileNameStart = contentDispositionString.IndexOf("filename=", StringComparison.OrdinalIgnoreCase);
                if (fileNameStart > -1 && (contentDispositionString.Length > (fileNameStart + 9)))
                {
                    // get rid of "filename="
                    fileNameStart += 9;

                    // check for end
                    int nextSemiColon = contentDispositionString.IndexOf(';', fileNameStart);
                    int fileNameLength;
                    if (nextSemiColon > -1)
                    {
                        fileNameLength = nextSemiColon - fileNameStart;
                    }
                    else
                    {
                        fileNameLength = contentDispositionString.Length - fileNameStart;
                    }

                    // strip quotes if present
                    fileName = contentDispositionString.Substring(fileNameStart, fileNameLength).Trim('"');
                }
            }

            return fileName;
        }

        [SuppressMessage("Microsoft.Cryptographic.Standard", "CA5354:SHA1CannotBeUsed", Justification = "We have an exception to use SHA1 as a checksum for webpi")]
        private static string GetSHA1Hash(string fileLocation)
        {
            StringBuilder sb = new StringBuilder();
            using (FileStream fileStream = File.OpenRead(fileLocation))
            {
                using (SHA1 sha1 = SHA1.Create())
                {
                    byte[] result = sha1.ComputeHash(fileStream);

                    for (int i = 0; i < result.Length; i++)
                    {
                        sb.Append(result[i].ToString("X2", CultureInfo.InvariantCulture));
                    }
                }
            }

            Debug.Assert(!String.IsNullOrEmpty(sb.ToString()), "Empty SHA hash");

            // And return it
            return sb.ToString();
        }

        /// <summary>
        /// Determines the file name for the uri when downloaded.
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        internal static string ResolveFileName(Uri uri)
        {            
            try
            {
                string filepath = DownloadFilePrivate(uri, false, Path.GetTempPath());
                if (filepath != null)
                {
                    File.Delete(filepath);
                    return Path.GetFileName(filepath);
                }
            }
            catch (Exception e)
            {
                throw new WebException(e.ToString());
            }

            return null;
        }
    }
}