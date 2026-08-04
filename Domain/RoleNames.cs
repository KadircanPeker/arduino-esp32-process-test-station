using System;

namespace ProcessTestApp.Domain
{
    public static class RoleNames
    {
        public const string Operator = "Operator";
        public const string Supervisor = "Supervisor";
        public const string QualityEngineer = "QualityEngineer";
        public const string ProcessEngineer = "ProcessEngineer";
        public const string Administrator = "Administrator";

        // Geriye dönük kodlar için sabit takma adlar (Aliases)
        public const string Engineer = "ProcessEngineer";
        public const string Quality = "QualityEngineer";
        public const string LegacyEngineer = "Engineer";
        public const string LegacyQuality = "Quality";

        public static bool IsValidRole(string role)
        {
            if (string.IsNullOrEmpty(role)) return false;
            string normalized = NormalizeRoleName(role);
            return normalized == Operator ||
                   normalized == Supervisor ||
                   normalized == QualityEngineer ||
                   normalized == ProcessEngineer ||
                   normalized == Administrator;
        }

        public static string NormalizeRoleName(string role)
        {
            if (string.IsNullOrEmpty(role)) return null;
            
            string r = role.Trim();

            if (r.Equals("Engineer", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("Mühendis", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("ProcessEngineer", StringComparison.OrdinalIgnoreCase))
            {
                return ProcessEngineer;
            }

            if (r.Equals("Quality", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("Kalite", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("QualityEngineer", StringComparison.OrdinalIgnoreCase))
            {
                return QualityEngineer;
            }

            if (r.Equals("Supervisor", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("Vardiya Amiri", StringComparison.OrdinalIgnoreCase))
            {
                return Supervisor;
            }

            if (r.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("Administrator", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("Yönetici", StringComparison.OrdinalIgnoreCase))
            {
                return Administrator;
            }

            if (r.Equals("Operator", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("Operatör", StringComparison.OrdinalIgnoreCase))
            {
                return Operator;
            }

            return null; // Bilinmeyen / Yetkisiz Rol
        }
    }
}
