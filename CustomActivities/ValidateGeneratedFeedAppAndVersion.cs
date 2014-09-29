using System;
using System.Activities;
using Model;

namespace CustomActivities
{
    public sealed class ValidateGeneratedFeedAppAndVersion : CodeActivity<bool>
    {
        public InArgument<Model.Submission> Submission { get; set; }
        public InArgument<string> AppId { get; set; }
        public InArgument<string> Version { get; set; }
        public InArgument<int> StepId { get; set; }

        protected override bool Execute(CodeActivityContext context)
        {
            var submission = context.GetValue(Submission);
            var stepId = context.GetValue(StepId);
            var appId = context.GetValue(AppId);
            var version = context.GetValue(Version);

            var status = new SubmissionStatus
            {
                Status = "AppId and Version validation",
                Date = DateTime.Now,
                Pass = submission.Feed.Contains(appId) && submission.Feed.Contains(version)
            };
            submission.Statuses.Add(status);
            LogMessage.AddStatus(submission.TransactionId, stepId, status);
            return (submission.Feed.Contains(appId) && submission.Feed.Contains(version));
        }
    }
}
