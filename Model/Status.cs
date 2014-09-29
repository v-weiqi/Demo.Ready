using System;
using System.Runtime.Serialization;

namespace Model
{
    [DataContract]
    [Serializable()]
    public class SubmissionStatus
    {
        [DataMember]
        public string Status { get; set; }

        [DataMember]
        public DateTime Date { get; set; }

        [DataMember]
        public bool Pass { get; set; }

        [DataMember]
        public string Log { get; set; }
    }
}
