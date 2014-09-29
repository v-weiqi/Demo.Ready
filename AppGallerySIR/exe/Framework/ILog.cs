using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

using LogEvent = AppGallery.SIR.ILog.LogEvent;
using LogEventType = AppGallery.SIR.ILog.LogEvent.LogEventType;

namespace AppGallery.SIR
{
    public abstract class ILog
    {
        #region Internal classes

        public class LogEvent
        {
            public enum LogEventType
            {
                Pass,
                Fail,
                Informational,
                Exception,
                Installation
            }

            protected DateTime _timeOccurred;
            protected LogEventType _type;
            protected string _message;
            protected string _stackTrace;
            protected string _location;
            protected string _target;

            public LogEvent(LogEventType eventType, string message)
            {
                _timeOccurred = DateTime.Now;
                _type = eventType;
                _message = message;
            }

            public LogEvent(LogEventType eventType, string message, string stackTrace)
            {
                _timeOccurred = DateTime.Now;
                _type = eventType;
                _message = message;
                _stackTrace = stackTrace;
            }

            public LogEvent(LogEventType eventType, string message, string location, string target)
            {
                _timeOccurred = DateTime.Now;
                _type = eventType;
                _message = message;
                _location = location;
                _target = target;
            }

            public DateTime TimeOccurred
            {
                get { return _timeOccurred; }
            }

            public LogEventType Type
            {
                get { return _type; }
            }

            public string Message
            {
                get { return _message; }
            }

            public string StackTrace
            {
                get { return _stackTrace; }
            }

            public string Location
            {
                get { return _location; }
            }

            public string Target
            {
                get { return _target; }
            }
        }

        #endregion

        #region Constants and Enums

        public enum ValidationResult
        {
            Pass,
            Fail,
            Unknown
        }

        #endregion

        #region Data Members

        protected ValidationResult _validationResult = ValidationResult.Unknown;
        protected List<LogEvent> _events;
        protected string _reportFullLocation;
        protected string _packageLocation;
        protected string _appUrl;
        protected string _reportFolder;
        protected string _reportFileName;
        
        #endregion 

        #region Properties

        public List<LogEvent> Events
        {
            get
            {
                if (_events == null)
                {
                    _events = new List<LogEvent>();
                }
                return _events;
            }
        }

        public string PackageLocation
        {
            get { return _packageLocation; }
            set { _packageLocation = value; }
        }

        public string AppUrl
        {
            get { return _appUrl; }
            set { _appUrl = value; }
        }
        
        public string ReportFolder
        {
            get
            {
                if (string.IsNullOrEmpty(_reportFolder))
                {
                    _reportFolder = PackageValidationManager.ExecutableFolder;
                }
                return _reportFolder;
            }
            set
            {
                if (!string.IsNullOrEmpty(value) && Directory.Exists(Environment.ExpandEnvironmentVariables(value)))
                {
                    _reportFolder = Environment.ExpandEnvironmentVariables(value);
                }
            }
        }

        public string ReportFileName
        {
            get { return _reportFileName; }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _reportFileName = Environment.ExpandEnvironmentVariables(value);
                }
            }
        }

        public string ReportFullLocation
        {
            get
            {
                if (string.IsNullOrEmpty(_reportFullLocation))
                {
                    if (string.IsNullOrEmpty(_reportFileName))
                    {
                        _reportFileName = GenerateReportFileName();
                    }
                    _reportFileName = Path.Combine(ReportFolder, _reportFileName);
                }
                return _reportFileName;
            }
            set { _reportFileName = value; }
        }

        #endregion

        #region Event and Delegate

        public delegate void StatusUpdatedHandler(object sender, StatusUpdatedEventArgs e);
        public event StatusUpdatedHandler StatusUpdated;

        #endregion

        #region Methods

        public void LogResult(ValidationResult result)
        {
            _validationResult = result;
        }

        public void Log(LogEvent logEvent)
        {
            StatusUpdated(this, new StatusUpdatedEventArgs(logEvent));
            Events.Add(logEvent);
        }
        
        public void Log(string message)
        {
            Log(LogEventType.Informational, message);
        }

        public void Log(LogEventType eventType, List<string> messages)
        {
            StringBuilder aggregatedMessages = new StringBuilder();

            foreach (string message in messages)
            {
                aggregatedMessages.Append(message);
                aggregatedMessages.Append(Environment.NewLine);
            }

            Log(new LogEvent(eventType, aggregatedMessages.ToString()));
        }

        public void Log(LogEventType eventType, string message)
        {
            Log(new LogEvent(eventType, message));
        }

        public void Log(LogEventType eventType, string message, string location, string target)
        {
            Log(new LogEvent(eventType, message, location, target));
        }

        public void Log(Exception e)
        {
            Log(new LogEvent(LogEventType.Exception, e.Message, e.StackTrace));
        }

        public void Clean()
        {
            Events.RemoveAll(MatchEvent);
            _reportFileName = string.Empty; // force regeneration of report filename
        }

        public bool MatchEvent(LogEvent logEvent)
        {
            // match all events
            return true;
        }

        public abstract string GenerateReportFileName();
        public abstract void GenerateLog();

        #endregion
    }
}
