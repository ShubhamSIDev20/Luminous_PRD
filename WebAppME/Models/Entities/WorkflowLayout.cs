using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities
{
    /// <summary>
    /// One saved canvas arrangement. The graph itself is an opaque JSON document (spec D7) so the
    /// node shape can change freely during the experiment without a migration each time.
    /// There are deliberately no foreign keys to Device / Channel / Program: ids inside the blob
    /// are soft references, and deleting hardware must never break or cascade into a layout.
    /// </summary>
    [Table(name: "WorkflowLayouts", Schema = "Device")]
    public class WorkflowLayout : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(400)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(450)]                        // matches AspNetUsers.Id length
        public string OwnerUserId { get; set; } = string.Empty;

        [Required]
        public string LayoutJson { get; set; } = string.Empty;

        public int SchemaVersion { get; set; } = 1;
    }
}
