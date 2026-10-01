using BatteryTestingSystem.DbSecurity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities
{

    [Table(name: "Configuration", Schema = "auth")]
    //[Encrypted]
    public class ConfigurationEntity : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        //[NotEncrypted]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Key { get; set; } = string.Empty;

        public string? Value { get; set; } = string.Empty;

    }
}
