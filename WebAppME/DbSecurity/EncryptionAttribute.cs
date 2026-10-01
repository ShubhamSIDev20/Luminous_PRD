namespace BatteryTestingSystem.DbSecurity
{
    // Encrypt using salted AES — strong security, NOT searchable
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class EncryptedAttribute : Attribute { }

    // Exclude a property when the class is fully encrypted
    [AttributeUsage(AttributeTargets.Property)]
    public class NotEncryptedAttribute : Attribute { }

    // Encrypt using deterministic AES (fixed IV) — allows direct WHERE queries
    // Use only for columns you need to search on (e.g. Name, Email, Code)
    // ⚠️ Do NOT use for highly sensitive data (SSN, passwords, etc.)
    [AttributeUsage(AttributeTargets.Property)]
    public class EncryptedSearchableAttribute : Attribute { }
}


//// ─── Usage Examples ────────────────────────────────────────────────────────

//// 1. Encrypt ALL properties (salted — strong, not searchable)
//[Encrypted]
//public class Device
//{
//    [NotEncrypted] public int Id { get; set; }        // ← skipped (PK)
//    [NotEncrypted] public int DeviceId { get; set; }  // ← skipped (FK)
//    public string Name { get; set; }                  // ← salted encrypted
//    public float Voltage { get; set; }                // ← salted encrypted
//    public bool IsActive { get; set; }                // ← salted encrypted
//}

//// 2. Mixed — searchable + strongly encrypted
//public class Device
//{
//    [NotEncrypted]         public int Id { get; set; }       // ← skipped
//    [EncryptedSearchable]  public string Name { get; set; }  // ← deterministic (searchable)
//    [Encrypted]            public float Voltage { get; set; }// ← salted (strong)
//    [Encrypted]            public string Notes { get; set; } // ← salted (strong)
//}

//// 3. Query searchable column directly ✅
//var encrypted = _encryption.Encrypt("SensorA");
//var result = await _db.Devices
//    .Where(d => d.Name == encrypted)
//    .ToListAsync();

//// 4. Or use the extension method ✅
//var result = await _db.Devices
//    .WhereEncrypted(d => d.Name, "SensorA", _encryption)
//    .ToListAsync();