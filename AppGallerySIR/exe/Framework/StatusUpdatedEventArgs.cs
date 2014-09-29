using System;
using LogEvent = AppGallery.SIR.ILog.LogEvent;

namespace AppGallery.SIR
{
    public class StatusUpdatedEventArgs : EventArgs
    {
        protected LogEvent _validationEvent;

        public StatusUpdatedEventArgs(LogEvent logEvent)
        {
            _validationEvent = logEvent;
        }

        public LogEvent ValidationEvent
        {
            get { return _validationEvent; }
        }
    }
}
