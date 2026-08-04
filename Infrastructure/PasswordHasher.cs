using System;
using System.Security.Cryptography;

namespace ProcessTestApp.Infrastructure
{
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 600000;
        private const string FormatPrefix = "PBKDF2-SHA256";

        public static string HashPassword(string password)
        {
            if (password == null) throw new ArgumentNullException("password");

            byte[] salt = new byte[SaltSize];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
            }

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                byte[] key = pbkdf2.GetBytes(KeySize);
                return string.Format("{0}${1}${2}${3}", FormatPrefix, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(key));
            }
        }

        public static bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword)) return false;

            try
            {
                string[] parts = hashedPassword.Split('$');
                if (parts.Length == 4 && parts[0] == FormatPrefix)
                {
                    int iterations;
                    if (!int.TryParse(parts[1], out iterations) || iterations < 1) return false;
                    byte[] salt = Convert.FromBase64String(parts[2]);
                    byte[] expectedKey = Convert.FromBase64String(parts[3]);
                    if (salt.Length < SaltSize || expectedKey.Length != KeySize) return false;

                    using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                    {
                        return FixedTimeEquals(expectedKey, pbkdf2.GetBytes(expectedKey.Length));
                    }
                }

                // Previous PBKDF2-SHA1 format: salt and key concatenated as Base64.
                byte[] legacyHashBytes = Convert.FromBase64String(hashedPassword);
                if (legacyHashBytes.Length != SaltSize + KeySize) return false;
                byte[] legacySalt = new byte[SaltSize];
                Array.Copy(legacyHashBytes, legacySalt, SaltSize);
                using (var legacyPbkdf2 = new Rfc2898DeriveBytes(password, legacySalt, 10000))
                {
                    byte[] expectedLegacyKey = new byte[KeySize];
                    Array.Copy(legacyHashBytes, SaltSize, expectedLegacyKey, 0, KeySize);
                    return FixedTimeEquals(expectedLegacyKey, legacyPbkdf2.GetBytes(KeySize));
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool NeedsRehash(string hashedPassword)
        {
            if (string.IsNullOrEmpty(hashedPassword)) return true;
            string[] parts = hashedPassword.Split('$');
            int iterations;
            return parts.Length != 4 || parts[0] != FormatPrefix || !int.TryParse(parts[1], out iterations) || iterations < Iterations;
        }

        public static bool IsPasswordStrong(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 12) return false;

            bool hasUpper = false;
            bool hasLower = false;
            bool hasDigit = false;
            foreach (char c in password)
            {
                if (char.IsUpper(c)) hasUpper = true;
                else if (char.IsLower(c)) hasLower = true;
                else if (char.IsDigit(c)) hasDigit = true;
            }
            return hasUpper && hasLower && hasDigit;
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            int result = 0;
            for (int i = 0; i < left.Length; i++) result |= left[i] ^ right[i];
            return result == 0;
        }
    }
}
