using System;
using System.Net;
using System.IO;
using LogEventType = AppGallery.SIR.ILog.LogEvent.LogEventType;

namespace AppGallery.SIR
{
    public class WPIWebClient : WebClient
    {
        protected string UserAgent = "Platform-Installer/3.0.0.0(" + Environment.OSVersion.VersionString + ")";
        protected static int _statusCode;
        protected static int _timeout = 20000;
        protected PackageValidationManager _packageValidationManager;

        public WPIWebClient(PackageValidationManager packageValidationManager)
        {
            _packageValidationManager = packageValidationManager;
        }

        public static int StatusCode
        {
            get { return _statusCode; }
            set { _statusCode = value; }
        }

        public static int Timeout
        {
            get { return _timeout; }
            set { _timeout = value; }
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            HttpWebRequest request = (HttpWebRequest)base.GetWebRequest(address);
            request.Timeout = _timeout;
            request.UserAgent = this.UserAgent;
            return request;
        }

        public void SendRequest(string requestUrl, int currentRetry)
        {
            try
            {
                // Create a request for the URL 
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUrl);
                // If required by the server, set the credentials
                request.Credentials = CredentialCache.DefaultCredentials;
                StatusCode = (int)((HttpWebResponse)request.GetResponse()).StatusCode;
            }
            catch (WebException e)
            {
                StatusCode = (int)((HttpWebResponse)e.Response).StatusCode;
            }
            catch (Exception e)
            {
                if (currentRetry == 1)
                {
                    _packageValidationManager.Reporter.Log(e);
                }
            }
        }

        public void SendRetryRequest(string appUrl, int retries)
        {
            for (int retry = 1; retry < retries; retry++)
            {
                SendRequest(appUrl, retry);
                if (StatusCode >= 200 && StatusCode <= 399)
                {
                    return;
                }                
            }
        }
    }
}
