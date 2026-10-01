using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities
{
    [Table(name: "SecondaryBoards", Schema = "Device")]
    public class SecondaryBoard : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        public int DeviceId { get; set; }

        [Range(0, 8)]
        public int BoardNumber { get; set; }

        public bool IsImplicit { get; set; } = false;
    }
}
