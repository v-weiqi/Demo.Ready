using System;
using System.Diagnostics;

namespace AppGallery.SIR
{
    public class SIRTraceListener
    {
        private SIRTraceListener() { }

        public static void EnableTextFileTraceListener(string textFilePath)
        {
            TraceListener listener = new SimpleTextFileTraceListener(textFilePath);
            listener.TraceOutputOptions = TraceOptions.DateTime;
            Trace.Listeners.Add(listener);
        }
    }
}