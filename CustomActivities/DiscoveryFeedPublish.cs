using System;
using System.Activities;
using Model;

namespace CustomActivities
{

    public sealed class DiscoveryFeedPublish : CodeActivity
    {
        public InArgument<Model.Submission> Submission { get; set; }
        public InArgument<int> StepId { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var stepId = context.GetValue(StepId);
            var submission = context.GetValue(this.Submission);
            var status = new SubmissionStatus { Status = "Make Changes To Discovery Feed", Date = DateTime.Now, Pass = true };

            submission.Statuses.Add(status);
            LogMessage.AddStatus(submission.TransactionId, stepId, status);
        }
    }
}
