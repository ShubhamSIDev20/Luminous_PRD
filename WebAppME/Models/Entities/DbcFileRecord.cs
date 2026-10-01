using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities
{
    [Table(name: "DBCFiles", Schema = "Files")]

    public class DbcFileRecord : BaseEntity
    {

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public long BatteryId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Description { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;

        [Required]
        public string FilePath { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }

        [MaxLength]
        public string dbcstrJson { get; set; } = "{}";
        public virtual Batteries? Battery { get; set; }


    }
}
