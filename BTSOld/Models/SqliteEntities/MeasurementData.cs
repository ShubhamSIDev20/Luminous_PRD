using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Utils;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.SqliteEntities
{
    public class MeasurementData
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; } // Primary key for SQLite
        public int SessionID { get; set; } // Primary key for SQLites
        public int DeviceId { get; set; }
        public int CircuitId { get; set; }
       
        public int StepNumber { get; set; }
        public int Operator { get; set; }
        public CircuitStatus CircuitStatus { get; set; }
        public DateTime DateTime { get; set; }

        /// <summary>
        /// 0x21 - Time (timestamp)
        /// 0 indexed
        /// </summary>
        public int ProgramRunningTime { get; set; }

        /// <summary>
        /// 0x22 - Current (A)
        /// 1 indexed
        /// </summary>
        
        [UseChart]
        public float? Current { get; set; }

        /// <summary>
        /// 0x23 - Voltage (V)
        /// 2 indexed
        /// </summary>
        [UseChart]
        public float? Voltage { get; set; }

        /// <summary>
        /// 0x24 - Temperature (°C)
        /// 3 indexed
        /// </summary>
        [UseChart]
        public float? Temperature { get; set; }

        /// <summary>
        /// 0x25 - Power (W)
        /// 4 indexed
        /// </summary>
        [UseChart]
        public float? Power { get; set; }

        /// <summary>
        /// 0x26 - Accumulated Capacity (Ah)
        /// 5 indexed
        /// </summary>
        [UseChart]
        public float? AccumulatedCapacity { get; set; }

        /// <summary>
        /// 0x27 - Charge Capacity (AhCha)
        /// 6 indexed
        /// </summary>
        [UseChart]
        public float? ChargeCapacity { get; set; }

        /// <summary>
        /// 0x28 - Discharge Capacity (AhDch)
        /// 7 indexed
        /// </summary>
        [UseChart]
        public float? DischargeCapacity { get; set; }

        /// <summary>
        /// 0x29 - Step Capacity (AhStep)
        /// 8 indexed
        /// </summary>
        [UseChart]
        public float? StepCapacity { get; set; }

        /// <summary>
        /// 0x2A - Accumulated Energy (Wh)
        /// 9 indexed
        /// </summary>
        [UseChart]
        public float? AccumulatedEnergy { get; set; }

        /// <summary>
        /// 0x2B - Charge Energy (WhCha)
        /// 10 indexed
        /// </summary>
        [UseChart]
        public float? ChargeEnergy { get; set; }

        /// <summary>
        /// 0x2C - Discharge Energy (WhDch)
        /// 11 indexed
        /// </summary>
        [UseChart]
        public float? DischargeEnergy { get; set; }

        /// <summary>
        /// 0x2D - Step Energy (WhStep)
        /// 12 indexed
        /// </summary>
        [UseChart]
        public float? StepEnergy { get; set; }

        public int? SystemErrorID { get; set; }
        
        public int? ErrorId { get; set; }

        public int? MessageId { get; set; }
        public string? Remark { get; set; } = string.Empty;
        public string? dbcValues { get; set; } = string.Empty ;

        // ✅ This is used in your app (Dictionary) — EF ignores it
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
