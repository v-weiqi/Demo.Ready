using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Activities;
using Model;

namespace CustomActivities
{

    public sealed class NotifyVendor : CodeActivity
    {
        public InArgument<Submission> Submission { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Submission submission = context.GetValue(this.Submission);
        }
    }
}
