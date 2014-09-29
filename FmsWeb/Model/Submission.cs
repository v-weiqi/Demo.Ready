using System;
using System.Collections.Generic;
using System.Linq;

namespace FmsWeb.Model
{
    public class Submission
    {
        public Submission()
        {
            Transactions = new List<Transaction>();
            WorkFlows = new List<AppWorkFlow>();
        }
        public string NickName { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Version { get; set; }
        public int SubmissionId { get; set; }
        public IList<Transaction> Transactions { get; set; }
        public IList<AppWorkFlow> WorkFlows { get; set; }
        public string CurrentState { get; set; }
        public string StateID { get; set; }
        public string ImageGuid { get; set; }
        public int TurnAroundTime
        {
            get
            {
                if(Transactions != null && Transactions.Count > 1)
                    return Transactions.Max(t => t.TransactionDate).Subtract(Transactions.Min(t => t.TransactionDate)).Days;

                return 0;
            }
        }
    }
}