using System;
using System.Activities;
using Model;

namespace CustomActivities
{
    public sealed class FeedDiff : CodeActivity
    {
        public InArgument<Model.Submission> Submission { get; set; }
        public InArgument<int> StepId { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var submission = context.GetValue(Submission);
            var stepId = context.GetValue(StepId);

            var status = new SubmissionStatus {Status = "Feed Difference", Date = DateTime.Now, Pass = true};
            submission.Statuses.Add(status);
            LogMessage.AddStatus(submission.TransactionId, stepId, status);

        }
    }
}