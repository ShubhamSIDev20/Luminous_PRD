using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Utils;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.DTOs
{
    public class RealTimeRecordDto
    {
        public int Id { get; set; }
        public DateTime TimeStamp { get; set; }
        public int DeviceID { get; set; }
        public int SecondaryBoardNumber { get; set; }
        public int ChannelNumber { get; set; }
        public int SessionID { get; set; }
        public int BatteryID { get; set; }
        public int ProgramID { get; set; }
        public int StepNumber { get; set; }
        public byte QueryID { get; set; }
        public TimeSpan StepRunningTime { get; set; } = TimeSpan.Zero;
        public TimeSpan RunningTime { get; set; } = TimeSpan.Zero;   
     
        [UseChart]
        public float Current { get; set; } = 0;
        public int CurrentInt { get; set; } = 0;

        [UseChart]
        public float Voltage { get; set; } = 0;
        public int VoltageInt { get; set; } = 0;

        [UseChart]
        public float Power { get; set; } = 0;

        [UseChart]
        public float Temperature { get; set; } = 0;
        public int TemperatureInt { get; set; } = 0;

        [UseChart]
        public float AccumulatedCapacity { get; set; } = 0;

        [UseChart]
        public float ChargeCapacity { get; set; } = 0;

        [UseChart]
        public float DischargeCapacity { get; set; } = 0;

        [UseChart]
        public float StepCapacity { get; set; } = 0;

        [UseChart]
        public float AccumulatedEnergy { get; set; } = 0;

        [UseChart]
        public float ChargeEnergy { get; set; } = 0;

        [UseChart]
        public float DischargeEnergy { get; set; } = 0;

        [UseChart]
        public float StepEnergy { get; set; } = 0;
        public byte Operator { get; set; }
        public int CycleNumber { get; set; }
        public int CycleStatus { get; set; } //
        public int CycleRunIteration { get; set; }

        public int TableStepNumber { get; set; }
        public int TableTotalRowNumber { get; set; }

        public ProgramRunningStatus ProgramStatus { get; set; }
        public CircuitStatus CircuitStatus { get; set; } = CircuitStatus.Offline;
        public int? ErrorId { get; set; } 
        public int? SystemErrorId { get; set; }
        public bool RegistrationFlag { get; set; }

    }
}
