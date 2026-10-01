using System.Security.Cryptography;
using System.Text;

namespace BatteryTestingSystem.DbSecurity
{
    public interface IColumnEncryptionService
    {
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
    }

    public class ColumnEncryptionService : IColumnEncryptionService
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;

        public ColumnEncryptionService(string secret)
        {
            using var sha = SHA256.Create();
            _key = sha.ComputeHash(Encoding.UTF8.GetBytes(secret));
            _iv = _key[..16];
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            var bytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(
                aes.CreateEncryptor().TransformFinalBlock(bytes, 0, bytes.Length));
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            var bytes = Convert.FromBase64String(cipherText);
            return Encoding.UTF8.GetString(
                aes.CreateDecryptor().TransformFinalBlock(bytes, 0, bytes.Length));
        }
    }
}
