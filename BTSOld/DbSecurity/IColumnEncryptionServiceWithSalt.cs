using Serilog;
using System.Security.Cryptography;
using System.Text;

namespace BatteryTestingSystem.DbSecurity
{
    public interface IColumnEncryptionServiceWithSalt
    {
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
    }

    public class ColumnEncryptionServiceWithSalt : IColumnEncryptionServiceWithSalt
    {
        private readonly byte[] _masterKey;

        private const int SaltSize = 16;      // 128-bit salt
        private const int IvSize = 16;      // AES IV
        private const int Iterations = 100_000; // PBKDF2 rounds
        private const int DerivedKeySize = 32;      // 256-bit AES key

        public ColumnEncryptionServiceWithSalt(string secret)
        {
            using var sha = SHA256.Create();
            _masterKey = sha.ComputeHash(Encoding.UTF8.GetBytes(secret));
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            try
            {
                var salt = RandomNumberGenerator.GetBytes(SaltSize);
                var derivedKey = DeriveKey(salt);

                using var aes = Aes.Create();
                aes.Key = derivedKey;
                aes.GenerateIV();

                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                var cipherBytes = aes.CreateEncryptor()
                                     .TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                var payload = new byte[SaltSize + IvSize + cipherBytes.Length];
                Buffer.BlockCopy(salt, 0, payload, 0, SaltSize);
                Buffer.BlockCopy(aes.IV, 0, payload, SaltSize, IvSize);
                Buffer.BlockCopy(cipherBytes, 0, payload, SaltSize + IvSize, cipherBytes.Length);

                return Convert.ToBase64String(payload);
            }
            catch (CryptographicException ex)
            {
                Log.ForContext("SourceContext", "ColumnEncryptionServiceWithSalt")
                .Error("Encryption failed due to a cryptographic error.", ex);
            }
            catch (Exception ex)
            {
                Log.ForContext("SourceContext", "ColumnEncryptionServiceWithSalt")
                .Error("Encryption failed unexpectedly.", ex);
            }

            return string.Empty;

        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            byte[] payload;

            // ── 1. Base64 decode ────────────────────────────────────────────
            try
            {
                payload = Convert.FromBase64String(cipherText);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException(
                    "Decryption failed: the value is not valid Base64. It may be corrupted or unencrypted.", ex);
            }

            // ── 2. Length guard ─────────────────────────────────────────────
            if (payload.Length <= SaltSize + IvSize)
                throw new InvalidOperationException(
                    $"Decryption failed: payload is too short ({payload.Length} bytes). " +
                    $"Minimum expected: {SaltSize + IvSize + 1} bytes. " +
                    $"The value may be from an older unencrypted format.");

            // ── 3. Unpack ───────────────────────────────────────────────────
            var salt = new byte[SaltSize];
            var iv = new byte[IvSize];
            var cipherBytes = new byte[payload.Length - SaltSize - IvSize];

            Buffer.BlockCopy(payload, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(payload, SaltSize, iv, 0, IvSize);
            Buffer.BlockCopy(payload, SaltSize + IvSize, cipherBytes, 0, cipherBytes.Length);

            // ── 4. Derive key + decrypt ─────────────────────────────────────
            try
            {
                var derivedKey = DeriveKey(salt);

                using var aes = Aes.Create();
                aes.Key = derivedKey;
                aes.IV = iv;

                var plainBytes = aes.CreateDecryptor()
                                    .TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (CryptographicException ex)
            {
                // Wrong key, tampered data, or old encrypted format
                throw new InvalidOperationException(
                    "Decryption failed: invalid padding or wrong key. " +
                    "The value may be tampered, corrupted, or encrypted with a different key.", ex);
            }
        }
        // PBKDF2 — stretches master key with salt into a unique per-value AES key
        private byte[] DeriveKey(byte[] salt)
        {
            using var kdf = new Rfc2898DeriveBytes(
                _masterKey,
                salt,
                Iterations,
                HashAlgorithmName.SHA256);

            return kdf.GetBytes(DerivedKeySize);
        }

    }

}
