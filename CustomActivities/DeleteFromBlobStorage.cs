using System;
using System.Activities;
using CustomActivities.Properties;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using Model;

namespace CustomActivities
{
    public sealed class DeleteFromBlobStorage : CodeActivity
    {
        public InArgument<Model.Submission> Submission { get; set; }
        public InArgument<int> StepId { get; set; }
        public InArgument<string> BlobStorageType { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var submission = context.GetValue(Submission);
            var stepId = context.GetValue(StepId);
            var blobStorageType = context.GetValue(BlobStorageType);
            SubmissionStatus status;

            try
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse((blobStorageType == "Prod") ? AppSettings.Default.ProdBlobStorage : AppSettings.Default.TC2BlobStorage);

                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                CloudBlobContainer container = null;

                for (int index = 0; index < submission.LiveInstallerUrls.Count; index++)
                {
                    string fileName = AppSettings.Default.TestPackagePrefix;
                    if (blobStorageType == "Prod")
                    {
                        var testUrl = submission.AzureInstallerUrls[index];
                        fileName += testUrl.Substring(testUrl.LastIndexOf('/') + 1);
                        container = blobClient.GetContainerReference(AppSettings.Default.ProdPackageContainer);
                        CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);
                        blockBlob.DeleteIfExists();
                    }
                    else
                    {
                        var testUrl = submission.TC2InstallerUrls[index];
                        fileName += testUrl.Substring(testUrl.LastIndexOf('/') + 1);
                        container = blobClient.GetContainerReference(AppSettings.Default.TC2PackageContainer);

                        //get it out of here
                        CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);
                        blockBlob.DeleteIfExists();

                    }

                    
                }

                status = new SubmissionStatus { Status = "Delete Package from " + blobStorageType, Date = DateTime.Now, Pass = true };

            }
            catch (Exception ex)
            {
                status = new SubmissionStatus { Status = "Delete Package from " + blobStorageType, Date = DateTime.Now, Pass = false, Log = ex.Message };
            }

            submission.Statuses.Add(status);
            LogMessage.AddStatus(submission.TransactionId, stepId, status);


        }
    }
}
