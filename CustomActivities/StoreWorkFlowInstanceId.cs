using System;
using System.Data.SqlClient;
using System.Activities;
using System.Text.RegularExpressions;
using CustomActivities.Properties;
using Model;

namespace CustomActivities
{

    public sealed class StoreWorkFlowInstanceId : CodeActivity
    {
        public InArgument<Model.Submission> Submission { get; set; }
        public InArgument<int> StepId { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var submission = context.GetValue(Submission);
            var stepId = context.GetValue(StepId);
            SubmissionStatus status;
            try
            {
                var connStr = Regex.Unescape(AppSettings.Default.TrackingDb);
                var connection = new SqlConnection(connStr);
                connection.Open();

                var query = string.Format(@"INSERT INTO [TrackingWorkFlow].[dbo].[AppWorkFlowInstanceId]
                   ([AppId]
                   ,[AppVersion]
                   ,[WorkflowInstanceId]
                    ,[TimeCreated]
                    ,[TransactionId],[AssignedTo])
             VALUES
                   ('{0}','{1}','{2}',GETDATE(),'{3}',null)", submission.AppId, submission.Version,
                                                       context.WorkflowInstanceId, submission.TransactionId);
                var adap =
                     new SqlCommand(query, connection);
                adap.ExecuteNonQuery();

                connection.Close();
                status = new SubmissionStatus
                {
                    Status = "Workflow Started and Instance Stored",
                    Date = DateTime.Now,
                    Pass = true
                };
                
            }
            catch (Exception ex)
            {
                status = new SubmissionStatus
                {
                    Status = "Workflow Started and Instance Stored",
                    Date = DateTime.Now,
                    Pass = false,
                    Log = ex.Message
                };
                
            }
            submission.Statuses.Add(status);
            LogMessage.AddStatus(submission.TransactionId, stepId, status);
        }
    }
}
