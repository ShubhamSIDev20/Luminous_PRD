using BatteryTestingSystem.Components.UI;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BatteryTestingSystem.Models
{
    public static class InitializeDataSeeder
    {

        public static async Task DefaultAuthAsync(IServiceProvider serviceProvider)
        {

            var roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();

            string[] roleNames = { "Administrator", "Supervisor", "Operator", "Maintenance" };

            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    var role = new ApplicationRole
                    {
                        Name = roleName,
                        rolePriority = Array.IndexOf(roleNames, roleName) + 1 // Assign priority based on index
                    };

                    var result = await roleManager.CreateAsync(role);

                }
                else
                {
                    var existingRole = await roleManager.FindByNameAsync(roleName);
                    if (existingRole != null && existingRole.rolePriority != Array.IndexOf(roleNames, roleName) + 1)
                    {
                        existingRole.rolePriority = Array.IndexOf(roleNames, roleName) + 1;
                        var updateResult = await roleManager.UpdateAsync(existingRole);

                    }
                }
            }

            // Bootstrap admin: runs in every environment (prod needs a way in on first run),
            // but the password must come from configuration — no hardcoded fallback — and
            // it only ever creates the account if one doesn't already exist.
            var adminPassword = configuration["AppSettings:Password"];

            if (!string.IsNullOrEmpty(adminPassword))
            {
                var adminEmail = "sysint1@adorpower.com";

                var adminUser = await userManager.FindByEmailAsync(adminEmail);

                if (adminUser == null)
                {
                    var user = new ApplicationUser
                    {
                        UserName = "admin",
                        Email = adminEmail,
                        FullName = "System Administrator",
                        PhoneNumber = "9975001173",
                        EmailConfirmed = true,
                    };

                    var result = await userManager.CreateAsync(user, adminPassword);

                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, "Administrator");
                    }
                }
            }

            // Test accounts for each non-admin role (screenshot/UI-review accounts only) —
            // Development-only, passwords sourced from configuration.
            if (!environment.IsDevelopment())
            {
                return;
            }

            var testAccounts = new (string UserName, string Email, string FullName, string Role)[]
            {
                ("test.supervisor", "test.supervisor@adorpower.com", "Test Supervisor", "Supervisor"),
                ("test.operator", "test.operator@adorpower.com", "Test Operator", "Operator"),
                ("test.maintenance", "test.maintenance@adorpower.com", "Test Maintenance", "Maintenance"),
            };

            foreach (var account in testAccounts)
            {
                var password = configuration[$"TestAccounts:{account.Role}:Password"];

                if (string.IsNullOrEmpty(password))
                {
                    continue;
                }

                var existingTestUser = await userManager.FindByEmailAsync(account.Email);

                if (existingTestUser == null)
                {
                    var testUser = new ApplicationUser
                    {
                        UserName = account.UserName,
                        Email = account.Email,
                        FullName = account.FullName,
                        EmailConfirmed = true,
                    };

                    var testResult = await userManager.CreateAsync(testUser, password);

                    if (testResult.Succeeded)
                    {
                        await userManager.AddToRoleAsync(testUser, account.Role);
                    }
                }
            }
        }
          

        public static async Task DefaultRegistrationStandard(IServiceProvider serviceProvider)
        {
            var PService = serviceProvider.GetRequiredService<IProgramServices>();

            if (PService != null)
            {  
                var Standards = new List<RegistrationStandardsDTO>
                {
                    new RegistrationStandardsDTO
                    {
                        StandardName = "STANDARD",
                        Description = "STANDARD Registration Method",
                        UnitList = new List<string> { "A", "V", "C", "W" ,"Ah" }
                    },
                    new RegistrationStandardsDTO
                    {
                        StandardName = "SIMPLE",
                        Description = "Simple Registration Method",
                        UnitList = new List<string> { "A", "V", "C", "W" }
                    }
                };

                foreach (var standard in Standards)
                {

                    var existingStandardsResponse = await PService.GetRegistrationStandardAsync();
                    if (existingStandardsResponse.Success && existingStandardsResponse.Data != null)
                    {
                        var existingStandard = existingStandardsResponse.Data.FirstOrDefault(s => s.StandardName.Equals(standard.StandardName, StringComparison.OrdinalIgnoreCase));
                        if (existingStandard == null)
                        {
                            await PService.CreateRegistrationStandardAsync(standard);
                        }
                    }

                }

            }

            await RStandards.GetStandardsAsync(true);
        }

    }
}
