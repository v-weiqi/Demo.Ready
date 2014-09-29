using System.Collections.Generic;
using System.Activities;
using Model;

namespace CustomActivities
{

    public sealed class RemoveSubmission : CodeActivity<Dictionary<string, Submission>>
    {
        public InArgument<string> TransactionId { get; set; }
        public InArgument<Dictionary<string, Submission>> Submissions { get; set; }

        protected override Dictionary<string, Submission> Execute(CodeActivityContext context)
        {
            var transactionId = context.GetValue(TransactionId);
            var submissions = context.GetValue(Submissions);

            submissions.Remove(transactionId);
            return submissions;
        }
    }
}
