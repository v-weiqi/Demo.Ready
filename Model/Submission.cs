using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Model
{
    [DataContract]
    [Serializable()]
    public class Submission
    {
        public Submission()
        {
            TransactionId = Guid.NewGuid().ToString();
            Statuses = new List<SubmissionStatus>();
            Faults = new List<Fault>();
            LiveInstallerUrls = new List<string>();
            TC2InstallerUrls = new List<string>();
            AzureInstallerUrls = new List<string>();
        }

        [DataMember]
        public string AppId { get; set; }

        [DataMember]
        public string Version { get; set; }
        
        [DataMember]
        public string TransactionId { get; set; }
        
        [DataMember]
        public string Feed { get; set; }

        [DataMember]
        public List<string> LiveInstallerUrls { get; set; }

        [DataMember]
        public List<string> AzureInstallerUrls { get; set; }

        [DataMember]
        public List<string> TC2InstallerUrls { get; set; }

        [DataMember]
        public List<SubmissionStatus> Statuses { get; set; }

        [DataMember]
        public List<Fault> Faults { get; set; }

        [DataMember]
        public SubmissionType SubmissionType { get; set; }

        [DataMember]
        public bool IsAzureReady { get; set; }

        [DataMember]
        public bool Failed { get; set; }

        [DataMember]
        public bool DiscoveryFeedRequiresUpdate { get; set; }

        [DataMember]
        public string BackupLocation { get; set; }

    }

    [DataContract]
    [Serializable()]
    public enum SubmissionType
    {
        [EnumMember]
        Azure,
        [EnumMember]
        Katal,
        [EnumMember]
        WebMatrix
    }
}
