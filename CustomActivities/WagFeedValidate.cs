using System;
using System.Activities;
using System.Activities.Tracking;
using CustomActivities.Properties;
using Model;
using WAGFeedValidator;

namespace CustomActivities
{
    public sealed class WagFeedValidate : CodeActivity<ValidateResult>
    {
        public InArgument<Model.Submission> Submission { get; set; }
        public InArgument<int> StepId { get; set; }

        protected override ValidateResult Execute(CodeActivityContext context)
        {
            var stepId = context.GetValue(StepId);
            var result = new ValidateResult();
            var submission = context.GetValue(this.Submission);

            string file = AppSettings.Default.FeedDirectory + "\\" + submission.TransactionId + ".xml";
            var validator = new Validator();
            SubmissionStatus status;


            try
            {
                var synch = validator.ValidateFeed(AppSettings.Default.WebProductList, file);


                foreach (var error in synch.Errors)
                {
                    result.Message += "\n\n" + error.Message;
                    result.Message += "\n" + error.Exception;
                }
                result.Result = false;

                bool appPresentInList = false;
                string appMessage = "";
                foreach (var app in synch.Applications)
                {
                    appMessage += "\n" + app;
                    if (app.ToLowerInvariant() == submission.AppId.ToLowerInvariant()
                        || app.ToLowerInvariant().Contains(submission.AppId.ToLowerInvariant()))
                        appPresentInList = true;
                }

                result.Message += string.Format("\n\n App {0} {1} is {2} present in the list", submission.AppId,
                                                submission.Version, appPresentInList ? "" : "not");
                result.Message += appMessage;

                result.Result = appPresentInList && (synch.Errors.Count > 0);
                status = new SubmissionStatus
                {
                    Status = "WagFeed Validation",
                    Date = DateTime.Now,
                    Pass = result.Result,
                    Log = result.Message

                };
            }
            catch (Exception ex)
            {
                result.Result = false;
                status = new SubmissionStatus
                {
                    Status = "WagFeed Validation",
                    Date = DateTime.Now,
                    Pass = result.Result,
                    Log = ex.StackTrace
                };

            }

            //result.Result = true;
            //status = new SubmissionStatus
            //{
            //    Status = "WagFeed Validation",
            //    Date = DateTime.Now,
            //    Pass = result.Result,
            //    Log = "some failure"
            //};

            submission.Statuses.Add(status);
            LogMessage.AddStatus(submission.TransactionId, stepId, status);


            return result;
        }
    }
}
