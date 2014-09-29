using System;
using System.Activities;
using Model;

namespace CustomActivities
{

    public sealed class RecycleAppPool : CodeActivity<Model.Submission>
    {
        public InArgument<Model.Submission> Submission { get; set; }
        public InArgument<int> StepId { get; set; }

        protected override Submission Execute(CodeActivityContext context)
        {
            var submission = context.GetValue(Submission);
            var stepId = context.GetValue(StepId);

            var status = new SubmissionStatus { Status = "Recycling App Pool", Date = DateTime.Now, Pass = true };
            submission.Statuses.Add(status);
            LogMessage.AddStatus(submission.TransactionId, stepId, status);
            return submission;
        }
    }
}
