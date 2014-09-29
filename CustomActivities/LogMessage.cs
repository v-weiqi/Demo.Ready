using System;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using CustomActivities.Properties;
using Model;

namespace CustomActivities
{
    public static class LogMessage
    {
        public static void Log(string transactionId, string activityName, string log)
        {
            if (string.IsNullOrEmpty(log.Trim()))
                return;

            var myparm = new SqlParameter[3];
            myparm[0] = new SqlParameter("@Transactionid", transactionId);
            myparm[1] = new SqlParameter("@ActivityName", activityName);
            myparm[2] = new SqlParameter("@Log", log);

            var query = string.Format(@"
	                    INSERT INTO [TrackingWorkflow].[dbo].[FmsMessage]
	                               ([Transactionid]
	                               ,[ActivityName]
	                               ,[Log]
	                               ,[TimeCreated])
	                         VALUES
	                               (@Transactionid
	                               ,@ActivityName
	                               ,@Log
	                               ,GETDATE())", transactionId, activityName, log);

            RunNonQuery(query, myparm);
        }
        public static void AddStatus(string transactionId, int stepId, SubmissionStatus status)
        {
            var myparm = new SqlParameter[5];
            myparm[0] = new SqlParameter("@Transactionid", transactionId);
            myparm[1] = new SqlParameter("@StepId", stepId);
            myparm[2] = new SqlParameter("@Status", status.Status);
            myparm[3] = new SqlParameter("@Log", string.IsNullOrEmpty(status.Log)?"":status.Log.Trim());
            myparm[4] = new SqlParameter("@Passed", status.Pass ? 1 : 0);


            var sqlQuery = string.Format(@"
			INSERT INTO [TrackingWorkflow].[dbo].[Status]
					   ([Transactionid]
                        ,[StepId]
					   ,[Status]
                        ,[Log]
					   ,[Passed]
					   ,[datecreated])
				 VALUES
					   (@Transactionid
					   ,@StepId
					   ,@Status
                        ,@Log
                        ,@Passed
					   ,GETDATE())");
            RunNonQuery(sqlQuery, myparm);
        }

        public static void RunNonQuery(string query, SqlParameter[] parameters)
        {

            using (SqlConnection connection = new SqlConnection(Regex.Unescape(AppSettings.Default.TrackingDb)))
            {
                var command = new SqlCommand(query, connection);
                command.CommandType = CommandType.Text;
                if (parameters != null && parameters.Length > 0)
                    foreach (var sqlParameter in parameters)
                    {
                        command.Parameters.Add(sqlParameter);
                    }


                command.Connection.Open();
                command.ExecuteNonQuery();
            }

        }

    }
}
