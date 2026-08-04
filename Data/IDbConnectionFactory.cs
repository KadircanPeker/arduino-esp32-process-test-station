using System;
using System.Configuration;
using System.Data.SqlClient;

namespace ProcessTestApp.Data
{
    public interface IDbConnectionFactory
    {
        SqlConnection CreateConnection();
        string ConnectionString { get; }
    }

    public class SqlConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public string ConnectionString
        {
            get { return _connectionString; }
        }

        public SqlConnectionFactory(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                var connSetting = ConfigurationManager.ConnectionStrings["DefaultConnection"];
                if (connSetting != null)
                {
                    _connectionString = connSetting.ConnectionString;
                }
                else
                {
                    // Controlled fallback/throw: we can fallback to a standard local database naming or throw.
                    // The seeder will check server connection, but here we require a connection string.
                    _connectionString = @"Server=.\SQLEXPRESS;Database=ProcessTestDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;";
                }
            }
            else
            {
                _connectionString = connectionString;
            }
        }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
