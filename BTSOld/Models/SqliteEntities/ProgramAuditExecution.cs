using BatteryTestingSystem.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.SqliteEntities
{
    public class ProgramAuditExecution
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public DateTime? LogTime { get; set; }
        public int? StepNumber { get; set; }
        public string? StepName { get; set; }
        public CircuitStatus? CircuitStatus { get; set; }
        public SeverityLevel? Severity { get; set; }
        public int Code { get; set; }
        public string? LogMessage { get; set; }
        public string? UserName { get; set; }

    }
}
