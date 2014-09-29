using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using FmsWeb.Adapter;
using FmsWeb.Model;
using System.Data.SqlClient;

namespace FmsWeb.Controllers
{
    public class VendorController : Controller
    {
                private DbAdapter _adapter;
        private Repository _wfepository;


        public VendorController()
        {
            _adapter = new DbAdapter();
            _wfepository = new WorkFlowRepository();
        }
        //
        // GET: /Vendor/

        public ActionResult Index()
        {
            var vendorlist = _wfepository.GetDataFromQuery(WorkFlowProcs.GetVendorList());
            var vendorDic = new Dictionary<string, string>();
            foreach (DataRow row in vendorlist.Rows)
            {
                vendorDic.Add(row["alias"].ToString(), row["name"].ToString());
            }
            ViewData["VendorList"] = vendorDic;

            var dictionary = new Dictionary<string, AppWorkFlow>();
            var table = _wfepository.GetDataFromQuery(WorkFlowProcs.GetLatestRuns());
            foreach (DataRow row in table.Rows)
            {
                var wfid = row["WorkflowInstanceId"].ToString().Trim();
                var exTable = _wfepository.GetDataFromQuery(WorkFlowProcs.GetLastExecutingMessage(wfid));
                string activity = "";
                string state = "";
                foreach (DataRow wrow in exTable.Rows)
                {
                    activity = wrow["ActivityName"].ToString().Trim();
                    state = wrow["State"].ToString().Trim();
                }

                if (activity == "VendorTC2Response" && state == "Executing")
                {
                    AppWorkFlow awf;
                    var transactionId = row["TransactionId"].ToString().Trim();

                    if (dictionary.ContainsKey(transactionId))
                    {
                        awf = dictionary[transactionId];
                    }
                    else
                    {
                        awf = new AppWorkFlow();
                        awf.AppId = row["AppId"].ToString().Trim();
                        awf.AppVersion = row["AppVersion"].ToString().Trim();
                        awf.WorkflowInstanceId = row["WorkflowInstanceId"].ToString().Trim();
                        awf.TimeCreated = (DateTime) row["TimeCreated"];
                        awf.TransactionId = transactionId;
                        awf.AssignedTo = row["AssignedTo"].ToString().Trim();
                        if (awf.AssignedTo == string.Empty)
                        {
                            awf.AssignedTo = "None";
                        }
                        dictionary.Add(transactionId, awf);
                    }

                    awf.Status.Add(new Model.SubmissionStatus()
                        {
                            Date = (DateTime) row["datecreated"],
                            Pass = (bool) row["passed"],
                            Status = row["status"].ToString()
                        });
                }
            }

            ViewData["LastWorkFlowRuns"] = dictionary;

            var completedRunsTable = _wfepository.GetDataFromQuery(WorkFlowProcs.GetCompletedRuns());
            var historyDictionary = new Dictionary<string, AppWorkFlow>();
            
            foreach (DataRow row in completedRunsTable.Rows)
            {
                AppWorkFlow hawf = new AppWorkFlow();
                        hawf.AppId = row["AppId"].ToString().Trim();
                        hawf.AppVersion = row["AppVersion"].ToString().Trim();
                        hawf.WorkflowInstanceId = row["WorkflowInstanceId"].ToString().Trim();
                        hawf.TimeCreated = (DateTime) row["TimeCreated"];
                        hawf.Reason = row["Reason"].ToString ();



                        historyDictionary.Add(hawf.WorkflowInstanceId, hawf);
            }

            ViewData["CompletedRuns"] = historyDictionary;

            var testedRunsTable = _wfepository.GetDataFromQuery(WorkFlowProcs.GetTestedRuns());
            var testedRuns = new Dictionary<string, AppWorkFlow>();
            foreach (DataRow row in testedRunsTable.Rows)
            {
                AppWorkFlow tawf = new AppWorkFlow();
                tawf.AppId = row["AppId"].ToString().Trim();
                tawf.AppVersion = row["AppVersion"].ToString().Trim();
                tawf.WorkflowInstanceId = row["WorkflowInstanceId"].ToString().Trim();
                tawf.AssignedTo = row["AssignedTo"].ToString();
                tawf.Failed = !(bool)row["passed"];
                tawf.TimeCreated = (DateTime)row["TimeCreated"];
                tawf.Reason = row["FailReason"].ToString();

                testedRuns.Add(tawf.WorkflowInstanceId, tawf);
            }

            ViewData["TestedRuns"] = testedRuns;

            return View(_adapter.GetSubmissionsInTesting());
        }

        public ActionResult AppDetail(string appId, string version)
        {
            var submission = _adapter.GetSubmissionAndTransaction(appId, version);
            return View(submission);
        }

        public ActionResult AssignTask(string appID, string version, string workFlowInstanceID, string assignTo)
        {
            var myparm = new SqlParameter[4];
            myparm[0] = new SqlParameter("@appID", appID);
            myparm[1] = new SqlParameter("@version", version);
            myparm[2] = new SqlParameter("@workFlowInstanceID", workFlowInstanceID);
            myparm[3] = new SqlParameter("@assignTo", assignTo);
            _wfepository.RunNonQuery(WorkFlowProcs.UpdateAssignment(), myparm);
            return RedirectToAction("Index", "Vendor");
        }

    }
}
