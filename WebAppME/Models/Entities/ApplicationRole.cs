using Microsoft.AspNetCore.Identity;

namespace BatteryTestingSystem.Models.Entities
{
    public class ApplicationRole : IdentityRole
    {
        public int rolePriority { get; set; } = 0;

    }
}
