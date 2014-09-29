using System.Activities;
using Model;

namespace CustomActivities
{

    public sealed class PublishAppFeed : CodeActivity
    {
        public InArgument<Model.Submission> Submission { get; set; }
        public InArgument<int> StepId { get; set; }

        protected override void Execute(CodeActivityContext context)
        {

        }
    }
}
