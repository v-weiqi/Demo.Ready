using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Security.Principal;
using System.Web.Configuration;
using System.Web.Mvc;
using System.Xml;
using System.Xml.XPath;
using FmsWeb.Adapter;
using FmsWeb.FmsService;
using FmsWeb.Model;

namespace FmsWeb.Controllers
{
    //[Authorize(Users = @"redmond\yaminij, redmond\askamboj, ntdev\dariac, redmond\benbyrd, redmond\sumuth")]
    public class FMSController : Controller
    {
        private DbAdapter _adapter;
        private Repository _wfepository;


        public FMSController()
        {
            _adapter = new DbAdapter();
            _wfepository = new WorkFlowRepository();
        }

        public ActionResult Index()
        {
            var dictionary = new Dictionary<string, AppWorkFlow>();
            var table = _wfepository.GetDataFromQuery(WorkFlowProcs.GetLatestRuns());
            foreach (DataRow row in table.Rows)
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
                    awf.TimeCreated = (DateTime)row["TimeCreated"];
                    awf.TransactionId = transactionId;
                    dictionary.Add(transactionId,awf);
                }

                awf.Status.Add(new Model.SubmissionStatus() { Date = (DateTime)row["datecreated"], Pass = (bool)row["passed"], Status = row["status"].ToString() });
            }

            ViewData["LastWorkFlowRuns"] = dictionary;
            List<FmsWeb.Model.Submission> pendingSubmissions = _adapter.GetSubmissionsPending();

            ViewData["SubmissionPending"] = pendingSubmissions;

            var stateListDic = new Dictionary<string, string>();
            var stateList = _adapter.GetSubmissionStateList();
            foreach (DataRow eachRow in stateList.Rows)
            {
                stateListDic.Add(eachRow["SubmissionStateID"].ToString(), eachRow["Name"].ToString());
            }
            ViewData["StateList"] = stateListDic;
           
