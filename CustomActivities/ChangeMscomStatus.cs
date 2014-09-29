using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Activities;
using Model;

namespace CustomActivities
{

    public sealed class ChangeMscomStatus : CodeActivity
    {
        public InArgument<Model.Submission> Submission { get; set; }
        public InArgument<int> StepId { get; set; }
        public InArgument<string> Status { get; set; }

        // If your activity returns a value, derive from CodeActivity<TResult>
        // and return the value from the Execute method.
        protected override void Execute(CodeActivityContext context)
        {
            var stepId = context.GetValue(StepId);
            var submission = context.GetValue(this.Submission);
            var mscomStatus = context.GetValue(this.Status);
            var status = new SubmissionStatus { Status = "Change MSCOM Status to " + mscomStatus, Date = DateTime.Now, Pass = true };

            try
            {


            }
            catch (Exception ex)
            {
                status.Pass = false;
            }

            submission.Statuses.Add(status);
            LogMessage.AddStatus(submission.TransactionId, stepId, status);
        }
    }
}
