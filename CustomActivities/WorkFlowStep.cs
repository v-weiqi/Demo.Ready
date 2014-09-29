namespace CustomActivities
{
    public enum WorkFlowStep
    {
        GenerateAndValidate = 1,
        ContinueToTC2 = 2,
        VendorTC2Response = 3,
        ContinueToProd = 4,
        BackupFeedAndPackages = 5,
        PublishToProd = 6,
    }
}
