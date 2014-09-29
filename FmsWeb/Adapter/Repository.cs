using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Configuration;

namespace FmsWeb.Adapter
{
    public abstract class Repository
    {
        public abstract DataTable GetDataFromQuery(string query);

        public abstract void RunNonQuery(string query, SqlParameter[] parameters);

        public string GetConnectionString(string connectionName)
        {
            Configuration rootWebConfig = WebConfigurationManager.OpenWebConfiguration("/");
            ConnectionStringSettings connString = rootWebConfig.ConnectionStrings.ConnectionStrings[connectionName];
            return connString.ConnectionString;
        }
    }
}
