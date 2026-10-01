using BatteryTestingSystem.Models.Entities;

namespace BatteryTestingSystem.Services.Auth
{
    public class GlobalState
    {
        public ApplicationUser applicationUser { get; set; } = new ApplicationUser();
        public ApplicationRole applicationRole { get; set; } = new ApplicationRole();

    }
}
