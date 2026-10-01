namespace BatteryTestingSystem.Models.APIModels
{
    public class CommonRequest
    {
        public int DeviceID { get; set; }
        public int SecondaryBoardNumber { get; set; } = 1;
        public List<int> ChannelList { get; set; } = new();

        public int? ProgramId { get; set; }   // for SetProgram
        public int? BatteryId { get; set; }   // for SetProgram
        public int? dbcId { get; set; } = 0;
    }
}
