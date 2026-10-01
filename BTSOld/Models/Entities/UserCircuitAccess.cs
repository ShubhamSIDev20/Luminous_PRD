using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace BatteryTestingSystem.Models.Entities
{
    [Table("UserCircuitAccess", Schema = "auth")]

    public class UserCircuitAccess
    {
         [Key]
            [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
            public long Id { get; set; }

            [Required]
            public string UserId { get; set; } = string.Empty;

            [Required]
            public int DeviceId { get; set; }

            [Required]
            public int CircuitId { get; set; }

            // Navigation
            [ForeignKey(nameof(UserId))]
            public ApplicationUser? User { get; set; }
    }
    
}
