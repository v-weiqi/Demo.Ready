namespace AppGallery.SIR
{
    public class DbInfo
    {
        private string _adminUsername;
        private string _adminPassword;
        private string _dbServer;

        public DbInfo(string adminUsername, string adminPassword, string dbServer)
        {
            _adminUsername = adminUsername;
            _adminPassword = adminPassword;
            _dbServer = dbServer;
        }

        public string AdminUsername
        {
            get { return _adminUsername; }
        }

        public string AdminPassword
        {
            get { return _adminPassword; }
        }

        public string DbServer
        {
            get { return _dbServer; }
        }
    }
}
