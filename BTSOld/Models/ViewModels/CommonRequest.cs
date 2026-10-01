namespace BatteryTestingSystem.Models.APIModels
{
    public class CommonRequest
    {
        public int DeviceID { get; set; }
        public int CircuitID { get; set; }
        public int? ProgramId { get; set; }   // for SetProgram
        public int? BatteryId { get; set; }   // for SetProgram
        public int? dbcId { get; set; } = 0;

    }
}
