using System;

namespace ProcessTestApp.Domain
{
    public class User
    {
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; } // RoleNames içindeki doğrulanmış rollerden biri

        public User()
        {
        }

        public User(string username, string passwordHash, string fullName, string role)
        {
            this.Username = username;
            this.PasswordHash = passwordHash;
            this.FullName = fullName;
            this.Role = role;
        }
    }
}
