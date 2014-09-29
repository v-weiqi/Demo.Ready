using System;
using System.Collections.Generic;
using System.IO;
using System.Activities;
using System.Xml;
using Model;
using XmlGenCore;

namespace CustomActivities
{

    public sealed class GenerateProdFeed : CodeActivity<Model.Submission>
    {
        public InArgument<Model.Submission> Submission { get; set; }
        public InArgument<int> StepId { get; set; }

        protected override Model.Submission Execute(CodeActivityContext context)
        {
            var submission = context.GetValue(Submission);
            var stepId = context.GetValue(StepId);

            CoreGenerationSettings settings = new CoreGenerationSettings();
            settings.FeedGenerationType = FeedType.PROD;
            settings.IncludeTestingApps = true;
            settings.MarkTestAppsAzureReady = true;
            settings.TestAppIdFilter = new List<string> { submission.AppId };

            var xmlDoc = FeedInterface.GenerateFeed(settings);

            XmlWriterSettings xmlSettings = new XmlWriterSettings();
            xmlSettings.Indent = true;
            xmlSettings.IndentChars = "  ";
            xmlSettings.NewLineChars = "\r\n";
            xmlSettings.NewLineHandling = NewLineHandling.Replace;

            using (var stringWriter = new StringWriter())
            using (var xmlTextWriter = XmlWriter.Create(stringWriter, xmlSettings))
            {
                xmlDoc.WriteTo(xmlTextWriter);
                xmlTextWriter.Flush();
                submission.Feed = stringWriter.GetStringBuilder().ToString();
            }

            var status = new SubmissionStatus
            {
                Status = "Generating Prod Feed",
                Date = DateTime.Now,
                Pass = true
            };
            submission.Statuses.Add(status);
            LogMessage.AddStatus(submission.TransactionId, stepId, status);
            submission.AzureInstallerUrls = FeedInterface.GetInstallers(xmlDoc, submission.AppId);
            return submission;
        }
    }
}
