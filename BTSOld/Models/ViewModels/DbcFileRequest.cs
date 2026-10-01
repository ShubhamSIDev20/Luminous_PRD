namespace BatteryTestingSystem.Models.ViewModels
{
    public class DbcFileRequest
    {
        public long? Id { get; set; }
        public long BatteryId { get; set; }
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Name is required")]
        public string Name { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Version is required")]
        public string Version { get; set; } = string.Empty;
        public string? Description { get; set; }

    }
}
