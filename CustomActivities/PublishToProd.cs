using System;
using System.Activities;
using System.IO;
using CustomActivities.Properties;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using Model;
using XmlGenCore;

namespace CustomActivities
{

    public sealed class PublishToProd : CodeActivity<Model.Submission>
    {
        public InArgument<Model.Submission> Submission { get; set; }
        public InArgument<int> StepId { get; set; }

        protected override Model.Submission Execute(CodeActivityContext context)
        {
            var submission = context.GetValue(Submission);
            var stepId = context.GetValue(StepId);

            var status = new SubmissionStatus { Status = "Feed published to Prod", Date = DateTime.Now, Pass = true };

            try
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(AppSettings.Default.ProdBlobStorage);

                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                CloudBlobContainer container = null;

                string fileName = AppSettings.Default.ProdFeedName;
                container = blobClient.GetContainerReference(AppSettings.Default.ProdFeedContainer);
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);
                using (var fileStream = System.IO.File.OpenRead(AppSettings.Default.FeedDirectory + "\\" + submission.TransactionId + ".xml"))
                {
                    blockBlob.UploadFromStream(fileStream);
                }
            }
            catch (Exception ex)
            {
                status.Pass = false;
                status.Log = ex.Message;
            }

            
            submission.Statuses.Add(status);
            LogMessage.AddStatus(submission.TransactionId, stepId, status);
            return submission;
        }
    }
}