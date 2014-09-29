using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Activities;
using Model;

namespace CustomActivities
{

    public sealed class SourceDepot : CodeActivity
    {
        public InArgument<Model.Submission> Submission { get; set; }
        public InArgument<int> StepId { get; set; }

        protected override void Execute(CodeActivityContext context)
        {

        }
    }
}
