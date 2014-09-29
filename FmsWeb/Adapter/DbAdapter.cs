using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using FmsWeb.Model;
using System.Data.SqlClient;

namespace FmsWeb.Adapter
{
    public class DbAdapter
    {
        private MsComRepository _repository;

        public DbAdapter()
        {
            _repository = new MsComRepository();
        }

        public Submission GetSubmissionAndTransaction(string appId, string version)
        {
            var table = _repository.GetDataFromQuery(SubmissionDetails(appId, version));

            var submissions = HydrateSubmission(table, true);

            if (submissions.Count > 0)
                return HydrateSubmission(table, true)[0];
            

                return new Submission();

        }

        public List<Submission> GetSubmissionsInTesting()
        {
            var table = _repository.GetDataFromQuery(InTesting());
            return HydrateSubmission(table, false);
        }

        public List<Submission> GetSubmissionsPending()
        {
            var table = _repository.GetDataFromQuery(InPending());
            return HydrateSubmission(table, false);
        }

        public DataTable GetSubmissionStateList()
        {
            return _repository.GetDataFromQuery(SubmissionStateList());
        }

        public DataTable GetSubmissionStatus(string submissionId)
        {
            var table = _repository.GetDataFromQuery(SubmissionStatus(submissionId));
            return table;
        }

        public bool ChangeState(string submissionID, string stateID, string oldStateID, bool transaction = true)
        {
            DataTable stateTable = GetSubmissionStateList();
            DataRow[] rows1 = stateTable.Select("SubmissionStateID = '" + stateID + "'");
            DataRow[] rows2 = stateTable.Select("SubmissionStateID = '" + oldStateID + "'");
            if (rows1.Length == 0 || rows2.Length == 0)
                return false;

            bool result = false;
            string newStateName = rows1[0]["Name"].ToString().Trim();
            string oldStateName = rows2[0]["Name"].ToString().Trim();
            string description = string.Format("Submission State\n    old: {0}\n    new: {1}", oldStateName, newStateName);
            // update submission's status and insert track data into SubmissionTransaction
            SqlParameter[] paraUpdate = new SqlParameter[2];
            paraUpdate[0] = new SqlParameter("@StateID", stateID);
            paraUpdate[1] = new SqlParameter("@SubmissionID", submissionID);

            SqlParameter[] paraInsert = new SqlParameter[2];
            paraInsert[0] = new SqlParameter("@submissionID", submissionID);
            paraInsert[1] = new SqlParameter("@description", description);
            try
            {
                _repository.RunNonQuery(ChangeState(), paraUpdate);
                if (transaction)
                {
                    _repository.RunNonQuery(TrackStatusChange(), paraInsert);
                }
                result = true;
            }
            catch (Exception e)
            {
                result = false;
                throw e;
            }

            return result;
        }

        private List<Submission> HydrateSubmission(DataTable table, bool withTransactions)
        {
            
            var submissions = new Dictionary<int, Submission>();
            foreach (DataRow row in table.Rows)
            {
                int submissionID = Int32.Parse(GetValueFromRow(table, row, "SubmissionID"));
                if (!submissions.ContainsKey(submissionID))
                {
                    string nickName = GetValueFromRow(table, row, "NickName");
                    string version = GetValueFromRow(table, row, "Version");
                    string imageGuid = GetValueFromRow(table, row, "ImageGUID");
                    //var currentState = (TransactionState)Enum.Parse(typeof(TransactionState), GetValueFromRow(table, row, "State").Replace(" ", ""));
                    string currentState = GetValueFromRow(table, row, "State");
                    string stateID = GetValueFromRow(table, row, "StateID");
                    DateTime createdAt = DateTime.MinValue;
                    try
                    {
                        createdAt = DateTime.Parse(GetValueFromRow(table, row, "Created"));
                    }
                    catch (Exception){}
                    
                    var transactions = new List<Transaction>();

                    var sub = new Submission
                    {
                        CreatedDate = createdAt,
                        CurrentState = currentState,
                        StateID = stateID,
                        Version = version,
                        SubmissionId = submissionID,
                        NickName = nickName,
                        Transactions = transactions,
                        ImageGuid = imageGuid
                    };

                    submissions.Add(submissionID, sub);
                }

                if (withTransactions)
                {
                    string stateDescription = GetValueFromRow(table, row, "Description");
                    if (!string.IsNullOrEmpty(stateDescription))
                    {
                        DateTime recordedAt = DateTime.Parse(GetValueFromRow(table, row, "RecordedAt"));

                        var transaction = new Transaction(recordedAt, stateDescription);
                        submissions[submissionID].Transactions.Add(transaction);
                    }
                }
            }
            return submissions.Values.OrderByDescending(s => s.CreatedDate).ToList(); ;
        }


        private string GetValueFromRow(DataTable table, DataRow row, string column)
        {
            return row[GetIndexOfRow(table, column)].ToString();
        }


        private int GetIndexOfRow(DataTable table, string column)
        {
            int index = 0;
            foreach (var c in table.Columns)
            {
                if (c.ToString().ToLowerInvariant() == column.ToLowerInvariant())
                    return index;
                index++;
            }
            return -1;
        }

