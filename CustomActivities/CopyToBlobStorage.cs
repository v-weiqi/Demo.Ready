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
    public sealed class CopyToBlobStorage : CodeActivity<bool>
    {
        public InArgument<Model.Submission> Submission { get; set; }
        public InArgument<int> StepId { get; set; }
        public InArgument<string> BlobStorageType { get; set; }

        protected override bool Execute(CodeActivityContext context)
        {
            var submission = context.GetValue(Submission);
            var stepId = context.GetValue(StepId);
            var blobStorageType = context.GetValue(BlobStorageType);
            SubmissionStatus status;

            try
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse((blobStorageType=="Prod")?AppSettings.Default.ProdBlobStorage:AppSettings.Default.TC2BlobStorage);

                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                CloudBlobContainer container = null;

                for (int index = 0; index < submission.LiveInstallerUrls.Count; index++)
                {
                    try
                    {
                        var url = submission.LiveInstallerUrls[index];

                        string fileName = "";

                        if (blobStorageType == "Prod")
                        {
                            var testUrl = submission.AzureInstallerUrls[index];
                            fileName = testUrl.Substring(testUrl.LastIndexOf('/') + 1);
                            container = blobClient.GetContainerReference(AppSettings.Default.ProdPackageContainer);
                        }
                        else
                        {
                            var testUrl = submission.TC2InstallerUrls[index];
                            fileName = testUrl.Substring(testUrl.LastIndexOf('/') + 1);
                            container = blobClient.GetContainerReference(AppSettings.Default.TC2PackageContainer);

                        }



                        CloudBlockBlob blockBlob = container.GetBlockBlobReference(AppSettings.Default.TestPackagePrefix + fileName);
                        DownloadHelper.DownloadFile(new Uri(url), AppSettings.Default.TempDownloadDirectory);
                        using (var fileStream = File.OpenRead(AppSettings.Default.TempDownloadDirectory + "/" + fileName))
                        {
                            blockBlob.UploadFromStream(fileStream);
                        }
                        File.Delete(AppSettings.Default.TempDownloadDirectory + "/" + fileName);
                        break;
                    }
                    catch (Exception)
                    {
                    }

                }

                status = new SubmissionStatus { Status = "Copy Package to " + blobStorageType + " blob storage", Date = DateTime.Now, Pass = true };

            }
            catch (Exception ex)
            {
                status = new SubmissionStatus { Status = "Copy Package to " + blobStorageType + " blob storage", Date = DateTime.Now, Pass = false, Log = ex.Message };
            }

            submission.Statuses.Add(status);
            LogMessage.AddStatus(submission.TransactionId, stepId, status);

            return status.Pass;



        }
    }
}
