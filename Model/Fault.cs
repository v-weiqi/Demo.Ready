using System;
using System.Runtime.Serialization;

namespace Model
{
    [DataContract]
    [Serializable()]
    public class Fault
    {
        [DataMember]
        public string WorkflowInstanceId { get; set; }

        [DataMember]
        public string ActivityName { get; set; }

        [DataMember]
        public string FaultDetails { get; set; }

        [DataMember]
        public DateTime TimeCreated { get; set; }
    }
}
