using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities.Program
{
    [Table(name: "Standards", Schema = "Programs")]

    public class RegistrationStandard : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(8)]
        [Display(Name = "Standard Name")]
        [StringLength(8, ErrorMessage = "Max Length 8")]
        public string StandardName { get; set; }
        public string? Description { get; set; }
        public List<string>? UnitList { get; set; }

    }
}
