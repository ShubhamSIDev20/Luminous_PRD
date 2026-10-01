using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities
{
    [Table(name: "CodeMessages", Schema = "Programs")]

    public class CodeMessage : BaseEntity
    {

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int Index { get; set; }
        
        [Required]
        public int Type { get; set; }

        [MaxLength(500)]
        public string? Message { get; set; } = string.Empty;
    }
}
