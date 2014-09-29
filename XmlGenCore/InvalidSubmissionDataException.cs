using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XmlGenCore
{
    public class InvalidSubmissionDataException : Exception
    {
        public InvalidSubmissionDataException(string productId, string datafield, string message=null) :
            base(String.Format("The submission with product ID: {0}, has bad data in the field '{1}'. {2}",
                productId,
                datafield,
                message))
        {
        }
    }
}
