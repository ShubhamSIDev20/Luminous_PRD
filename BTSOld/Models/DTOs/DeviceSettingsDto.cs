using Newtonsoft.Json;

namespace BatteryTestingSystem.Models.DTOs
{
    public class DeviceSettingsDto
    {
        public int Id { get; set; }
        public int DeviceID { get; set; }
        public int? CircuitID { get; set; }
        public string? SettingName { get; set; }
        public string? SettingJson { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

    }

    public class DeviceSettingsDto<T> : DeviceSettingsDto
    {
        public T? Settings
        {
            get
            {
                if (string.IsNullOrWhiteSpace(SettingJson))
                    return default;

                try
                {
                    return JsonConvert.DeserializeObject<T>(SettingJson);
                }
                catch
                {
                    return default;
                }
            }
            set
            {
                SettingJson = value == null
                    ? null
                    : JsonConvert.SerializeObject(value);
            }
        }
    }

}
