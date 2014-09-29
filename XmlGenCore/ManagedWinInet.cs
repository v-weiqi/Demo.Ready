//------------------------------------------------------------------------------
// <copyright file="ManagedWinInet.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
#pragma warning disable 1634, 1691
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace XmlGenCore
{
    public class ManagedWinInet : IDisposable
    {
        private static int _bufferSize = 32768;

        private static string _userAgent;
        private static string _referer;

        private DateTime _ifModifiedSince;
        private EventHandler<BytesReadEventArgs> _bytesRead;
        private Uri _respondingUri;
        private SafeInetHandle _hInet;
        private bool _cancelDownload;
        private string _errorMessage;
        private bool _disposed;

        internal ManagedWinInet(string errorMessage)
        {
            _ifModifiedSince = DateTime.MinValue;
            _errorMessage = errorMessage;
        }

        internal event EventHandler<BytesReadEventArgs> BytesRead
        {
            add
            {
                _bytesRead = (EventHandler<BytesReadEventArgs>)Delegate.Combine(_bytesRead, value);
            }

            remove
            {
                _bytesRead = (EventHandler<BytesReadEventArgs>)Delegate.Remove(_bytesRead, value);
            }
        }

        internal bool CancelDownload
        {
            [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Shared code. Used in framework")]
            get
            {
                return _cancelDownload;
            }

            [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Shared code. Used in framework")]
            set
            {
                _cancelDownload = value;
            }
        }

        internal DateTime IfModifiedSince
        {
            get
            {
                return _ifModifiedSince;
            }

            [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Used in framework")]
            set
            {
                _ifModifiedSince = value;
            }
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Shared code. Used in framework")]
        internal Uri RespondingUri
        {
            get
            {
                return _respondingUri;
            }
        }

        internal void InternetClose()
        {
            Debug.Assert(_hInet != null && !_hInet.IsInvalid, "_hInet != null && !_hInet.IsInvalid");
            _hInet.Dispose();
            _hInet.SetHandleAsInvalid();
        }

        internal void InternetOpen()
        {
            Debug.Assert(_hInet == null, "_hInet must be null");

            // we do call GetLastWin32Error in ThrowGetLastErrorException
            #pragma warning suppress 56523
            _hInet = NativeMethods.InternetOpen(UserAgent, NativeMethods.INTERNET_OPEN_TYPE_PRECONFIG, null, null, 0);
            if (_hInet.IsInvalid)
            {
                ThrowGetLastErrorException("InternetOpen");
            }
        }

        public string GetContentDispositionHeader(Uri uri)
        {
            bool downloadFile = true;
            string contentDispositionFileName = null;
            IntPtr hInetFile = IntPtr.Zero;

            downloadFile = OpenUrlAndFollowRedirects(ref uri, ref hInetFile);

            if (downloadFile)
            {
                byte[] buffer = new byte[_bufferSize];

                // get total file size
                int responseSize = GetContentLength(hInetFile, buffer);

                // check for content-disposition and pull out file name if found
                contentDispositionFileName = GetContentDispositionFileName(contentDispositionFileName, hInetFile, buffer);

                return contentDispositionFileName;
            }
            else
            {
                return null;
            }
        }

        // will return false if a "if-modified-since" header is specified and the
        // file has not been modified
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "TODO")]
        internal bool DownloadFile(Uri uri, string fileName, out string contentDispositionFileName)
        {
            bool downloadFile = true;
            contentDispositionFileName = null;

            if (uri.IsFile)
            {
                _respondingUri = uri;

                // Satisfy local file system requests with a File.Copy
                File.Copy(uri.LocalPath, fileName, true);

                FileAttributes copiedFileAttributes = File.GetAttributes(fileName);
                if ((copiedFileAttributes & FileAttributes.ReadOnly).Equals(
                    FileAttributes.ReadOnly))
                {
                    // remove read only flag
                    File.SetAttributes(fileName, copiedFileAttributes & ~FileAttributes.ReadOnly);
                }
            }
            else
            {
                if (_hInet.IsInvalid)
                {
                    throw new InvalidOperationException("InternetOpen has not been called yet");
                }

                if (uri.IsFile)
                {
                    throw new ArgumentException("Uri is a file location");
                }

                string fileDirectory = Path.GetDirectoryName(fileName);
                if (!Directory.Exists(fileDirectory))
                {
                    throw new ArgumentException("Directory '" + fileDirectory + "' does not exist");
                }

                IntPtr hInetFile = IntPtr.Zero;
                try
                {
                    downloadFile = OpenUrlAndFollowRedirects(ref uri, ref hInetFile);

                    if (downloadFile)
                    {
                        // create the file
                        bool readSuccess = true;
                        int bytesRead;
                        byte[] buffer = new byte[_bufferSize];

                        // get total file size
                        int responseSize = GetContentLength(hInetFile, buffer);

                        // check for content-disposition and pull out file name if found
                        contentDispositionFileName = GetContentDispositionFileName(contentDispositionFileName, hInetFile, buffer);

                        using (FileStream stream = File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            // we do call GetLastWin32Error in ThrowGetLastErrorException
#pragma warning suppress 56523
                            while (readSuccess = NativeMethods.InternetReadFile(hInetFile, buffer, _bufferSize, out bytesRead) &&
                                    !_cancelDownload)
                            {
                                if (bytesRead == 0)
                                {
                                    // done
                                    break;
                                }

                                stream.Write(buffer, 0, bytesRead);

                                // send event
                                SendBytesReadEvent(bytesRead, responseSize);
                            }
                        }

                        if (!_cancelDownload && !readSuccess)
                        {
                            ThrowGetLastErrorException("InternetReadFile");
                        }
                    }
                }
                finally
                {
                    if (hInetFile != IntPtr.Zero)
                    {
                        if (!NativeMethods.InternetCloseHandle(hInetFile))
                        {
                            // what do we do with this value?
                            Marshal.GetLastWin32Error();
                        }

                        hInetFile = IntPtr.Zero;
                    }
                }
            }

            return downloadFile;
        }

        private bool OpenUrlAndFollowRedirects(ref Uri uri, ref IntPtr hInetFile)
        {
            bool downloadFile = true;

            // handle all the redirection ourself so we can get the responding URI location
            bool redirect;
            byte[] buffer = new byte[_bufferSize];

            // start with wpi:// referer but update as we get redirected
            string referer = Referer;

            int i = 0;
            do
            {
                redirect = false;

                // construct headers each time so we can update referrer 
                string headers = "Referer: " + referer;

                if (IfModifiedSince > DateTime.MinValue)
                {
                    headers += "\nIf-Modified-Since: " + IfModifiedSince.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
                }

                // verbose logging
//                Logging.LogMessage(TraceEventType.Verbose, "Connecting to {0} with (partial) headers:\r\n{1}\r\nUser-Agent:{2}\r\n", uri.AbsoluteUri, headers.Replace("\n", "\r\n"), UserAgent);

                // we do call GetLastWin32Error in ThrowGetLastErrorException
#pragma warning suppress 56523
                hInetFile = NativeMethods.InternetOpenUrl(_hInet, uri.AbsoluteUri, headers, headers.Length, NativeMethods.INTERNET_FLAG_NO_AUTO_REDIRECT, IntPtr.Zero);
                if (hInetFile == IntPtr.Zero)
                {
                    ThrowGetLastErrorException("InternetOpenUrl");
                }

                _respondingUri = uri;

                int bufferLength = _bufferSize;
                int index = 0;

                // we do call GetLastWin32Error in ThrowGetLastErrorException
#pragma warning suppress 56523
                if (!NativeMethods.HttpQueryInfo(hInetFile, NativeMethods.HTTP_QUERY_STATUS_CODE, buffer, out bufferLength, out index))
                {
                    ThrowGetLastErrorException("HttpQueryInfo");
                }

                // really we got back some text so convert to string
                string responseCode = Encoding.Unicode.GetString(buffer, 0, (int)bufferLength);

                if (!string.Equals(responseCode, "200", StringComparison.Ordinal))
                {
         //           Logging.LogMessage(TraceEventType.Information, uri + " responded with " + responseCode);
         //           Logging.LogMessage(TraceEventType.Information, "Response headers:\r\n{0}", GetResponseHeaders(hInetFile));
                }
                else
                {
          //          Logging.LogMessage(TraceEventType.Verbose, uri + " responded with " + responseCode);
           //         Logging.LogMessage(TraceEventType.Verbose, "Response header:\r\n{0}", GetResponseHeaders(hInetFile));
                }

                // expect 200
                if (!String.Equals(responseCode, "200", StringComparison.Ordinal))
                {
                    // allow 200 + something else just in case...
                    if (responseCode[0] == '2' &&
                        responseCode[1] == '0' &&
                        responseCode[2] == '0')
                    {
                        Debug.Fail("Response code was: " + responseCode);
                    }
                    else
                    {
                        // check for redirect
                        if (String.Equals(responseCode, "301", StringComparison.Ordinal) ||
                            String.Equals(responseCode, "302", StringComparison.Ordinal))
                        {
                            redirect = true;

                            // BUG: 18821 - We'd like to update but gallery but prevents us
                            // update referer
                            //referer = uri.AbsoluteUri;

                            // get redirected location
                            bufferLength = _bufferSize;
                            index = 0;

                            // we do call GetLastWin32Error in ThrowGetLastErrorException
#pragma warning suppress 56523
                            if (!NativeMethods.HttpQueryInfo(hInetFile, NativeMethods.HTTP_QUERY_LOCATION, buffer, out bufferLength, out index))
                            {
                                ThrowGetLastErrorException("HttpQueryInfo");
                            }

                            // we do call GetLastWin32Error in ThrowGetLastErrorException
#pragma warning suppress 56523
                            if (!NativeMethods.InternetCloseHandle(hInetFile))
                            {
                                // what do we do with this value?
                                Marshal.GetLastWin32Error();
                            }

                            hInetFile = IntPtr.Zero;

                            Uri redirectUri;
                            if (Uri.TryCreate(Encoding.Unicode.GetString(buffer, 0, (int)bufferLength), UriKind.RelativeOrAbsolute, out redirectUri))
                            {
                                // the redirect uri might be a path (no host info), if that's the case, use the host of the
                                // original uri
                                if (!redirectUri.IsAbsoluteUri)
                                {
                                    redirectUri = new Uri(uri, redirectUri);
                                }

                                uri = redirectUri;
                            }
                        }
                        else
                        {
                            if (IfModifiedSince > DateTime.MinValue)
                            {
                                // in this case 304 is valid
                                if (String.Equals(responseCode, "304", StringComparison.Ordinal))
                                {
                                    downloadFile = false;
                                }
                            }
                            else
                            {
                                // unexpected response code... throw
                                // TODO: Add specific messages here (404, 500, etc...)
                                throw new WebException("Url '" + uri.AbsoluteUri + "' returned HTTP status code: " + responseCode);
                            }
                        }
                    }
                }

                // only follow redirects 20 times to avoid infinite loop
                i++;
            }
            while (redirect && i < 20);

            if (i == 20)
            {
                throw new InvalidOperationException("Unable to download file. Maximum number of redirects exceeded");
            }

            return downloadFile;
        }

        private static string GetResponseHeaders(IntPtr hInetFile)
        {
            StringBuilder sb = new StringBuilder();

            byte[] buffer = new byte[_bufferSize];
            int bufferLength = _bufferSize;
            int index = 0;

            if (!NativeMethods.HttpQueryInfo(hInetFile, NativeMethods.HTTP_QUERY_RAW_HEADERS_CRLF, buffer, out bufferLength, out index))
            {
                int win32Error = Marshal.GetLastWin32Error();
                if (win32Error == NativeMethods.ERROR_INSUFFICIENT_BUFFER)
                {
                    //Logging.LogMessage(TraceEventType.Warning, "Insufficient buffer size to store headers");
                }

                return String.Empty;
            }

            string headers = Encoding.Unicode.GetString(buffer, 0, bufferLength);
            sb.Append(headers);

            return sb.ToString();
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "TODO")]
        private static string GetContentDispositionFileName(string contentDispositionFileName, IntPtr hInetFile, byte[] buffer)
        {
            try
            {
                int bufferLength = _bufferSize;
                int index = 0;
                if (!NativeMethods.HttpQueryInfo(hInetFile, NativeMethods.HTTP_QUERY_CONTENT_DISPOSITION, buffer, out bufferLength, out index))
                {
                    // this isn't really an error... there was just no content disposition header
                    Marshal.GetLastWin32Error();
                }
                else
                {
                    string contentDispositionString = Encoding.Unicode.GetString(buffer, 0, (int)bufferLength);
                    //Logging.LogMessage(TraceEventType.Information, "Content-disposition header: " + contentDispositionString);

                    // find index of filename=
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
                        contentDispositionFileName = contentDispositionString.Substring(fileNameStart, fileNameLength).Trim('"');
                    }
                }
            }
#pragma warning suppress 56500
            catch (Exception ex)
            {
                // eat all errors checking for file name
                // Logging.LogMessage(TraceEventType.Warning, "Error getting content disposition header: " + ex.ToString());
            }

            return contentDispositionFileName;
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "TODO")]
        private static int GetContentLength(IntPtr hInetFile, byte[] buffer)
        {
            int responseSize = 1;
            try
            {
                int bufferLength = _bufferSize;
                int index = 0;
                if (!NativeMethods.HttpQueryInfo(hInetFile, NativeMethods.HTTP_QUERY_CONTENT_LENGTH, buffer, out bufferLength, out index))
                {
                    // log somewhere?
                    Marshal.GetLastWin32Error();
                }
                else
                {
                    string fileSizeString = Encoding.Unicode.GetString(buffer, 0, (int)bufferLength);
                    responseSize = int.Parse(fileSizeString, CultureInfo.InvariantCulture);
                }
            }
#pragma warning suppress 56500
            catch (Exception ex)
            {
                // eat errors getting file size
                // Logging.LogMessage(TraceEventType.Warning, "Error getting content length header: " + ex.ToString());
            }

            return responseSize;
        }

        private void SendBytesReadEvent(int bytesRead, int totalBytes)
        {
            if (_bytesRead != null)
            {
                _bytesRead(this, new BytesReadEventArgs(bytesRead, totalBytes));
            }
        }

        internal static string Referer
        {
            get
            {
                if (String.IsNullOrEmpty(_referer))
                {
                    _referer = "wpi://2.1.0.0/" + Environment.OSVersion.VersionString;
                }

                return _referer;
            }
        }

        internal static string UserAgent
        {
            get
            {
                if (String.IsNullOrEmpty(_userAgent))
                {
                    _userAgent = "Platform-Installer/" + typeof(ManagedWinInet).Assembly.GetName().Version +
                        "(" + Environment.OSVersion.VersionString + ")";
                }

                return _userAgent;
            }
        }

        internal void ThrowGetLastErrorException(string offendingFunction)
        {
     /*       Win32ErrorCode errorCode = (Win32ErrorCode)Marshal.GetLastWin32Error();
            string win32ErrorMessage = NativeMethods.GetMessage(errorCode);
            int hresult = NativeMethods.MakeHRFromErrorCode(errorCode);
            string errorString = String.Format(CultureInfo.InvariantCulture, _errorMessage, offendingFunction, hresult, win32ErrorMessage);
            throw new WebException(errorString);*/
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                // no managed resources to call dispose on

                if (_hInet != null && !_hInet.IsInvalid)
                {
                    _hInet.Dispose();
                }

                _disposed = true;
            }
        }

        ~ManagedWinInet()
        {
            Dispose(false);
        }
    }

    [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
    internal class SafeInetHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeInetHandle()
            : base(true)
        {
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        protected override bool ReleaseHandle()
        {
            bool result = NativeMethods.InternetCloseHandle(handle);
            if (!result)
            {
                // should log somewhere I guess
                Marshal.GetLastWin32Error();
            }

            return result;
        }
    }

    internal class BytesReadEventArgs : EventArgs
    {
        private int _bytesRead;
        private int _totalBytes;

        internal BytesReadEventArgs(int bytesRead, int totalBytes)
        {
            _bytesRead = bytesRead;
            _totalBytes = totalBytes;
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Common code. This is use from the framework")]
        internal int BytesRead
        {
            get
            {
                return _bytesRead;
            }
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Common code. This is use from the framework")]
        internal int TotalBytes
        {
            get
            {
                return _totalBytes;
            }
        }
    }

    internal static partial class NativeMethods
    {
        [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeInetHandle InternetOpen(
            string lpszAgent,
            uint dwAccessType,
            string lpszProxyName,
            string lpszProxyBypass,
            uint dwFlags
            );

        [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr InternetOpenUrl(
            SafeInetHandle hInternet,
            string lpszUrl,
            string lpszHeaders,
            int dwHeadersLength,
            int dwFlags,
            IntPtr dwContext
            );

        [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool HttpQueryInfo(
            IntPtr hRequest,
            uint dwInfoLevel,
            [MarshalAs(UnmanagedType.LPArray)]
            byte[] lpvBuffer,
            out int dwBufferLength,
            out int dwIndex
            );

        [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool InternetReadFile(
            IntPtr hRequest,
            [MarshalAs(UnmanagedType.LPArray)]
            byte[] lpBuffer,
            int dwNumberOfBytesToRead,
            out int lpdwNumberOfBytesRead
            );

        [DllImport("wininet.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool InternetCloseHandle(
            IntPtr hInternet
            );

        // dwAccessType values
        internal const uint INTERNET_OPEN_TYPE_PRECONFIG = 0; // use registry configuration
        internal const int INTERNET_FLAG_NO_AUTO_REDIRECT = 2097152;

        // dwInfoLevel values
        internal const uint HTTP_QUERY_CONTENT_LENGTH = 5;
        internal const uint HTTP_QUERY_STATUS_CODE = 19;
        internal const uint HTTP_QUERY_RAW_HEADERS_CRLF = 22;
        internal const uint HTTP_QUERY_LOCATION = 33;
        internal const uint HTTP_QUERY_CONTENT_DISPOSITION = 47;

        internal const int ERROR_INSUFFICIENT_BUFFER = 122;

/*        internal static string GetMessage(Win32ErrorCode errorCode)
        {
            StringBuilder lpBuffer = new StringBuilder(512);
            if (FormatMessageW(
                FormatMessageFlags.IGNORE_INSERTS | FormatMessageFlags.FROM_SYSTEM | FormatMessageFlags.ARGUMENT_ARRAY,
                IntPtr.Zero,
                (int)errorCode,
                0,
                lpBuffer,
                lpBuffer.Capacity,
                IntPtr.Zero) != 0)
            {
                return lpBuffer.ToString();
            }

            return String.Format(CultureInfo.CurrentCulture, "Unknown error {0}", errorCode);
        }

        internal static int MakeHRFromErrorCode(Win32ErrorCode errorCode)
        {
            return (int)(0x80070000 | (uint)errorCode);
        }*/

        internal enum FormatMessageFlags
        {
            IGNORE_INSERTS = 0x00000200,
            FROM_SYSTEM = 0x00001000,
            ARGUMENT_ARRAY = 0x00002000
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int FormatMessageW(
            FormatMessageFlags dwFlags,
            IntPtr lpSource,
            int dwMessageId,
            int dwLanguageId,
            StringBuilder lpBuffer,
            int nSize,
            IntPtr Arguments);
    }
}
