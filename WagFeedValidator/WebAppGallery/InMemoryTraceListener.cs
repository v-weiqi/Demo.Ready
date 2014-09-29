using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace WAGFeedValidator.WebAppGallery
{
    public class WebPIErrorEventArgs: EventArgs
    {
        public string Message { get; private set; }

        public WebPIErrorEventArgs(string message)
        {
            Message = message;
        }
    }

    public class InMemoryTraceListener : TraceListener
    {
        public delegate void WebPITrace(object sender, WebPIErrorEventArgs e);

        public event WebPITrace WebPITraceEmitted;

        public InMemoryTraceListener()
        {
        }

        public override void Write(string message)
        {
            if (WebPITraceEmitted != null)
            {
                if (!String.IsNullOrEmpty(message) && !message.StartsWith("DownloadManager"))
                {
                    WebPITraceEmitted(this, new WebPIErrorEventArgs(message));
                }
            }
        }

        public override void WriteLine(string message)
        {
            if (WebPITraceEmitted != null)
            {
                if (!String.IsNullOrEmpty(message) && !message.StartsWith("DownloadManager"))
                {
                    WebPITraceEmitted(this, new WebPIErrorEventArgs(message));
                }
            }
        }
    }
}
