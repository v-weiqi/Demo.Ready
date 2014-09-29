using System;
using System.Activities;
using AppGallery.SIR;
using CustomActivities.Properties;
using Model;

namespace CustomActivities
{

    public sealed class SirToolValidation : CodeActivity<ValidateResult>
    {

        public InArgument<Model.Submission> Submission { get; set; }
        public InArgument<int> StepId { get; set; }

        private string _messages = "";
        private string _errors = "";
        private string _finalResult = "";
        

        protected override ValidateResult Execute(CodeActivityContext context)
        {
            var stepId = context.GetValue(StepId);
            var result = new ValidateResult();
            var submission = context.GetValue(this.Submission);

            try
            {
                _messages = "";
                _finalResult = "";

                var packageValidationManager = new PackageValidationManager();
                
                packageValidationManager.SkipInstallation = true;
                packageValidationManager.SkipReportGeneration = true;


                packageValidationManager.ValidationStatusUpdated += packageValidationManager_ValidationStatusUpdated;
                packageValidationManager.ValidationCompleted += packageValidationManager_ValidationCompleted;


                string url = submission.LiveInstallerUrls[0];
                packageValidationManager.ValidatePackage(url);

                result.Message = "\n MESSAGES\n\n"; ;
                result.Message += _messages;
                result.Message += "\n\n ERRORS\n\n"; ;
                result.Message += _errors;
                result.Result = (_finalResult != "Fail");


            }
            catch (Exception ex)
            {
                result.Message = "\n MESSAGES\n\n"; ;
                result.Message += _messages;
                result.Message += "\n\n ERRORS\n\n"; ;
                result.Message += "\n\n EXCEPTION \n";
                result.Message += ex.StackTrace;
                result.Result = false;
            }

            var status = new SubmissionStatus { Status = "SIR Tool Validation", Date = DateTime.Now, Pass = result.Result, Log = result.Message };
            submission.Statuses.Add(status);
            LogMessage.AddStatus(submission.TransactionId, stepId, status);
            return result;
        }

        private void packageValidationManager_ValidationStatusUpdated(object sender, StatusUpdatedEventArgs e)
        {
            if (e.ValidationEvent.Type.ToString() == "Fail")
                _errors += "\n  -" + e.ValidationEvent.Message;
            else
                _messages += "\n  -" + e.ValidationEvent.Message;
        }

        private void packageValidationManager_ValidationCompleted(object sender, ValidationCompletedEventArgs e)
        {
            _finalResult = e.Result.ToString();
        }

    }

}
