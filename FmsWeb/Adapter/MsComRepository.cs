using System.Data;
using System.Data.SqlClient;

namespace FmsWeb.Adapter
{
    public class MsComRepository: Repository
    {
        public override DataTable GetDataFromQuery(string query)
        {
            using (SqlConnection connection = new SqlConnection(GetConnectionString("ApplicationServices")))
            {
                var adap =
                     new SqlDataAdapter(query, connection);
                var data = new DataTable();
                adap.Fill(data);
                return data;
            }
        }

        public override void RunNonQuery(string query, SqlParameter[] parameters)
        {
            using (SqlConnection connection = new SqlConnection(GetConnectionString("ApplicationServices")))
            {
                var command = new SqlCommand(query, connection);
                if (parameters != null && parameters.Length > 0)
                    foreach (var sqlParameter in parameters)
                    {
                        command.Parameters.Add(sqlParameter);
                    }
                command.Connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }
}