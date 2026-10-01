using BatteryTestingSystem.DbSecurity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Serilog;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace BatteryTestingSystem.DbSecurity
{
    public static class ModelBuilderEncryptionExtensions
    {
        private static readonly Serilog.ILogger _log =
            Log.ForContext("SourceContext", "ModelBuilderEncryptionExtensions");

        /// <summary>
        /// Scans all entities and applies encryption converters based on attributes:
        /// [Encrypted]           → salted AES via IColumnEncryptionServiceWithSalt (strong, not searchable)
        /// [EncryptedSearchable] → deterministic AES via IColumnEncryptionService   (searchable, weaker)
        /// </summary>
        public static ModelBuilder ApplyEncryption(
            this ModelBuilder modelBuilder,
            IColumnEncryptionService searchableEncryption,        // deterministic — for [EncryptedSearchable]
            IColumnEncryptionServiceWithSalt saltedEncryption)    // salted        — for [Encrypted]
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var classEncrypted = entityType.ClrType
                    .GetCustomAttribute<EncryptedAttribute>() != null;

                foreach (var property in entityType.ClrType.GetProperties())
                {
                    var notEncrypted = property.GetCustomAttribute<NotEncryptedAttribute>() != null;
                    if (notEncrypted) continue;

                    var isSearchable = property.GetCustomAttribute<EncryptedSearchableAttribute>() != null;

                    // Class-level [Encrypted]: encrypt all unless [NotEncrypted] or [EncryptedSearchable]
                    // Property-level: encrypt only if [Encrypted] or [EncryptedSearchable]
                    var useSalted = classEncrypted
                        ? !isSearchable   // class encrypted → salted unless explicitly searchable
                        : property.GetCustomAttribute<EncryptedAttribute>() != null;

                    if (!useSalted && !isSearchable) continue;

                    var type = Nullable.GetUnderlyingType(property.PropertyType)
                               ?? property.PropertyType;
                    var isNullable = Nullable.GetUnderlyingType(property.PropertyType) != null
                                     || !property.PropertyType.IsValueType;

                    // Pick the right encryption service
                    ValueConverter? converter = isSearchable
                        ? GetConverterSearchable(type, isNullable, searchableEncryption)
                        : GetConverterSalted(type, isNullable, saltedEncryption);

                    if (converter is null) continue;

                    modelBuilder
                        .Entity(entityType.ClrType)
                        .Property(property.Name)
                        .HasConversion(converter)
                        .HasColumnType("TEXT");
                }
            }

            return modelBuilder;
        }

        // ── Deterministic converter (IColumnEncryptionService) ────────────────────
        // Used for [EncryptedSearchable] — same plaintext always → same ciphertext
        // Allows: .Where(d => d.Name == encryption.Encrypt("SensorA"))

        private static ValueConverter? GetConverterSearchable(
            Type type, bool isNullable, IColumnEncryptionService enc)
        {
            if (type == typeof(string))
                return new ValueConverter<string, string>(
                    v => SafeEncrypt(enc, v),
                    v => SafeDecrypt(enc, v));

            if (type == typeof(int))
                return isNullable
                    ? new ValueConverter<int?, string>(
                        v => v.HasValue ? SafeEncrypt(enc, v.Value.ToString()) : null,
                        v => v == null ? null : SafeDecryptParse(enc, v, int.Parse))
                    : new ValueConverter<int, string>(
                        v => SafeEncrypt(enc, v.ToString()),
                        v => SafeDecryptParse(enc, v, int.Parse));

            if (type == typeof(float))
                return isNullable
                    ? new ValueConverter<float?, string>(
                        v => v.HasValue ? SafeEncrypt(enc, v.Value.ToString(CultureInfo.InvariantCulture)) : null,
                        v => v == null ? null : SafeDecryptParse(enc, v, s => float.Parse(s, CultureInfo.InvariantCulture)))
                    : new ValueConverter<float, string>(
                        v => SafeEncrypt(enc, v.ToString(CultureInfo.InvariantCulture)),
                        v => SafeDecryptParse(enc, v, s => float.Parse(s, CultureInfo.InvariantCulture)));

            if (type == typeof(double))
                return isNullable
                    ? new ValueConverter<double?, string>(
                        v => v.HasValue ? SafeEncrypt(enc, v.Value.ToString(CultureInfo.InvariantCulture)) : null,
                        v => v == null ? null : SafeDecryptParse(enc, v, s => double.Parse(s, CultureInfo.InvariantCulture)))
                    : new ValueConverter<double, string>(
                        v => SafeEncrypt(enc, v.ToString(CultureInfo.InvariantCulture)),
                        v => SafeDecryptParse(enc, v, s => double.Parse(s, CultureInfo.InvariantCulture)));

            if (type == typeof(bool))
                return isNullable
                    ? new ValueConverter<bool?, string>(
                        v => v.HasValue ? SafeEncrypt(enc, v.Value.ToString()) : null,
                        v => v == null ? null : SafeDecryptParse(enc, v, bool.Parse))
                    : new ValueConverter<bool, string>(
                        v => SafeEncrypt(enc, v.ToString()),
                        v => SafeDecryptParse(enc, v, bool.Parse));

            if (type == typeof(DateTime))
                return isNullable
                    ? new ValueConverter<DateTime?, string>(
                        v => v.HasValue ? SafeEncrypt(enc, v.Value.ToString("o")) : null,
                        v => v == null ? null : SafeDecryptParse(enc, v, s => DateTime.Parse(s, null, DateTimeStyles.RoundtripKind)))
                    : new ValueConverter<DateTime, string>(
                        v => SafeEncrypt(enc, v.ToString("o")),
                        v => SafeDecryptParse(enc, v, s => DateTime.Parse(s, null, DateTimeStyles.RoundtripKind)));

            if (type == typeof(decimal))
                return isNullable
                    ? new ValueConverter<decimal?, string>(
                        v => v.HasValue ? SafeEncrypt(enc, v.Value.ToString(CultureInfo.InvariantCulture)) : null,
                        v => v == null ? null : SafeDecryptParse(enc, v, s => decimal.Parse(s, CultureInfo.InvariantCulture)))
                    : new ValueConverter<decimal, string>(
                        v => SafeEncrypt(enc, v.ToString(CultureInfo.InvariantCulture)),
                        v => SafeDecryptParse(enc, v, s => decimal.Parse(s, CultureInfo.InvariantCulture)));

            if (type == typeof(long))
                return isNullable
                    ? new ValueConverter<long?, string>(
                        v => v.HasValue ? SafeEncrypt(enc, v.Value.ToString()) : null,
                        v => v == null ? null : SafeDecryptParse(enc, v, long.Parse))
                    : new ValueConverter<long, string>(
                        v => SafeEncrypt(enc, v.ToString()),
                        v => SafeDecryptParse(enc, v, long.Parse));

            _log.Debug("Unsupported searchable-encrypted type: {Type} — skipped", type.Name);
            return null;
        }

        // ── Salted converter (IColumnEncryptionServiceWithSalt) ───────────────────
        // Used for [Encrypted] — random salt → different ciphertext every time
        // Strong security, NOT queryable via WHERE

        private static ValueConverter? GetConverterSalted(
            Type type, bool isNullable, IColumnEncryptionServiceWithSalt enc)
        {
            if (type == typeof(string))
                return new ValueConverter<string, string>(
                    v => SafeEncryptSalted(enc, v),
                    v => SafeDecryptSalted(enc, v));

            if (type == typeof(int))
                return isNullable
                    ? new ValueConverter<int?, string>(
                        v => v.HasValue ? SafeEncryptSalted(enc, v.Value.ToString()) : null,
                        v => v == null ? null : SafeDecryptParseSalted(enc, v, int.Parse))
                    : new ValueConverter<int, string>(
                        v => SafeEncryptSalted(enc, v.ToString()),
                        v => SafeDecryptParseSalted(enc, v, int.Parse));

            if (type == typeof(float))
                return isNullable
                    ? new ValueConverter<float?, string>(
                        v => v.HasValue ? SafeEncryptSalted(enc, v.Value.ToString(CultureInfo.InvariantCulture)) : null,
                        v => v == null ? null : SafeDecryptParseSalted(enc, v, s => float.Parse(s, CultureInfo.InvariantCulture)))
                    : new ValueConverter<float, string>(
                        v => SafeEncryptSalted(enc, v.ToString(CultureInfo.InvariantCulture)),
                        v => SafeDecryptParseSalted(enc, v, s => float.Parse(s, CultureInfo.InvariantCulture)));

            if (type == typeof(double))
                return isNullable
                    ? new ValueConverter<double?, string>(
                        v => v.HasValue ? SafeEncryptSalted(enc, v.Value.ToString(CultureInfo.InvariantCulture)) : null,
                        v => v == null ? null : SafeDecryptParseSalted(enc, v, s => double.Parse(s, CultureInfo.InvariantCulture)))
                    : new ValueConverter<double, string>(
                        v => SafeEncryptSalted(enc, v.ToString(CultureInfo.InvariantCulture)),
                        v => SafeDecryptParseSalted(enc, v, s => double.Parse(s, CultureInfo.InvariantCulture)));

            if (type == typeof(bool))
                return isNullable
                    ? new ValueConverter<bool?, string>(
                        v => v.HasValue ? SafeEncryptSalted(enc, v.Value.ToString()) : null,
                        v => v == null ? null : SafeDecryptParseSalted(enc, v, bool.Parse))
                    : new ValueConverter<bool, string>(
                        v => SafeEncryptSalted(enc, v.ToString()),
                        v => SafeDecryptParseSalted(enc, v, bool.Parse));

            if (type == typeof(DateTime))
                return isNullable
                    ? new ValueConverter<DateTime?, string>(
                        v => v.HasValue ? SafeEncryptSalted(enc, v.Value.ToString("o")) : null,
                        v => v == null ? null : SafeDecryptParseSalted(enc, v, s => DateTime.Parse(s, null, DateTimeStyles.RoundtripKind)))
                    : new ValueConverter<DateTime, string>(
                        v => SafeEncryptSalted(enc, v.ToString("o")),
                        v => SafeDecryptParseSalted(enc, v, s => DateTime.Parse(s, null, DateTimeStyles.RoundtripKind)));

            if (type == typeof(decimal))
                return isNullable
                    ? new ValueConverter<decimal?, string>(
                        v => v.HasValue ? SafeEncryptSalted(enc, v.Value.ToString(CultureInfo.InvariantCulture)) : null,
                        v => v == null ? null : SafeDecryptParseSalted(enc, v, s => decimal.Parse(s, CultureInfo.InvariantCulture)))
                    : new ValueConverter<decimal, string>(
                        v => SafeEncryptSalted(enc, v.ToString(CultureInfo.InvariantCulture)),
                        v => SafeDecryptParseSalted(enc, v, s => decimal.Parse(s, CultureInfo.InvariantCulture)));

            if (type == typeof(long))
                return isNullable
                    ? new ValueConverter<long?, string>(
                        v => v.HasValue ? SafeEncryptSalted(enc, v.Value.ToString()) : null,
                        v => v == null ? null : SafeDecryptParseSalted(enc, v, long.Parse))
                    : new ValueConverter<long, string>(
                        v => SafeEncryptSalted(enc, v.ToString()),
                        v => SafeDecryptParseSalted(enc, v, long.Parse));

            _log.Debug("Unsupported salted-encrypted type: {Type} — skipped", type.Name);
            return null;
        }

        // ── Safe helpers — deterministic (IColumnEncryptionService) ──────────────

        private static string SafeEncrypt(IColumnEncryptionService enc, string value)
        {
            try { return enc.Encrypt(value); }
            catch (Exception ex) { _log.Error(ex, "Encrypt failed — storing null"); return null; }
        }

        private static string SafeDecrypt(IColumnEncryptionService enc, string value)
        {
            try { return enc.Decrypt(value); }
            catch (Exception ex) { _log.Error(ex, "Decrypt failed — returning null"); return null; }
        }

        private static T SafeDecryptParse<T>(
            IColumnEncryptionService enc, string value, Func<string, T> parser) where T : struct
        {
            try { return parser(enc.Decrypt(value)); }
            catch (Exception ex)
            {
                _log.Error(ex, "Decrypt+Parse failed — returning default({Type})", typeof(T).Name);
                return default;
            }
        }

        // ── Safe helpers — salted (IColumnEncryptionServiceWithSalt) ─────────────

        private static string SafeEncryptSalted(IColumnEncryptionServiceWithSalt enc, string value)
        {
            try { return enc.Encrypt(value); }
            catch (Exception ex) { _log.Error(ex, "Salted encrypt failed — storing null"); return null; }
        }

        private static string SafeDecryptSalted(IColumnEncryptionServiceWithSalt enc, string value)
        {
            try { return enc.Decrypt(value); }
            catch (Exception ex) { _log.Error(ex, "Salted decrypt failed — returning null"); return null; }
        }

        private static T SafeDecryptParseSalted<T>(
            IColumnEncryptionServiceWithSalt enc, string value, Func<string, T> parser) where T : struct
        {
            try { return parser(enc.Decrypt(value)); }
            catch (Exception ex)
            {
                _log.Error(ex, "Salted Decrypt+Parse failed — returning default({Type})", typeof(T).Name);
                return default;
            }
        }
    }


    // ── Query extension — clean WHERE on [EncryptedSearchable] columns ────────────

    public static class EncryptedQueryExtensions
    {
        /// <summary>
        /// Encrypts the search value deterministically and applies a WHERE clause.
        /// Only works on columns marked [EncryptedSearchable].
        /// </summary>
        public static IQueryable<T> WhereEncrypted<T>(
            this IQueryable<T> query,
            Expression<Func<T, string>> property,
            string plainValue,
            IColumnEncryptionService encryption)
        {
            var encrypted = encryption.Encrypt(plainValue);
            var param = property.Parameters[0];
            var body = Expression.Equal(
                property.Body,
                Expression.Constant(encrypted, typeof(string)));
            var lambda = Expression.Lambda<Func<T, bool>>(body, param);
            return query.Where(lambda);
        }
    }
}


