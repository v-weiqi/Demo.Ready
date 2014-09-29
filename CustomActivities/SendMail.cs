using System;
using System.Activities;
using System.IO;
using System.Net.Mail;
using System.Text;
using CustomActivities.Properties;
using Model;

namespace CustomActivities
{
    public sealed class SendMail : CodeActivity
    {
        public InArgument<string> ToAddress { get; set; }
        public InArgument<string> FromAddress { get; set; }
        public InArgument<string> Subject { get; set; }
        public InArgument<string> MailBody { get; set; }
        public InArgument<Model.Submission> Submission { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Submission submission = context.GetValue(Submission);
            
            string toAddress = ToAddress.Get(context);
            string mailBody = "";

            mailBody += MailBody.Get(context) + "\r\n\n";

            mailBody += "STATUS:\r\n\n";

            mailBody += "Tranasaction Id: " + submission.TransactionId + "\r\n\n";
            foreach (SubmissionStatus status in submission.Statuses)
            {
                mailBody += string.Format("{0}:{1}\r\n\n", status.Date, status.Status);
            }

            mailBody += "\r\n\n";

            var msg = new MailMessage(
                AppSettings.Default.AdminEmail,
                AppSettings.Default.VendorEmail,
                Subject.Get(context),
                mailBody
                );

            byte[] byteArray = Encoding.ASCII.GetBytes(submission.Feed);
            var stream = new MemoryStream(byteArray);
            var atch = new Attachment(stream, "feed.xml", "text/xml");
            msg.Attachments.Add(atch);

            var client = new SmtpClient("smtphost");

            client.UseDefaultCredentials = true;


            try
            {
                client.Send(msg);
            }

            catch (Exception ex)
            {
                Console.WriteLine("Exception caught in Send(): {0}", ex);
            }
        }
    }
}
