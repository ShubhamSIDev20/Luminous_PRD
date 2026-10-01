using Newtonsoft.Json;

namespace BatteryTestingSystem.Models.DTOs
{
    public class ConfigurationDto
    {
        public int Id { get; set; }

        public string Key { get; set; } = string.Empty;

        public string? Value { get; set; }

        public string? CreatedBy { get; set; }

        public string? UpdatedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public T? GetValueAs<T>()
        {
            if (string.IsNullOrWhiteSpace(Value))
                return default;

            try
            {
                return JsonConvert.DeserializeObject<T>(Value);
            }
            catch
            {
                return default;
            }
        }
    }
}
