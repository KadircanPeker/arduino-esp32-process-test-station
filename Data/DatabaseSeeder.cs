using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using ProcessTestApp.Infrastructure;

namespace ProcessTestApp.Data
{
    public static class DatabaseSeeder
    {
        public static void EnsureSchemas(string connectionString)
        {
            string targetConnection = ResolveConnectionString(connectionString);
            var targetBuilder = new SqlConnectionStringBuilder(targetConnection);
            string databaseName = targetBuilder.InitialCatalog;

            if (string.IsNullOrWhiteSpace(databaseName) || !Regex.IsMatch(databaseName, "^[A-Za-z0-9_]+$"))
            {
                throw new ArgumentException("Geçersiz veritabanı adı.");
            }

            var masterBuilder = new SqlConnectionStringBuilder(targetConnection) { InitialCatalog = "master" };
            using (var master = new SqlConnection(masterBuilder.ConnectionString))
            using (var command = new SqlCommand($"IF DB_ID(@Name) IS NULL EXEC('CREATE DATABASE [{databaseName}]')", master))
            {
                command.Parameters.AddWithValue("@Name", databaseName);
                master.Open();
                command.ExecuteNonQuery();
            }

            using (var connection = new SqlConnection(targetConnection))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        Execute(connection, transaction, SchemaSql);
                        Execute(connection, transaction, SeedSql);
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            FileLogger.Info("DatabaseSeeder", "Minimal Arduino/ESP32 şeması hazırlandı.");
        }

        private static string ResolveConnectionString(string supplied)
        {
            if (!string.IsNullOrWhiteSpace(supplied)) return supplied;
            var setting = ConfigurationManager.ConnectionStrings["DefaultConnection"];
            if (setting != null && !string.IsNullOrWhiteSpace(setting.ConnectionString)) return setting.ConnectionString;
            return @"Server=.\SQLEXPRESS;Database=ProcessTestDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;";
        }

        private static void Execute(SqlConnection connection, SqlTransaction transaction, string sql)
        {
            using (var command = new SqlCommand(sql, connection, transaction))
            {
                command.ExecuteNonQuery();
            }
        }

        private const string SchemaSql = @"
IF OBJECT_ID('dbo.ProcessUsers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProcessUsers (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(50) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(512) NOT NULL,
        FullName NVARCHAR(120) NOT NULL,
        Role NVARCHAR(40) NOT NULL,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSDATETIME()
    );
END;

IF OBJECT_ID('dbo.AuditLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogs (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(50) NOT NULL,
        ActionTime DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        ActionType NVARCHAR(80) NOT NULL,
        Description NVARCHAR(1000) NOT NULL,
        OldValue NVARCHAR(1000) NULL,
        NewValue NVARCHAR(1000) NULL
    );
END;

IF OBJECT_ID('dbo.ProductThresholds', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductThresholds (
        ProductType NVARCHAR(100) NOT NULL PRIMARY KEY,
        MinVoltage DECIMAL(10,3) NOT NULL,
        MaxVoltage DECIMAL(10,3) NOT NULL,
        MinCurrent DECIMAL(10,3) NOT NULL,
        MaxCurrent DECIMAL(10,3) NOT NULL,
        IpcClass NVARCHAR(30) NULL
    );
END;

IF OBJECT_ID('dbo.ProcessTestLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProcessTestLogs (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        SerialNumber NVARCHAR(80) NOT NULL,
        ProductType NVARCHAR(100) NOT NULL,
        Voltage FLOAT NOT NULL,
        [Current] FLOAT NOT NULL,
        Result NVARCHAR(10) NOT NULL,
        ErrorCode NVARCHAR(30) NOT NULL,
        CreatedDate DATETIME2 NOT NULL,
        TestAttemptNo INT NOT NULL DEFAULT 1,
        StationName NVARCHAR(100) NULL,
        OperatorName NVARCHAR(120) NULL,
        SourceType NVARCHAR(40) NULL,
        BatchNo NVARCHAR(80) NULL
    );
    CREATE INDEX IX_ProcessTestLogs_CreatedDate ON dbo.ProcessTestLogs(CreatedDate DESC);
    CREATE INDEX IX_ProcessTestLogs_Result ON dbo.ProcessTestLogs(Result, ErrorCode);
END;";

        private const string SeedSql = @"
IF NOT EXISTS (SELECT 1 FROM dbo.ProductThresholds WHERE ProductType = 'VOLTAGE_RELAY_TESTER')
    INSERT INTO dbo.ProductThresholds VALUES ('VOLTAGE_RELAY_TESTER', 1.000, 4.500, 0.000, 2.500, 'ARDUINO');

IF NOT EXISTS (SELECT 1 FROM dbo.ProductThresholds WHERE ProductType = 'WIFI_TESTER')
    INSERT INTO dbo.ProductThresholds VALUES ('WIFI_TESTER', 0.000, 75.000, 0.000, 100.000, 'ESP32');";
    }
}
