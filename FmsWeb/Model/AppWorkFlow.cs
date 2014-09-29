using System;
using System.Collections.Generic;

namespace FmsWeb.Model
{
    public class AppWorkFlow
    {
        public AppWorkFlow()
        {
            Status = new List<SubmissionStatus>();
        }
        public string AppId { get; set; }
        public string AppVersion { get; set; }
        public string WorkflowInstanceId { get; set; }
        public string TransactionId { get; set; }
        public string Feed { get; set; }
        public DateTime TimeCreated { get; set; }
        public List<SubmissionStatus> Status { get; set; }
        public bool Failed { get; set; }
        public FailReason FailReason { get; set; }
        public string AssignedTo { get; set; }
        public string Reason {get;set;}

    }

    public class SubmissionStatus
    {
        public string Status { get; set; }
        public DateTime Date { get; set; }
        public bool Pass { get; set; }
        public string Log { get; set; }
    }
}