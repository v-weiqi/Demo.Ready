using System.Activities;
using System.Collections.Generic;
using Model;

namespace CustomActivities
{
    public sealed class GetSubmission : CodeActivity<Model.Submission>
    {
        public InArgument<string> TransactionId { get; set; }
        public InArgument<Dictionary<string, Submission>> Submissions { get; set; }

        protected override Submission Execute(CodeActivityContext context)
        {
            var transactionId = context.GetValue(TransactionId);
            var submissions = context.GetValue(Submissions);
            return submissions[transactionId.Trim()];
        }
    }
}
