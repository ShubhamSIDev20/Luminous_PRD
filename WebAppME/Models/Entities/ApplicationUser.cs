using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace BatteryTestingSystem.Models.Entities
{
    public class ApplicationUser : IdentityUser
    {
        [MaxLength(100)]
        public string FullName { get; set; }
       
    }
}
