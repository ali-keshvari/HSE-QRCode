using System;
using System.Data.Entity;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HSEQRCode.Data;
using HSEQRCode.Domain.Entities;
using HSEQRCode.Services.Interfaces;

namespace HSEQRCode.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Pbkdf2Iterations = 10000;

        private readonly HseDbContext _db;

        public AuthService(HseDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<AppUser> ValidateUserAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                return null;
            }

            string trimmed = username.Trim();
            AppUser user = await _db.AppUsers
                .FirstOrDefaultAsync(u => u.Username == trimmed && u.IsActive)
                .ConfigureAwait(false);

            if (user == null || string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                return null;
            }

            return VerifyPassword(password, user.PasswordHash) ? user : null;
        }

        public string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password cannot be empty.", nameof(password));
            }

            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] hash;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Pbkdf2Iterations))
            {
                hash = pbkdf2.GetBytes(HashSize);
            }

            return "PBKDF2:" + Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
        }

        public async Task<bool> IsUserAuthorizedForDataSourceAsync(int dataSourceId, string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            string trimmed = username.Trim();
            AppUser user = await _db.AppUsers
                .FirstOrDefaultAsync(u => u.Username == trimmed && u.IsActive)
                .ConfigureAwait(false);

            if (user == null)
            {
                return false;
            }

            if (user.IsAdmin)
            {
                return true;
            }

            return await _db.FormPersonnel
                .AnyAsync(p => p.DataSourceId == dataSourceId && p.Username == trimmed && p.IsActive)
                .ConfigureAwait(false);
        }

        public async Task<AppUser> GetActiveUserAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            string trimmed = username.Trim();
            return await _db.AppUsers
                .FirstOrDefaultAsync(u => u.Username == trimmed && u.IsActive)
                .ConfigureAwait(false);
        }

        private static bool VerifyPassword(string password, string storedHash)
        {
            if (storedHash.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase))
            {
                string expectedHex = storedHash.Substring("SHA256:".Length).Trim();
                using (var sha = SHA256.Create())
                {
                    byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                    var sb = new StringBuilder(bytes.Length * 2);
                    foreach (byte b in bytes)
                    {
                        sb.Append(b.ToString("x2"));
                    }

                    return FixedTimeEquals(
                        Encoding.ASCII.GetBytes(sb.ToString()),
                        Encoding.ASCII.GetBytes(expectedHex.ToLowerInvariant()));
                }
            }

            if (storedHash.StartsWith("PBKDF2:", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = storedHash.Split(':');
                if (parts.Length != 3)
                {
                    return false;
                }

                byte[] salt;
                byte[] expectedHash;
                try
                {
                    salt = Convert.FromBase64String(parts[1]);
                    expectedHash = Convert.FromBase64String(parts[2]);
                }
                catch (FormatException)
                {
                    return false;
                }

                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Pbkdf2Iterations))
                {
                    byte[] actualHash = pbkdf2.GetBytes(expectedHash.Length);
                    return FixedTimeEquals(actualHash, expectedHash);
                }
            }

            return false;
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }

            return diff == 0;
        }
    }
}
