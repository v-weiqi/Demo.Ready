using System;
using System.Data.SqlClient;

namespace FmsWeb.Adapter
{
    public class WorkFlowProcs
    {
        public static string TrackingActivitySql(string transactionId)
        {
            var sqlQuery = string.Format(@"
                    SELECT [State], [ActivityName], act.[TimeCreated], [ActivityInstanceId], [TransactionId], act.WorkflowInstanceId
FROM [TrackingWorkFlow].[Microsoft.Samples.Tracking].[ActivityInstanceEventsTable] as act, 
[TrackingWorkFlow].[dbo].[AppWorkFlowInstanceId] as awt
WHERE [awt].[TransactionId]='{0}'
AND act.WorkFlowInstanceId = awt.WorkFlowInstanceId ", transactionId);
            return sqlQuery;
        }


        public static string AppAndWorkFLowInstancesSql(string appId, string version)
        {
            var sqlQuery = string.Format(@"
                    SELECT [AppId],[AppVersion],[WorkflowInstanceId],[TimeCreated],[TransactionId]
                    FROM [TrackingWorkFlow].[dbo].[AppWorkFlowInstanceId]
                    WHERE [AppId]='{0}' AND [AppVersion] = '{1}'
ORDER BY [TimeCreated] DESC

", appId, version);
            return sqlQuery;
        }

        public static string AppAndWorkFLowInstance(string workFlowInstanceId)
        {
            var sqlQuery = string.Format(@"
                    SELECT [AppId],[AppVersion],[WorkflowInstanceId],[TimeCreated],[TransactionId],[Failed],[ID]
                FROM [TrackingWorkFlow].[dbo].[AppWorkFlowInstanceId]
                Left join [TrackingWorkflow].[dbo].[FailReason] on [FailReason].[id] = [AppWorkFlowInstanceId].[FailReason]
                WHERE [WorkflowInstanceId]='{0}'
                ORDER BY [TimeCreated] DESC", workFlowInstanceId);
            return sqlQuery;
        }



        public static string GetComments(string transactionId)
        {
            var sqlQuery = string.Format(@"
                SELECT [id]
                ,[transactionid]        
                ,[alias]
                ,[comment]
                ,[datecreated]
                FROM [TrackingWorkflow].[dbo].[Comments]
                where [transactionid] = '{0}'
                order by [datecreated] DESC
            ", transactionId);
            return sqlQuery;
        }

        public static string AddComment()
        {

            var sqlQuery = string.Format(@"
                INSERT INTO [TrackingWorkflow].[dbo].[Comments]
                    ([alias]
                    ,[comment]
                    ,[datecreated]
                    ,[transactionid])
                VALUES
                    (@Alias
                    ,@Comment
                    , GETDATE()
                    ,@TransactionId)
                ");
            return sqlQuery;
        }

        public static string EditComment()
        {

            var sqlQuery = string.Format(@"
                Update [TrackingWorkflow].[dbo].[Comments]
                    Set
                        [comment]= @Comment
                        ,[datecreated] = GETDATE()
                    Where
                        [id] = @CommentID
                    And [transactionid] = @TransactionId
                ");
            return sqlQuery;
        }

        public static string DeleteComment()
        {

            var sqlQuery = string.Format(@"
                Delete From 
                        [TrackingWorkflow].[dbo].[Comments]
                    Where
                        [id] = @CommentID
                    And [transactionid] = @TransactionId
                ");
            return sqlQuery;
        }

        public static string GetStatus(string transactionId)
        {
            var sqlQuery = string.Format(@"
                SELECT  
                [DisplayName]
                ,[status]
                ,[passed]
                ,[Log]
                ,[datecreated]
                FROM [TrackingWorkflow].[dbo].[WorkFlowStep]
                Left join [TrackingWorkflow].[dbo].[Status] 
                on [Status].[StepId] = [WorkFlowStep].[StepId] 
                AND [transactionid] = '{0}'
                order by case when [datecreated] is null then 1 else 0 end, [datecreated], [WorkFlowStep].[StepId] asc
            ", transactionId);
            return sqlQuery;
        }

        public static string DeleteStatus(string transactionId)
        {
            var sqlQuery = string.Format(@"
                DELETE FROM [TrackingWorkflow].[dbo].[Status]
                where [transactionid] = '{0}'
            ", transactionId);
            return sqlQuery;
        }

        public static string GetFaults(string transactionId)
        {
            var sqlQuery = string.Format(@"
            SELECT [ActivityName]     
            ,[FaultDetails]
            ,[ExtendedActivityEventsTable].[TimeCreated]
            FROM [TrackingWorkflow].[Microsoft.Samples.Tracking].[ExtendedActivityEventsTable], [TrackingWorkflow].[dbo].[AppWorkFlowInstanceId]
            Where [FaultDetails] is not null
            AND [ExtendedActivityEventsTable].[WorkflowInstanceId] = [AppWorkFlowInstanceId].[WorkflowInstanceId]
            AND [TransactionId] = '{0}'
                        ", transactionId);
            return sqlQuery;
        }

        public static string GetLatestRuns()
        {
            var sqlQuery = string.Format(@"
SELECT [AppId]
      ,[AppVersion]
      ,[WorkflowInstanceId]
      ,[TimeCreated]
      ,[AppWorkFlowInstanceId].[TransactionId]
      ,[status]
      ,[passed]
     ,[datecreated]
      ,[AssignedTo]
  FROM [TrackingWorkflow].[dbo].[AppWorkFlowInstanceId], [TrackingWorkflow].[dbo].[Status]
  where [AppWorkFlowInstanceId].[TransactionId] = [Status].[TransactionId]
  AND [TimeCreated] in (SELECT top 25 MAX([TimeCreated])
					FROM [TrackingWorkflow].[dbo].[AppWorkFlowInstanceId]
					group by [AppId],[AppVersion])
  ORDER BY CASE WHEN [AssignedTo] IS NULL THEN 0 WHEN CAST([AssignedTo] AS nvarchar) ='None' THEN 0 ELSE 1 END, [TimeCreated] DESC, [datecreated] ASC");
            return sqlQuery;
        }

        public static string GetCompletedRuns()
        {
            var sqlQuery = string.Format(@"
SELECT [AppId]
      ,[AppVersion]    
      ,Reason 
      ,[Failed]      
      ,[AssignedTo],[TimeCreated],[WorkflowInstanceId]
  FROM [TrackingWorkflow].[dbo].[AppWorkFlowInstanceId]as Awf
  left outer join [TrackingWorkflow].[dbo].[FailReason] as tfr on awf.FailReason = tfr.id
  ORDER BY [TimeCreated] DESC");
            return sqlQuery;
        }


        public static string GetLastExecutingMessage(string workFlowInstanceId)
        {
                    var sqlQuery = string.Format(@"
            SELECT TOP 1 *
            FROM (
            SELECT TOP 2 [ID], [WorkflowInstanceId], [ActivityName], [State]
            FROM [TrackingWorkflow].[Microsoft.Samples.Tracking].[ActivityInstanceEventsTable]
            WHERE [WorkflowInstanceId] = '{0}'
            ORDER BY [ID] DESC
            ) AS newTable
            ORDER BY [ID] ASC", workFlowInstanceId);
                    return sqlQuery;
        }


        public static string UpdateFailStatus(string transactionId, int failReason)
        {
            string sqlQuery = "";
            if (failReason == 0)
            {
                sqlQuery = string.Format(@"
                UPDATE [TrackingWorkflow].[dbo].[AppWorkFlowInstanceId]
                   SET [Failed] = 1
                 WHERE TransactionId = '{0}'
                ", transactionId);
            }
            else
            {
                sqlQuery = string.Format(@"
                UPDATE [TrackingWorkflow].[dbo].[AppWorkFlowInstanceId]
                   SET [Failed] = 1
                      ,[FailReason] = {0}
                 WHERE TransactionId = '{1}'
                ", failReason, transactionId);
            }
            
            return sqlQuery;
        }

        public static string GetVendorList()
        {
            string sqlQuery = string.Format(@"
                    SELECT 
                        [name]
                        ,[alias]
                    FROM  [TrackingWorkflow].[dbo].[Vendors]
                    Order by alias
                ");
            return sqlQuery;
        }

        public static string UpdateAssignment()
        {
            string sqlQuery = string.Format(@"
                    Update
                        [TrackingWorkflow].[dbo].[AppWorkFlowInstanceId]
                    Set 
                        [AssignedTo] = @assignTo
                    Where
                            [WorkflowInstanceId] = @workFlowInstanceID
                        And [AppId] = @appID
                        And [AppVersion] = @version
                ");

            return sqlQuery;
        }

        public static string GetTestedRuns()
        {
            string sqlQuery = string.Format(@"
                    Select [AppId]
                          ,[AppVersion]
                          ,wk.[WorkflowInstanceId]
                          ,wk.[Failed]
                          ,wk.[FailReason]
                          ,wk.[TimeCreated]
                          ,wk.[TransactionId]
                          ,st.[status]
                          ,st.[passed]
                          ,st.[datecreated]
                          ,wk.[AssignedTo]
                    From
	                    [TrackingWorkflow].[dbo].[AppWorkFlowInstanceId] wk
	                    ,[TrackingWorkflow].[dbo].[Status] st
	                    ,(Select
		                    [WorkflowInstanceId]
		                    From
			                    [TrackingWorkflow].[Microsoft.Samples.Tracking].[ActivityInstanceEventsTable]
		                    Where
				                    [ActivityName] = 'VendorTC2Response'
			                    And [State] = 'Closed'
	                    ) tr
                    Where 
		                    wk.[WorkflowInstanceId] = tr.[WorkflowInstanceId]
	                    And wk.[TransactionId] = st.[transactionid]
	                    And st.[status] = 'Vendor TC2 Validation Completed'
                    Order by
	                    st.[datecreated] Desc
                ");

            return sqlQuery;
        }


    }
}