            return View(_adapter.GetSubmissionsInTesting());
        }

        public ActionResult AppDetail(string appId, string version)
        {
            var submission = _adapter.GetSubmissionAndTransaction(appId, version);
            var list = new List<AppWorkFlow>();
            var table = _wfepository.GetDataFromQuery(WorkFlowProcs.AppAndWorkFLowInstancesSql(appId, version));
            foreach (DataRow row in table.Rows)
            {
                var awf = new AppWorkFlow();
                awf.AppId = row["AppId"].ToString();
                awf.AppVersion = row["AppVersion"].ToString();
                awf.WorkflowInstanceId = row["WorkflowInstanceId"].ToString();
                awf.TimeCreated = (DateTime)row["TimeCreated"];
                list.Add(awf);
            }
            submission.WorkFlows = list;
            return View(submission);
        }

        public JsonResult WorkflowDetail(string workFlowInstanceId)
        {
            var table = _wfepository.GetDataFromQuery(WorkFlowProcs.AppAndWorkFLowInstance(workFlowInstanceId));
            var awf = new AppWorkFlow();
            foreach (DataRow row in table.Rows)
            {

                awf.AppId = row["AppId"].ToString().Trim();
                awf.AppVersion = row["AppVersion"].ToString().Trim();
                awf.TransactionId = row["TransactionId"].ToString().Trim();

                if (!string.IsNullOrEmpty(row["Failed"].ToString()))
                    awf.Failed = (bool)row["Failed"];
                else
                    awf.Failed = false;

                if (!string.IsNullOrEmpty(row["id"].ToString()))
                    awf.FailReason = (FailReason)row["id"];
                
                var feedDir = WebConfigurationManager.OpenWebConfiguration("/").AppSettings.Settings["FeedDir"].Value;
                string file = feedDir + "\\" + awf.TransactionId + ".xml";
                try
                {
                    awf.Feed = System.IO.File.ReadAllText(file);
                }
                catch (Exception ex)
                {
                    
                }
                
                
                awf.WorkflowInstanceId = row["WorkflowInstanceId"].ToString();
                awf.TimeCreated = (DateTime)row["TimeCreated"];
            }
            return Json(awf, JsonRequestBehavior.AllowGet);
        }


        public static void SaveXmlDocWithFormatting(XmlDocument xDoc, Stream fileStream)
        {
            fileStream.Seek(0, SeekOrigin.Begin);
            fileStream.SetLength(0);
            xDoc.Save(fileStream);

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "  ";
            settings.NewLineChars = "\r\n";
            settings.NewLineHandling = NewLineHandling.Replace;
            fileStream.Seek(0, SeekOrigin.Begin);
            xDoc.Load(fileStream);
            fileStream.Seek(0, SeekOrigin.Begin);
            fileStream.SetLength(0);
            XmlWriter fwriter = XmlWriter.Create(fileStream, settings);
            xDoc.Save(fwriter);
            fwriter.Flush();
            fileStream.Flush();
        }

        public JsonResult Start(string appId, string appVersion)
        {
            var client = new FmsClient();
            var submission = client.SubmitAppId(new SubmitAppId {appId = appId, appVersion = appVersion});
            var feed = submission.Feed;
            return Json(submission, JsonRequestBehavior.AllowGet);
        }

        [HttpPost, ValidateInput(false)]
        public JsonResult GenerateAndValidate(string transactionId, string feed)
        {
            var client = new FmsClient();
            FmsService.Submission submission = null;
            using (((WindowsIdentity) User.Identity).Impersonate())
            {
                submission =
                    client.GenerateAndValidate(new UserRequest
                        {
                            User = User.Identity.Name,
                            TransactionId = transactionId
                        });
                if (submission.Failed) UpdateFailStatus(submission.TransactionId, 0);
            }
            return Json(submission, JsonRequestBehavior.AllowGet);
        }

        [HttpPost, ValidateInput(false)]
        public JsonResult ContinueToTc2(string transactionId, string feed)
        {
            var client = new FmsClient();
            FmsService.Submission submission = null;
            try
            {
                submission =
                    client.ContinueToTC2(new UserRequest
                        {
                            User = User.Identity.Name,
                            TransactionId = transactionId,
                            Feed = feed
                        });
                //if (submission.Failed) UpdateFailStatus(submission.TransactionId, 0);
            }
            catch (Exception ex)
            {
                var s= ex.StackTrace;
            }
            
            return Json(submission, JsonRequestBehavior.AllowGet);
        }

        [HttpPost, ValidateInput(false)]
        public JsonResult VendorTc2Response(string transactionId, bool passed, bool discoveryFeedRequiresUpdate)
        {
            var client = new FmsClient();
            FmsService.FmsResponse response = null;
            try
            {
                response =
                    client.VendorTC2Response(new UserRequest
                        {
                            User = User.Identity.Name,
                            TransactionId = transactionId,
                            CloseTransaction = !passed,
                            DiscoveryFeedRequiresUpdate = discoveryFeedRequiresUpdate
                        });
                if (!passed) UpdateFailStatus(transactionId, 0);
            }
            catch (Exception ex)
            {
                var s = ex.StackTrace;
            }

            return Json((response != null) ? response.Submission : null, JsonRequestBehavior.AllowGet);
        }

        [HttpPost, ValidateInput(false)]
        public JsonResult ContinueToProd(string transactionId, string feed)
        {
            var client = new FmsClient();
            FmsService.Submission submission = null;
            try
            {
                submission =
                    client.ContinueToProd(new UserRequest
                    {
                        User = User.Identity.Name,
                        TransactionId = transactionId,
                        Feed = feed
                    });
                //if (submission.Failed) UpdateFailStatus(submission.TransactionId, 0);
            }
            catch (Exception ex)
            {
                var s = ex.StackTrace;
            }
            return Json(submission, JsonRequestBehavior.AllowGet);
        }

        [HttpPost, ValidateInput(false)]
        public JsonResult TakeActionToFix(string transactionId, string feed)
        {
            var client = new FmsClient();
            FmsService.Submission submission = null;
            try
            {
                submission =
                    client.TakeActionToFix(new UserRequest
                    {
                        User = User.Identity.Name,
                        TransactionId = transactionId,
                        Feed = feed
                    });
            }
            catch (Exception ex)
            {
                var s = ex.StackTrace;
            }
            return Json(submission, JsonRequestBehavior.AllowGet);
        }

        [HttpPost, ValidateInput(false)]
        public JsonResult BackupFeedAndPackages(string transactionId, string feed)
        {
            var client = new FmsClient();
            FmsService.Submission submission = null;
            try
            {
                using (((WindowsIdentity)User.Identity).Impersonate())
                {
                    submission =
                    client.BackupFeedAndPackages(new UserRequest
                    {
                        User = User.Identity.Name,
                        TransactionId = transactionId,
                        Feed = feed
                    });
                }

                if (submission.Failed) UpdateFailStatus(submission.TransactionId, 0);
            }
            catch (Exception ex)
            {
                var s = ex.StackTrace;
            }
            return Json(submission, JsonRequestBehavior.AllowGet);
        }

        [HttpPost, ValidateInput(false)]
        public JsonResult PublishToProd(string transactionId, string feed)
        {
            var client = new FmsClient();
            FmsService.Submission submission = null;
            try
            {
                submission =
                    client.PublishToProd(new UserRequest
                    {
                        User = User.Identity.Name,
                        TransactionId = transactionId,
                        Feed = feed
                    });
                if (submission.Failed) UpdateFailStatus(submission.TransactionId, 0);
            }
            catch (Exception ex)
            {
                var s = ex.StackTrace;
            }
            return Json(submission, JsonRequestBehavior.AllowGet);
        }

        private void UpdateFailStatus(string transactionId,  int failReason)
        {
           _wfepository.RunNonQuery(WorkFlowProcs.UpdateFailStatus(transactionId, failReason), null);
        }

        public JsonResult Track(string transactionId)
        {
            var list = new List<WorkFlowTrack>();
            var table = _wfepository.GetDataFromQuery(WorkFlowProcs.TrackingActivitySql(transactionId));
            foreach (DataRow row in table.Rows)
            {
                var wft = new WorkFlowTrack();
                wft.State = row["State"].ToString().Trim();
                wft.ActivityName = row["ActivityName"].ToString().Trim();
                wft.ActivityInstanceId = row["ActivityInstanceId"].ToString().Trim();
                wft.WorkflowInstanceId = row["WorkflowInstanceId"].ToString().Trim();
                wft.TransactionId = row["TransactionId"].ToString().Trim();
                wft.TimeCreated = (DateTime)row["TimeCreated"];
                list.Add(wft);
            }

            return Json(list, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetComments(string transactionId)
        {
            var list = new List<Comment>();
            var table = _wfepository.GetDataFromQuery(WorkFlowProcs.GetComments(transactionId));
            foreach (DataRow row in table.Rows)
            {
                var comment = new Comment();
                comment.Id = Int32.Parse(row["id"].ToString().Trim());
                comment.Alias = row["alias"].ToString().Trim().ToLower().Replace("redmond\\","");
                comment.CommentText = row["comment"].ToString().Trim().Replace("<div", "<span");
                comment.CommentText = comment.CommentText.Replace("div>", "span>");
                comment.TransactionId = row["TransactionId"].ToString().Trim();
                comment.DateCreated = (DateTime)row["DateCreated"];
                list.Add(comment);
            }

            return Json(list, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetStatus(string transactionId)
        {
            var subDict = new Dictionary<string, List<Model.SubmissionStatus>>();
            var table = _wfepository.GetDataFromQuery(WorkFlowProcs.GetStatus(transactionId));

            foreach (DataRow row in table.Rows)
            {
                var stepName = row["DisplayName"].ToString().Trim();
                var status = new Model.SubmissionStatus();
                var statusName = row["Status"].ToString().Trim();
                if (!string.IsNullOrEmpty(statusName))
                {
                    status.Date = (DateTime)row["DateCreated"];
                    status.Status = row["Status"].ToString().Trim();
                    status.Log = row["Log"].ToString().Trim().Replace("\n", "<BR/>").Replace("  at ", "&nbsp;&nbsp;at ");
                    if (status.Status.Contains("Backing up"))
                    {
                        var path = status.Status.Substring(status.Status.IndexOf(":")+1).Trim();
                        status.Status = "Backing up blob content: <a href='file:///" + path + "'>" + path + "</a>";
                    }

                    status.Pass = (bool)row["passed"];
                }


                if (subDict.ContainsKey(stepName))
                {
                    subDict[stepName].Add(status);
                }
                else
                {
                    var list = new List<Model.SubmissionStatus>();
                    if (!string.IsNullOrEmpty(statusName))
                        list.Add(status);
                    subDict[stepName] = list;
                }
            }

            return Json(subDict, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetSubmissionStatus(string submissionId)
        {
            string statusId = string.Empty;
            var data = _adapter.GetSubmissionStatus(submissionId);
            if (data.Rows.Count > 0)
            {
                statusId = data.Rows[0]["SubmissionStateID"].ToString().Trim();
            }
            return Json(statusId, JsonRequestBehavior.AllowGet);
        }

        [HttpPost, ValidateInput(false)]
        public EmptyResult UpdateState(string submissionID, string stateID, string oldStateID, bool transaction = true)
        {
            _adapter.ChangeState(submissionID, stateID, oldStateID, transaction);
            return null;
        }

        public JsonResult GetXml(string url)
        {
            WebRequest request = HttpWebRequest.Create(url);
            WebResponse response = request.GetResponse();
            StreamReader reader = new StreamReader(response.GetResponseStream());
            string text = reader.ReadToEnd();

            return Json(text, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetFaults(string transactionId)
        {
            var list = new List<Fault>();
            var table = _wfepository.GetDataFromQuery(WorkFlowProcs.GetFaults(transactionId));
            foreach (DataRow row in table.Rows)
            {
                var fault = new Fault();
                fault.TimeCreated = (DateTime)row["TimeCreated"];
                fault.ActivityName = row["ActivityName"].ToString().Trim();
                fault.FaultDetails = row["FaultDetails"].ToString().Trim().Replace("\n", "<BR/>").Replace("  at ", "&nbsp;&nbsp;at ");
                list.Add(fault);
            }

            return Json(list, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetMessages(string transactionId)
        {
            var list = new List<string>();
            return Json(list, JsonRequestBehavior.AllowGet);
        }

        [HttpPost, ValidateInput(false)]
        public EmptyResult AddComment(string transactionId, string comment)
        {
            if (string.IsNullOrEmpty(comment.Trim()))
                return null;

            var myparm = new SqlParameter[3];
            myparm[0] = new SqlParameter("@Alias", User.Identity.Name);
            myparm[1] = new SqlParameter("@Comment", comment);
            myparm[2] = new SqlParameter("@TransactionId", transactionId);
            _wfepository.RunNonQuery(WorkFlowProcs.AddComment(), myparm);
            return null;
        }

        [HttpPost, ValidateInput(false)]
        public EmptyResult EditComment(string transactionId, string commentID, string comment)
        {
            if (string.IsNullOrEmpty(comment.Trim()))
                return null;

            var myparm = new SqlParameter[3];
            myparm[0] = new SqlParameter("@CommentID", commentID);
            myparm[1] = new SqlParameter("@Comment", comment);
            myparm[2] = new SqlParameter("@TransactionId", transactionId);
            _wfepository.RunNonQuery(WorkFlowProcs.EditComment(), myparm);
            return null;
        }

        [HttpPost, ValidateInput(false)]
        public EmptyResult DeleteComment(string transactionId, string commentID)
        {
            var myparm = new SqlParameter[2];
            myparm[0] = new SqlParameter("@CommentID", commentID);
            myparm[1] = new SqlParameter("@TransactionId", transactionId);
            _wfepository.RunNonQuery(WorkFlowProcs.DeleteComment(), myparm);
            return null;
        }

        [HttpGet, ValidateInput(false)]
        public EmptyResult AddFailReason(string transactionId, int failReason)
        {
            UpdateFailStatus(transactionId, failReason);
            return null;
        }


        public ActionResult ChangeState(string submissionID, string stateID, string oldStateID)
        {
            _adapter.ChangeState(submissionID, stateID, oldStateID);
            return RedirectToAction("Index", "FMS");
        }
    }
}
