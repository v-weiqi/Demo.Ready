namespace FmsWeb.Model
{
    public enum TransactionState
    {
        NewSubmission,
        PendingReview,
        Testing,
        TestingFailed,
        TestingPassed,
        Rejected,
        ReadyToPublish,
        Published,
        Hold,
        Inactive,
        PendingLocalizationReview,
        LocalizationTesting,
        ReadyforAppGalReview
    }
}