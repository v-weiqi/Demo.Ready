using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace XmlGenCore
{
    public class LogClass
    {
        private static LogClass _logger;
        private TextWriter _logWriter = null;

        private LogClass(TextWriter logWriter)
        {
            _logWriter = logWriter;
        }

        public static LogClass Logger()
        {
            if (_logger == null)
            {
                _logger = new LogClass(Console.Out);
            }

            return _logger;
        }

        public static void InitLogger(TextWriter logWriter)
        {
            _logger = new LogClass(logWriter);
        }

        public static void InitLogger(Stream logStream)
        {
            InitLogger(new StreamWriter(logStream));
        }

        public void Write(string message)
        {

            if (_logWriter != null)
            {
                try
                {
                    _logWriter.Write(message);
                }
                catch { }
            }
        }

        public void Write(string format, params object[] args)
        {
            Write(String.Format(format, args));
        }

        public void WriteLine(string message)
        {
            Write(message + Environment.NewLine);
        }

        public void WriteLine(string format, params object[] args)
        {
            string newMessage = String.Format(format, args) + Environment.NewLine;
            Write(newMessage);
        }
    }
}
