using System.Security.Cryptography;
using System.Text;

namespace KryskataFund.Services
{
    public static class PasswordHasher
    {
        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public static bool VerifyPassword(string password, string hash)
        {
            // BCrypt hashes start with "$2" — try BCrypt first
            if (hash.StartsWith("$2"))
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }

            // Legacy SHA256 hash — verify against it
            return VerifySha256(password, hash);
        }

        private static bool VerifySha256(string password, string hash)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            var computed = Convert.ToBase64String(bytes);
            return computed == hash;
        }
    }
}
