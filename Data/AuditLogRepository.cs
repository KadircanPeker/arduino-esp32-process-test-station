using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using ProcessTestApp.Domain;
using ProcessTestApp.Infrastructure;

namespace ProcessTestApp.Data
{
    public interface IAuditLogRepository
    {
        bool Add(AuditLog log);
        List<AuditLog> GetLogs(int limit);
        Dictionary<string, int> GetChangeSummaryByType(DateTime since);
    }

    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly IDbConnectionFactory _dbConnectionFactory;

        public AuditLogRepository(IDbConnectionFactory dbConnectionFactory)
        {
            this._dbConnectionFactory = dbConnectionFactory;
        }

        public bool Add(AuditLog log)
        {
            const string query = "INSERT INTO AuditLogs (Username, ActionTime, ActionType, Description, OldValue, NewValue) VALUES (@Username, @ActionTime, @ActionType, @Description, @OldValue, @NewValue)";
            try
            {
                using (var conn = _dbConnectionFactory.CreateConnection())
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Username", log.Username);
                    cmd.Parameters.AddWithValue("@ActionTime", log.ActionTime);
                    cmd.Parameters.AddWithValue("@ActionType", log.ActionType);
                    cmd.Parameters.AddWithValue("@Description", log.Description);
                    cmd.Parameters.AddWithValue("@OldValue", (object)log.OldValue ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@NewValue", (object)log.NewValue ?? DBNull.Value);

                    conn.Open();
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error("AuditLogRepository", "Add hatasi: " + ex.Message);
                return false;
            }
        }

        public List<AuditLog> GetLogs(int limit)
        {
            var list = new List<AuditLog>();
            string query = string.Format("SELECT TOP {0} Id, Username, ActionTime, ActionType, Description, OldValue, NewValue FROM AuditLogs ORDER BY ActionTime DESC", limit);
            try
            {
                using (var conn = _dbConnectionFactory.CreateConnection())
                using (var cmd = new SqlCommand(query, conn))
                {
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new AuditLog
                            {
                                Id = reader.GetInt32(0),
                                Username = reader.GetString(1),
                                ActionTime = reader.GetDateTime(2),
                                ActionType = reader.GetString(3),
                                Description = reader.GetString(4),
                                OldValue = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                NewValue = reader.IsDBNull(6) ? "" : reader.GetString(6)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error("AuditLogRepository", "GetLogs hatasi: " + ex.Message);
            }
            return list;
        }

        public Dictionary<string, int> GetChangeSummaryByType(DateTime since)
        {
            var summary = new Dictionary<string, int>();
            const string query = @"
                SELECT ActionType, COUNT(*) 
                FROM AuditLogs 
                WHERE ActionTime >= @Since 
                GROUP BY ActionType";

            try
            {
                using (var conn = _dbConnectionFactory.CreateConnection())
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Since", since);
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            summary[reader.GetString(0)] = reader.GetInt32(1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error("AuditLogRepository", "GetChangeSummaryByType hatasi: " + ex.Message);
            }
            return summary;
        }
    }
}
