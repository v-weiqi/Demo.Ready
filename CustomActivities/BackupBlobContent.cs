using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Activities;
using CustomActivities.Properties;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using Model;
using XmlGenCore;

namespace CustomActivities
{

    public sealed class BackupBlobContent : CodeActivity<Model.Submission>
    {
        public InArgument<Model.Submission> Submission { get; set; }
        public InArgument<int> StepId { get; set; }
        public InArgument<string> BlobStorageType { get; set; }
        string dateFormat = "yyyy-MM-dd_hh-mm-ss-tt";

        protected override Model.Submission Execute(CodeActivityContext context)
        {
            var stepId = context.GetValue(StepId);
            var submission = context.GetValue(this.Submission);
            var blobstorageType = context.GetValue(BlobStorageType);
            SubmissionStatus status;
            status = new SubmissionStatus { Date = DateTime.Now, Pass = true };

            try
            {
                //initialize storageaccount infor
                string account, feedContainer, packageContainer, backPath;
                if (blobstorageType == "Prod")
                {
                    account = AppSettings.Default.ProdBlobStorage;
                    feedContainer = AppSettings.Default.ProdFeedContainer;
                    packageContainer = AppSettings.Default.ProdPackageContainer;
                    backPath = AppSettings.Default.ProdBackupPath;
                }
                else
                {
                    account = AppSettings.Default.TC2BlobStorage;
                    feedContainer = AppSettings.Default.TC2FeedContainer;
                    packageContainer = AppSettings.Default.TC2PackageContainer;
                    backPath = AppSettings.Default.TC2BackupPath;
                }

                try
                {
                    var storageAccount = CloudStorageAccount.Parse(account);

                    var blobClient = storageAccount.CreateCloudBlobClient();

                    string directoryName = string.Format("backup-{0}", DateTime.Now.ToString(dateFormat));
                    string backupPath = Path.Combine(backPath, directoryName);
                    submission.BackupLocation = backupPath;
                    Directory.CreateDirectory(backupPath);
                    string packagePath = backupPath + "//packages";
                    Directory.CreateDirectory(packagePath);
                    string feedPath = backupPath + "//appfeed";
                    Directory.CreateDirectory(feedPath);

                    var container = blobClient.GetContainerReference(packageContainer);

                    foreach (var item in container.ListBlobs(null))
                    {
                        if (item.GetType() == typeof(CloudBlockBlob))
                        {
                            var blob = (CloudBlockBlob)item;

                            using (var fileStream = File.Open(packagePath + "//" + blob.Name, FileMode.Create))
                            {
                                blob.DownloadToStream(fileStream);
                            }
                        }
                    }

                    container = blobClient.GetContainerReference(feedContainer);
                    foreach (var item in container.ListBlobs(null))
                    {
                        if (item.GetType() == typeof(CloudBlockBlob))
                        {
                            var blob = (CloudBlockBlob)item;

                            using (var fileStream = File.Open(feedPath + "//" + blob.Name, FileMode.Create))
                            {
                                blob.DownloadToStream(fileStream);
                            }
                        }
                    }

                    DeleteOldBackups(backPath);

                    status.Status = blobstorageType + " Backing up blob content: " + backupPath;


                }
                catch (Exception ex)
                {
                    status.Pass = false;
                    status.Log = ex.Message + "\n\n";
                    status.Log += ex.StackTrace;
                }

                submission.Statuses.Add(status);
                LogMessage.AddStatus(submission.TransactionId, stepId, status);
            }
            catch
            {
            }

            return submission;

        }

        private void DeleteOldBackups(string directortyPath)
        {
            var directories = Directory.GetDirectories(directortyPath, "backup-*");
            var backupCount = Int32.Parse(AppSettings.Default.BackupDirectoryCount);


            if (directories.Count() < backupCount)
                return;
            
            var dates = new List<DateTime>();

            foreach (var directory in directories)
            {
                var dateString = directory.Replace("\\backup-", "").Replace(directortyPath, "");
                DateTime theDate;
                DateTime.TryParseExact(dateString, dateFormat,
                                        CultureInfo.InvariantCulture, DateTimeStyles.None, out theDate);
                dates.Add(theDate);
            }

            dates.Sort();

            for (int index = 0; index < (dates.Count - backupCount); index++)
            {
                var dateTime = dates[index];
                string directoryName = string.Format("backup-{0}", dateTime.ToString(dateFormat));
                string backupPath = Path.Combine(directortyPath, directoryName);
                Directory.Delete(backupPath, true);
            }
        }

    }
}
