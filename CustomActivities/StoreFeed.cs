using System.Activities;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using CustomActivities.Properties;
using Model;

namespace CustomActivities
{
    public sealed class StoreFeed : CodeActivity
    {
        public InArgument<Model.Submission> Submission { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Submission submission = context.GetValue(this.Submission);
            string file = AppSettings.Default.FeedDirectory + "\\" + submission.TransactionId + ".xml";
            XmlDocument xml = new XmlDocument();
            xml.LoadXml(submission.Feed);
            File.WriteAllText(file, "");
            using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                SaveXmlDocWithFormatting(xml, stream);
            }
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
    }
}