        public static string SubmissionDetails(string submissionId, string version)
        {
            var query = string.Format(
                @"SELECT s.SubmissionID
                  ,[Nickname]
                  ,[Version]
                  ,st.Name as State
                  ,ss.SubmissionStateID StateID
                  ,t.Description
                  ,t.RecordedAt
                  ,s.Created
                  ,poa.ImageGUID
              FROM [MSCOMWeb].[dbo].[Submissions] s
              LEFT OUTER JOIN [MSCOMWeb].[dbo].[ProductOrAppImages] poa ON poa.ImageID = s.LogoID
              LEFT OUTER JOIN [MSCOMWeb].[dbo].[SubmissionsStatus] ss ON ss.SubmissionID = s.SubmissionID
              LEFT OUTER JOIN [MSCOMWeb].[dbo].[SubmissionStates] st ON st.SubmissionStateID = ss.SubmissionStateID
              LEFT OUTER JOIN [MSCOMWeb].[dbo].[SubmissionTransactions] t ON t.SubmissionID = s.SubmissionID and t.RecordedAt BETWEEN (GETDATE()-60) AND (GETDATE())
              Where 
s.NickName = '{0}' AND Version='{1}'
and s.SubmissionID in (SELECT [SubmissionID] 
from [MSCOMWeb].[dbo].[SubmissionTransactions] 
where [Description] like '%New Submission%') 
              Order by s.Created DESC, t.RecordedAt DESC
            ", submissionId, version);

            return query;
        }

        public static string InTesting()
        {
            var query = string.Format(
                @"SELECT s.SubmissionID
                      ,[Nickname]
                      ,[Version]
                      ,st.Name as State
                      ,ss.SubmissionStateID StateID
                  ,s.Created
                  ,poa.ImageGUID
              FROM [MSCOMWeb].[dbo].[Submissions] s
              LEFT OUTER JOIN [MSCOMWeb].[dbo].[ProductOrAppImages] poa ON poa.ImageID = s.LogoID
              LEFT OUTER JOIN [MSCOMWeb].[dbo].[SubmissionsStatus] ss ON ss.SubmissionID = s.SubmissionID
              LEFT OUTER JOIN [MSCOMWeb].[dbo].[SubmissionStates] st ON st.SubmissionStateID = ss.SubmissionStateID                
                  WHERE
                  st.Name = 'Testing'
                  AND [Nickname] IS NOT NULL

                  Order by s.Created DESC
            ");
            return query;
        }

        public static string InPending()
        {
            var query = string.Format(
                @"SELECT s.SubmissionID
                      ,[Nickname]
                      ,[Version]
                      ,st.Name as State
                      ,ss.SubmissionStateID StateID
                  ,s.Created
                  ,poa.ImageGUID
              FROM [MSCOMWeb].[dbo].[Submissions] s
              LEFT OUTER JOIN [MSCOMWeb].[dbo].[ProductOrAppImages] poa ON poa.ImageID = s.LogoID
              LEFT OUTER JOIN [MSCOMWeb].[dbo].[SubmissionsStatus] ss ON ss.SubmissionID = s.SubmissionID
              LEFT OUTER JOIN [MSCOMWeb].[dbo].[SubmissionStates] st ON st.SubmissionStateID = ss.SubmissionStateID                
                  WHERE
                  st.Name in ('Pending Review', 'Ready To Publish', 'Hold')
                  AND [Nickname] IS NOT NULL

                  Order by s.Created DESC
            ");
            return query;
        }

        public static string ChangeState()
        {
            var query = string.Format(
                @"Update
                        [MSCOMWeb].[dbo].[SubmissionsStatus]
                    Set [SubmissionStateID] = @StateID
                    Where [SubmissionID] = @SubmissionID
                ");
            return query;
        }

        public static string SubmissionStateList()
        {
            var query = string.Format(
                @"Select
                        [SubmissionStateID]
                        ,[Name]
                    From [MSCOMWeb].[dbo].[SubmissionStates]
                    Order by [SubmissionStateID]
                ");
            return query;
        }

        public static string SubmissionStatus(string submissionsId)
        {
            var query = string.Format(
                @"Select
                        [SubmissionStateID]
                    From
                        [MSCOMWeb].[dbo].[Submissions] s
                        ,[MSCOMWeb].[dbo].[SubmissionsStatus] ss
                    Where
                        s.[SubmissionID] = {0}
                        And s.[SubmissionID] = ss.[SubmissionID]
                ", submissionsId);
            return query;
        }

        public static string TrackStatusChange()
        {
            var query = string.Format(
                @"Insert into 
                    [MSCOMWeb].[dbo].[SubmissionTransactions](
                        [SubmissionID]
                        ,[SubmissionTaskID]
                        ,[Description]
                        ,[RecordedAt]
                        )
                  Values(
                        @submissionID
                        ,'1'
                        ,@description
                        ,GETDATE()
                        )
                ");
            return query;
        }

    }
}