using System;
using System.Data.SqlClient;
using ProcessTestApp.Domain;
using ProcessTestApp.Infrastructure;

namespace ProcessTestApp.Data
{
    public interface IUserRepository
    {
        bool Register(User user);
        User Authenticate(string username, string password);
        bool UserExists(string username);
        bool HasAnyAdmin();
    }

    public class UserRepository : IUserRepository
    {
        private readonly IDbConnectionFactory _dbConnectionFactory;

        public UserRepository(IDbConnectionFactory dbConnectionFactory)
        {
            this._dbConnectionFactory = dbConnectionFactory;
        }

        public bool HasAnyAdmin()
        {
            const string query = "SELECT COUNT(1) FROM ProcessUsers WHERE Role = 'Administrator'";
            try
            {
                using (var conn = _dbConnectionFactory.CreateConnection())
                using (var cmd = new SqlCommand(query, conn))
                {
                    conn.Open();
                    int count = (int)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error("UserRepository", "HasAnyAdmin hatasi: " + ex.Message);
                return false;
            }
        }

        public bool UserExists(string username)
        {
            const string query = "SELECT COUNT(1) FROM ProcessUsers WHERE Username = @Username";
            try
            {
                using (var conn = _dbConnectionFactory.CreateConnection())
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Username", username);
                    conn.Open();
                    int count = (int)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error("UserRepository", "UserExists hatasi: " + ex.Message);
                return false;
            }
        }

        public bool Register(User user)
        {
            const string query = "INSERT INTO ProcessUsers (Username, PasswordHash, FullName, Role) VALUES (@Username, @PasswordHash, @FullName, @Role)";
            try
            {
                if (user == null || !PasswordHasher.IsPasswordStrong(user.PasswordHash))
                {
                    FileLogger.Warning("UserRepository", "Zayif parola nedeniyle kullanici kaydi reddedildi.");
                    return false;
                }

                // Yeni kayıtlar PBKDF2 ile şifrelenir
                string pbkdf2Hash = PasswordHasher.HashPassword(user.PasswordHash);

                using (var conn = _dbConnectionFactory.CreateConnection())
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Username", user.Username);
                    cmd.Parameters.AddWithValue("@PasswordHash", pbkdf2Hash);
                    cmd.Parameters.AddWithValue("@FullName", user.FullName);
                    cmd.Parameters.AddWithValue("@Role", user.Role);

                    conn.Open();
                    int rows = cmd.ExecuteNonQuery();
                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error("UserRepository", "Register hatasi: " + ex.Message);
                return false;
            }
        }

        public User Authenticate(string username, string password)
        {
            const string query = "SELECT PasswordHash, FullName, Role FROM ProcessUsers WHERE Username = @Username";
            try
            {
                using (var conn = _dbConnectionFactory.CreateConnection())
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Username", username);
                    conn.Open();

                    User authenticatedUser = null;
                    string upgradedPasswordHash = null;
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string storedHash = reader.GetString(0);
                            string fullName = reader.GetString(1);
                            string role = reader.GetString(2);

                            if (role != null)
                            {
                                string r = role.Trim().ToLowerInvariant();
                                if (r == "mühendis" || r == "mhendis" || r == "muhendis")
                                    role = RoleNames.Engineer;
                                else if (r == "operatör" || r == "operatr" || r == "operator")
                                    role = RoleNames.Operator;
                                else if (r == "kalite" || r == "quality")
                                    role = RoleNames.Quality;
                                else if (r == "yönetici" || r == "yonetici" || r == "administrator" || r == "admin")
                                    role = RoleNames.Administrator;
                            }

                            bool isPasswordCorrect = false;

                            if (storedHash.Length == 64 && IsHexString(storedHash))
                            {
                                // Legacy SHA-256 doğrulaması ve otomatik PBKDF2'ye yükseltme
                                string legacySha256 = ComputeLegacySha256(password);
                                isPasswordCorrect = FixedTimeEquals(storedHash, legacySha256);
                            }
                            else
                            {
                                // PBKDF2 doğrulaması
                                isPasswordCorrect = PasswordHasher.VerifyPassword(password, storedHash);
                            }

                            if (isPasswordCorrect)
                            {
                                if (PasswordHasher.NeedsRehash(storedHash) || (storedHash.Length == 64 && IsHexString(storedHash)))
                                {
                                    upgradedPasswordHash = PasswordHasher.HashPassword(password);
                                }
                                authenticatedUser = new User(username, upgradedPasswordHash ?? storedHash, fullName, role);
                            }
                        }
                    }

                    if (authenticatedUser != null)
                    {
                        if (upgradedPasswordHash != null && !UpdatePasswordHash(username, upgradedPasswordHash))
                        {
                            FileLogger.Warning("UserRepository", "Kullanici parolasi yeni hash formatina yukseltilmedi: " + username);
                        }
                        return authenticatedUser;
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error("UserRepository", "Authenticate hatasi: " + ex.Message);
            }
            return null;
        }

        private bool IsHexString(string text)
        {
            foreach (char c in text)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                {
                    return false;
                }
            }
            return true;
        }

        private string ComputeLegacySha256(string rawData)
        {
            using (var sha256Hash = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawData));
                var builder = new System.Text.StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private bool UpdatePasswordHash(string username, string passwordHash)
        {
            const string query = "UPDATE ProcessUsers SET PasswordHash = @PasswordHash WHERE Username = @Username";
            try
            {
                using (var conn = _dbConnectionFactory.CreateConnection())
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                    cmd.Parameters.AddWithValue("@Username", username);
                    conn.Open();
                    return cmd.ExecuteNonQuery() == 1;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error("UserRepository", "Password hash upgrade hatasi: " + ex.Message);
                return false;
            }
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            int result = 0;
            for (int i = 0; i < left.Length; i++) result |= left[i] ^ right[i];
            return result == 0;
        }
    }
}
