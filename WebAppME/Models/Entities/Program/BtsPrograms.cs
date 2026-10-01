using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace BatteryTestingSystem.Models.Entities.Program
{
    [Table(name: "Programs", Schema = "Program")]

    public class BtsPrograms : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        [Required]
        [MaxLength(200)]
        [Display(Name = "Program Name")]
        [StringLength(200, ErrorMessage = "Max Length 200")]
        public string ProgramName { get; set; }
        public string? Description { get; set; }
        public float? MaxAh { get; set; }
        public int? ProgramSteps { get; set; }
        public string? ProgramJson { get; set; }
        public long? ProgramTimeTicks { get; set; }
        public string? ProgramHash { get; set; }
        public bool IsVaild { get; set; } = false;

    }
}
