using System;
using System.Runtime.Serialization;

namespace Model
{
    [DataContract]
    [Serializable()]
    public class FmsResponse
    {
        [DataMember]
        public Submission Submission { get; set; }

    }
}
