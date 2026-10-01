namespace BatteryTestingSystem.Models.ViewModels
{
    public class AuditLogQueryParameters
    {
        public Models.Enums.ModuleName? Module { get; set; }
        public Models.Enums.AuditActionType? Action { get; set; }
        public string? User { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? EndDate { get; set; }

    }
}
