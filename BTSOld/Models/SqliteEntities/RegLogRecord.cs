using BatteryTestingSystem.Models.Enums;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.SqliteEntities
{
    /// <summary>
    /// A REG-operator measurement row tagged with a Nominal Value label (StepModel.NominalValues[0]).
    /// Stored in its own table (separate from Measurements) so labeled REG data can be reviewed
    /// independently in the Data Viewer without appearing in the main session chart/table.
    /// Field shape mirrors MeasurementData so the Data Viewer's existing table rendering can be reused.
    /// </summary>
    public class RegLogRecord
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>The Nominal Value text on the REG step (StepModel.NominalValues[0]), ≤8 chars. Grouping key.</summary>
        [MaxLength(8)]
        public string RegLabel { get; set; } = string.Empty;

        public int SessionID { get; set; }
        public int DeviceId { get; set; }
        public int CircuitId { get; set; }

        public int StepNumber { get; set; }
        public int Operator { get; set; }
        public CircuitStatus CircuitStatus { get; set; }
        public DateTime DateTime { get; set; }

        public int ProgramRunningTime { get; set; }

        public float? Current { get; set; }
        public float? Voltage { get; set; }
        public float? Temperature { get; set; }
        public float? Power { get; set; }
        public float? AccumulatedCapacity { get; set; }
        public float? ChargeCapacity { get; set; }
        public float? DischargeCapacity { get; set; }
        public float? StepCapacity { get; set; }
        public float? AccumulatedEnergy { get; set; }
        public float? ChargeEnergy { get; set; }
        public float? DischargeEnergy { get; set; }
        public float? StepEnergy { get; set; }

        public int? SystemErrorID { get; set; }
        public int? ErrorId { get; set; }
        public int? MessageId { get; set; }
        public string? Remark { get; set; } = string.Empty;
        public string? dbcValues { get; set; } = string.Empty;

        [NotMapped]
        public Dictionary<string, object?> DbcValuesParsed
        {
            get
            {
                if (string.IsNullOrEmpty(dbcValues))
                    return new Dictionary<string, object?>();

                return JsonConvert.DeserializeObject<Dictionary<string, object?>>(dbcValues)
                       ?? new Dictionary<string, object?>();
            }
            set
            {
                dbcValues = JsonConvert.SerializeObject(value);
            }
        }
    }
}
