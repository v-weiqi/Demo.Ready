using System.Activities;
using System.Collections.Generic;
using Model;

namespace CustomActivities
{
    public sealed class StoreSubmission : CodeActivity<Dictionary<string, Submission>>
    {
        public InArgument<Model.Submission> Submission { get; set; }
        public InArgument<Dictionary<string, Model.Submission>> Submissions { get; set; }

        protected override Dictionary<string, Model.Submission> Execute(CodeActivityContext context)
        {
            var submission = context.GetValue(Submission);
            var submissions = context.GetValue(Submissions) ?? new Dictionary<string, Submission>();

            if (!submissions.ContainsKey(submission.TransactionId))
                submissions.Add(submission.TransactionId, submission);
            else
                submissions[submission.TransactionId] = submission;

            return submissions;
        }
    }
}
