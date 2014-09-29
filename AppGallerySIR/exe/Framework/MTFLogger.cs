using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

using LogEvent = AppGallery.SIR.ILog.LogEvent;
using LogEventType = AppGallery.SIR.ILog.LogEvent.LogEventType;

namespace AppGallery.SIR
{
    public static class MTFLogger
    {
        private static Type _logObject = null;        

        public static bool Initialize()
        {
            try
            {
                // try loading the mtf logger (should be gac'd)
                AssemblyName asm = new AssemblyName(
                    "microsoft.webplatform.test.logging, Version=1.5.0.0, Culture=neutral, " 
                    + "PublicKeyToken=5e140785eb9fd630, processorArchitecture=MSIL");

                Assembly assemblyInstance = Assembly.Load(asm);
                _logObject = assemblyInstance.GetType("Microsoft.WebPlatform.Test.Logging.Logger"); // our own logger                        

                // try adding the wtt logger.
                Object wttLogger = assemblyInstance.CreateInstance("Microsoft.WebPlatform.Test.Logging.WTTLogger");
                if (wttLogger == null)
                {
                    Console.WriteLine("Could not load WTT Logger");
                    return false;
                }

                _logObject.InvokeMember("AddLogger", BindingFlags.InvokeMethod, null, _logObject, new Object[] { wttLogger });

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not initialize MTF Logger");
                Console.WriteLine("Exception " + ex.ToString());
                return false;
            }
        }

        public static void GenerateLog(List<LogEvent> logEvents)
        {
            foreach (LogEvent logEvent in logEvents)
            {
                switch (logEvent.Type)
                {
                    case LogEvent.LogEventType.Fail :
                    case LogEvent.LogEventType.Exception:
                        LogFail(logEvent.Message);
                        if (logEvent.StackTrace != null)
                        {
                            LogFail(logEvent.StackTrace);                            
                        }
                        break;                                                               

                    case LogEvent.LogEventType.Pass :
                        LogPass(logEvent.Message);                        
                        break;

                    case LogEvent.LogEventType.Informational :
                    case LogEvent.LogEventType.Installation:
                        LogMessage(logEvent.Message);
                        break;                                           
                }
            }
        }

        private static void LogMessage(string message)
        {
            _logObject.InvokeMember("LogMessage", BindingFlags.InvokeMethod, null, _logObject, new Object[] { message });
        }

        private static void LogFail(string message)
        {
            _logObject.InvokeMember("LogFail", BindingFlags.InvokeMethod, null, _logObject, new Object[] { message });
        }

        private static void LogPass(string message)
        {
            _logObject.InvokeMember("LogPass", BindingFlags.InvokeMethod, null, _logObject, new Object[] { message });
        }
    }
}