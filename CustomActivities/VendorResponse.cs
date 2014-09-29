using System;
using System.Activities;
using Model;

namespace CustomActivities
{

    public sealed class VendorResponse : CodeActivity<Model.Submission>
    {
        public InArgument<Model.Submission> Submission { get; set; }
        public InArgument<bool> Passed { get; set; }
        public InArgument<int> StepId { get; set; }

        protected override Submission Execute(CodeActivityContext context)
        {
            var stepId = context.GetValue(StepId);
            var passed = context.GetValue(Passed);
            var submission = context.GetValue(Submission);
            var status = new SubmissionStatus { Status = "Vendor TC2 Validation Completed", Date = DateTime.Now, Pass = passed };
            submission.Statuses.Add(status);
            LogMessage.AddStatus(submission.TransactionId, stepId, status);

            return submission;
        }
    }
}
