using BatteryTestingSystem.Services;
using Newtonsoft.Json;

namespace BatteryTestingSystem.Models.DTOs
{
    public class DbcFileRecordDto
    {
        public long Id { get; set; }
        public long BatteryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string StoredFilePath { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public double FileSizeKb => Math.Round(FileSizeBytes / 1024.0, 1);
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false;
        public string DbcstrJson { get; set; } = "{}";
        public DbcDatabase dbcDatabase
        {
            get
            {
                if (string.IsNullOrWhiteSpace(DbcstrJson))
                    return new();

                try
                {
                    return JsonConvert.DeserializeObject<DbcDatabase>(DbcstrJson) ?? new();
                }
                catch
                {
                    return new();
                }
            }
            set
            {
                try
                {
                    DbcstrJson = JsonConvert.SerializeObject(value ?? new());
                }
                catch
                {
                    DbcstrJson = "{}"; // fallback to empty array
                }
            }
        }
    }
}
