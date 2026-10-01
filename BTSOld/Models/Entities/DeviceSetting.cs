using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities
{
    public class DeviceSetting : BaseEntity
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int DeviceID { get; set; }
        public int? CircuitID { get; set; }
        public string? SettingName { get; set; }
        public string? SettingJson { get; set; }

    }
}
