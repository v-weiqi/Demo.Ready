using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace AppGallery.SIR
{
    public class Reporter : ILog
    {
        #region Constants

        protected const string PACKAGE_ELEMENT = "package";        
        protected const string MD5_ELEMENT = "MD5";
        protected const string SHA_ELEMENT = "SHA";
        protected const string SHA512_ELEMENT = "SHA512";
        protected const string VALIDATION_RESULT_ELEMENT = "validationResult";
        protected const string APPURL_ELEMENT = "appUrl";
        protected const string ROOT_REPORT_ELEMENT = "report";
        protected const string DATE_ELEMENT = "date";
        protected const string VERSION_ELEMENT = "version";
        protected const string EVENTS_ELEMENT = "events";
        protected const string EVENT_ELEMENT = "event";
        protected const string EVENT_TYPE_ATTRIBUTE = "type";
        protected const string LOCATION_ATTRIBUTE = "location";
        protected const string TARGET_ATTRIBUTE = "target";
        protected const string EVENT_TIME_OCCURRED_ATTRIBUTE = "timeOccurred";        
        
        protected const string XslFileLocation = "http://www.iis.net/downloads/files/SIR/report.xsl";

        #endregion
    
        protected PackageValidationManager _packageValidationManager;

        public Reporter(PackageValidationManager packageValidationManager)
        {
            _packageValidationManager = packageValidationManager;
        }

        #region Methods

        public override string GenerateReportFileName()
        {
            return (Package.Current != null ? Package.Current.PackageFriendlyName : "UnknownPackage") + "_" + Helper.GetTimeStampString(DateTime.Now) + ".xml";
        }

        public override void GenerateLog()
        {
            XmlTextWriter reportWriter = new XmlTextWriter(ReportFullLocation, null);
            reportWriter.Formatting = Formatting.Indented;
            reportWriter.Indentation = 3;
            reportWriter.WriteStartDocument();
            reportWriter.WriteProcessingInstruction("xml-stylesheet", string.Format("type='text/xsl' href='{0}'", XslFileLocation));
            reportWriter.WriteStartElement(ROOT_REPORT_ELEMENT);
            reportWriter.WriteStartElement(DATE_ELEMENT);
            reportWriter.WriteString(DateTime.Now.ToString());
            reportWriter.WriteEndElement();
            reportWriter.WriteStartElement(VERSION_ELEMENT);
            reportWriter.WriteString(Helper.AssemblyVersion);
            reportWriter.WriteEndElement();
            reportWriter.WriteStartElement(VALIDATION_RESULT_ELEMENT);
            reportWriter.WriteString(_validationResult.ToString());
            reportWriter.WriteEndElement();
            if (Package.Current != null)
            {
                if (!string.IsNullOrEmpty(Package.Current.MD5Hash))
                {
                    reportWriter.WriteStartElement(MD5_ELEMENT);
                    reportWriter.WriteString(Package.Current.MD5Hash);
                    reportWriter.WriteEndElement();
                }
                if (Package.Current != null && !string.IsNullOrEmpty(Package.Current.SHA1Hash))
                {
                    reportWriter.WriteStartElement(SHA_ELEMENT);
                    reportWriter.WriteString(Package.Current.SHA1Hash);
                    reportWriter.WriteEndElement();
                }
                if (Package.Current != null && !string.IsNullOrEmpty(Package.Current.SHA512Hash))
                {
                    reportWriter.WriteStartElement(SHA512_ELEMENT);
                    reportWriter.WriteString(Package.Current.SHA512Hash);
                    reportWriter.WriteEndElement();
                }
            }
            if (!string.IsNullOrEmpty(AppUrl))
            {
                reportWriter.WriteStartElement(APPURL_ELEMENT);
                reportWriter.WriteString(AppUrl);
                reportWriter.WriteEndElement();
            }
            reportWriter.WriteStartElement(PACKAGE_ELEMENT);
            reportWriter.WriteString(!string.IsNullOrEmpty(PackageLocation) ? PackageLocation : "unknown package");
            reportWriter.WriteEndElement();

            if (Events.Count > 0)
            {
                reportWriter.WriteStartElement(EVENTS_ELEMENT);

                foreach (LogEvent logEvent in Events)
                {
                    reportWriter.WriteStartElement(EVENT_ELEMENT);
                    reportWriter.WriteStartAttribute(EVENT_TYPE_ATTRIBUTE);
                    reportWriter.WriteString(logEvent.Type.ToString());
                    reportWriter.WriteEndAttribute();
                    reportWriter.WriteStartAttribute(EVENT_TIME_OCCURRED_ATTRIBUTE);
                    reportWriter.WriteString(logEvent.TimeOccurred.ToLongTimeString());
                    reportWriter.WriteEndAttribute();
                    if (!string.IsNullOrEmpty(logEvent.Location))
                    {
                        reportWriter.WriteStartAttribute(LOCATION_ATTRIBUTE);
                        reportWriter.WriteString(logEvent.Location);
                        reportWriter.WriteEndAttribute();
                    }
                    if (!string.IsNullOrEmpty(logEvent.Target))
                    {
                        reportWriter.WriteStartAttribute(TARGET_ATTRIBUTE);
                        reportWriter.WriteString(logEvent.Target);
                        reportWriter.WriteEndAttribute();
                    }
                    reportWriter.WriteString(logEvent.Message);
                    if (!string.IsNullOrEmpty(logEvent.StackTrace))
                    {
                        reportWriter.WriteString("StackTrace: " + logEvent.StackTrace);
                    }
                    reportWriter.WriteEndElement();
                }
                reportWriter.WriteEndElement();
            }
            reportWriter.WriteEndElement();
            reportWriter.WriteEndDocument();
            reportWriter.Flush();
            reportWriter.Close();
        }

        #endregion
    }
}
