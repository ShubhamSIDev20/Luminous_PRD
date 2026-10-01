using System.ComponentModel.DataAnnotations;

namespace BatteryTestingSystem.Models.DTOs
{
    public class RegistrationStandardsDTO
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(8)]
        [Display(Name = "Standard Name")]
        [StringLength(8, ErrorMessage = "Max Length 8")]
        public string StandardName { get; set; }
        public string? Description { get; set; }
        public List<string>? UnitList { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false;
    }
}
