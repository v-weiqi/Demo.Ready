using System;
using System.Activities;
using CustomActivities.Properties;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using Model;

namespace CustomActivities
{

    public sealed class PublishFeedOnTC2 : CodeActivity<Model.Submission>
    {
        public InArgument<Model.Submission> Submission { get; set; }
        public InArgument<int> StepId { get; set; }

        protected override Submission Execute(CodeActivityContext context)
        {
            var submission = context.GetValue(Submission);
            var stepId = context.GetValue(StepId);
            SubmissionStatus status;

            try
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(AppSettings.Default.TC2BlobStorage);
                
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                CloudBlobContainer container = blobClient.GetContainerReference(AppSettings.Default.TC2FeedContainer);

                CloudBlockBlob blockBlob = container.GetBlockBlobReference(AppSettings.Default.TC2FeedName);

                using (var fileStream = System.IO.File.OpenRead(AppSettings.Default.FeedDirectory + "\\" + submission.TransactionId + ".xml"))
                {
                    blockBlob.UploadFromStream(fileStream);
                }

                status = new SubmissionStatus { Status = "Feed published to TC2", Date = DateTime.Now, Pass = true };
                
            }
            catch (Exception ex)
            {
                status = new SubmissionStatus { Status = "Feed published to TC2", Date = DateTime.Now, Pass = false, Log = ex.Message };
            }


            submission.Statuses.Add(status);
            LogMessage.AddStatus(submission.TransactionId, stepId, status);





            return submission;
        }
    }
}
