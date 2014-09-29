using System;
using System.IO;
using System.Diagnostics;

namespace AppGallery.SIR
{
    // TODO: do we need 2 classes for the trace listener?
    public class SimpleTextFileTraceListener : TraceListener
    {
        private string _logFileName;

        public SimpleTextFileTraceListener(string logFileName)
            : base(logFileName)
        {           
            _logFileName = Environment.ExpandEnvironmentVariables(logFileName);
        }

        public override void Write(string message)
        {
            WriteWithRetry(message);
        }

        public override void WriteLine(string message)
        {
            WriteWithRetry(message, Environment.NewLine);
        }
        
        private void WriteWithRetry(params string[] strings)
        {
            if (strings != null)
            {
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        using (StreamWriter writer = File.AppendText(_logFileName))
                        {
                            foreach (string str in strings)
                            {
                                writer.Write(str);
                            }
                        }
                        return;
                    }
                    catch (Exception)
                    {
                        // cant really do much here
                    }
                }
            }
        }
    }
}
