using System;

using ValidationResult = AppGallery.SIR.ILog.ValidationResult;

namespace AppGallery.SIR
{
    public class ValidationCompletedEventArgs : EventArgs
    {
        protected ValidationResult _result;

        public ValidationCompletedEventArgs(ValidationResult result)
        {
            _result = result;
        }

        public ValidationResult Result
        {
            get { return _result; }
        }
    }
}
