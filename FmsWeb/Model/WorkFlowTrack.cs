using System;

namespace FmsWeb.Model
{
    public class WorkFlowTrack
    {
        public string WorkflowInstanceId { get; set; }
        public string State { get; set; }
        public string ActivityName { get; set; }
        public string ActivityInstanceId { get; set; }
        public string TransactionId { get; set; }
        public DateTime TimeCreated { get; set; }
    }
}