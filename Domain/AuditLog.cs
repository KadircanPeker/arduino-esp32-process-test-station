using System;

namespace ProcessTestApp.Domain
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public DateTime ActionTime { get; set; }
        public string ActionType { get; set; } // Örn: SERIAL_CONNECT, LOGIN, LIMITS_SENT
        public string Description { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }

        public AuditLog()
        {
            ActionTime = DateTime.Now;
        }

        public AuditLog(string username, string actionType, string description, string oldValue, string newValue)
        {
            this.Username = username;
            this.ActionTime = DateTime.Now;
            this.ActionType = actionType;
            this.Description = description;
            this.OldValue = oldValue;
            this.NewValue = newValue;
        }
    }
}
