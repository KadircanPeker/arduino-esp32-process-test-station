using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using ProcessTestApp.Infrastructure;

namespace ProcessTestApp.Data
{
    public class TestLogRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public TestLogRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public bool Add(TestData data)
        {
            const string sql = @"INSERT INTO dbo.ProcessTestLogs
                (SerialNumber, ProductType, Voltage, [Current], Result, ErrorCode, CreatedDate,
                 TestAttemptNo, StationName, OperatorName, SourceType, BatchNo)
                VALUES (@SerialNumber, @ProductType, @Voltage, @Current, @Result, @ErrorCode, @CreatedDate,
                        @TestAttemptNo, @StationName, @OperatorName, @SourceType, @BatchNo)";
            try
            {
                using (var connection = _connectionFactory.CreateConnection())
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@SerialNumber", data.SerialNumber ?? "UNKNOWN");
                    command.Parameters.AddWithValue("@ProductType", data.ProductType ?? "UNKNOWN");
                    command.Parameters.AddWithValue("@Voltage", data.Voltage);
                    command.Parameters.AddWithValue("@Current", data.Current);
                    command.Parameters.AddWithValue("@Result", data.Result ?? "FAIL");
                    command.Parameters.AddWithValue("@ErrorCode", data.ErrorCode ?? "FORMAT_ERR");
                    command.Parameters.AddWithValue("@CreatedDate", data.LogTime);
                    command.Parameters.AddWithValue("@TestAttemptNo", data.TestAttemptNo < 1 ? 1 : data.TestAttemptNo);
                    command.Parameters.AddWithValue("@StationName", (object)data.StationName ?? DBNull.Value);
                    command.Parameters.AddWithValue("@OperatorName", (object)data.OperatorName ?? DBNull.Value);
                    command.Parameters.AddWithValue("@SourceType", (object)data.SourceType ?? DBNull.Value);
                    command.Parameters.AddWithValue("@BatchNo", (object)data.BatchNo ?? DBNull.Value);
                    connection.Open();
                    return command.ExecuteNonQuery() == 1;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error("TestLogRepository", "Test kaydı eklenemedi: " + ex.Message);
                return false;
            }
        }

        public List<TestData> GetRecent(int limit)
        {
            var results = new List<TestData>();
            int safeLimit = Math.Max(1, Math.Min(limit, 5000));
            string sql = string.Format(@"SELECT TOP {0} Id, SerialNumber, ProductType, Voltage, [Current], Result,
                ErrorCode, CreatedDate, TestAttemptNo, StationName, OperatorName, SourceType, BatchNo
                FROM dbo.ProcessTestLogs ORDER BY CreatedDate DESC", safeLimit);
            try
            {
                using (var connection = _connectionFactory.CreateConnection())
                using (var command = new SqlCommand(sql, connection))
                {
                    connection.Open();
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new TestData
                            {
                                Id = reader.GetInt32(0),
                                SerialNumber = reader.GetString(1),
                                ProductType = reader.GetString(2),
                                Voltage = Convert.ToDouble(reader.GetValue(3)),
                                Current = Convert.ToDouble(reader.GetValue(4)),
                                Result = reader.GetString(5),
                                ErrorCode = reader.GetString(6),
                                LogTime = reader.GetDateTime(7),
                                TestAttemptNo = reader.GetInt32(8),
                                StationName = reader.IsDBNull(9) ? "" : reader.GetString(9),
                                OperatorName = reader.IsDBNull(10) ? "" : reader.GetString(10),
                                SourceType = reader.IsDBNull(11) ? "" : reader.GetString(11),
                                BatchNo = reader.IsDBNull(12) ? "" : reader.GetString(12)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error("TestLogRepository", "Geçmiş kayıtlar okunamadı: " + ex.Message);
            }
            return results;
        }
    }
}
