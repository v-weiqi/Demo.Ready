namespace Model
{
    public class UserRequest
    {
        public string TransactionId { get; set; }
        public string Feed { get; set; }
        public bool CloseTransaction { get; set; }
        public string User { get; set; }

        public bool DiscoveryFeedRequiresUpdate { get; set; }
    }
}
