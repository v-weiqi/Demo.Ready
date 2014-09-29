using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WAGFeedValidator
{
    public class Error
    {
        public string Message { get; private set; }
        public Exception Exception { get; set; }
   
        public Error(string message, Exception e)
        {
            Message = message;
            Exception = e;
        }
    }
}