// ── Registration in Program.cs ────────────────────────────────────────────────

//var dbEncryptionKey = builder.Configuration["DbEncryption:Key"]
//    ?? throw new InvalidOperationException("DbEncryption:Key not configured.");

//builder.Services.AddSingleton<IColumnEncryptionService>(
//    new ColumnEncryptionService(dbEncryptionKey));           // deterministic

//builder.Services.AddSingleton<IColumnEncryptionServiceWithSalt>(
//    new ColumnEncryptionServiceWithSalt(dbEncryptionKey));   // salted


// ── DbContext OnModelCreating ─────────────────────────────────────────────────

//protected override void OnModelCreating(ModelBuilder modelBuilder)
//{
//    base.OnModelCreating(modelBuilder);
//
//    if (_searchableEncryption is not null && _saltedEncryption is not null)
//        modelBuilder.ApplyEncryption(_searchableEncryption, _saltedEncryption);
//}


// ── Entity example ────────────────────────────────────────────────────────────

//public class Device
//{
//    [NotEncrypted]         public int Id { get; set; }
//    [NotEncrypted]         public int DeviceId { get; set; }      // FK
//    [EncryptedSearchable]  public string Name { get; set; }       // searchable
//    [Encrypted]            public float Voltage { get; set; }     // strong
//    [Encrypted]            public string Notes { get; set; }      // strong
//}


// ── Query examples ────────────────────────────────────────────────────────────

//// Option A — manual encrypt
//var enc = _encryption.Encrypt("SensorA");
//var result = await _db.Devices.Where(d => d.Name == enc).ToListAsync();

//// Option B — extension method (cleaner)
//var result = await _db.Devices
//    .WhereEncrypted(d => d.Name, "SensorA", _encryption)
//    .ToListAsync();