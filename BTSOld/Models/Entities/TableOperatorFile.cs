using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities
{
    [Table(name: "TableFiles", Schema = "Files")]

    public class TableOperatorFile : BaseEntity
    {

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [MaxLength(8)]
        public string Name { get; set; }

        [MaxLength(200)]
        public string? Description { get; set; }

        [Required]
        public string FilePath { get; set; }

        [Required]
        public long Size { get; set; }

        [Required]
        public string FileHash { get; set; }

    }
}
