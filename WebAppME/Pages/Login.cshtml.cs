using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services.Interfaces;
using BatteryTestingSystem.Utils;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace BatteryTestingSystem.Pages
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public LoginModel(SignInManager<ApplicationUser> signInManager, 
            UserManager<ApplicationUser> userManager,
            IAuditService auditService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _auditService = auditService;
        }

        [BindProperty] public string Email { get; set; }
        [BindProperty] public string Password { get; set; }
        [BindProperty] public bool RememberMe { get; set; }

        public async Task<IActionResult> OnGet()
        {
            if (_signInManager.IsSignedIn(User))
            {
                // Land on the TabViewer shell, not the canvas's own /workflows route: this app hosts
                // pages as tabs, and routing straight to the page renders it bare, with no tab bar
                // or nav. TabService seeds the canvas as the default tab instead (WorkflowDefaultTab).
                return Redirect("/");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            // ✅ Get client info
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();

            var user = await _userManager.FindByEmailAsync(Email);

            if (user == null)
            {
                user = await _userManager.FindByNameAsync(Email);

                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid credentials.");

                    // 🔹 User not found
                    await _auditService.LogEventAsync(new AuditLogDto
                    {
                        User = Email,
                        IPAddress= ipAddress,
                        UserAgent = userAgent,
                        Module = ModuleName.USER,
                        Action = AuditActionType.LOGIN,
                        Details = $"Attempted login with email/username: {Email}",
                        Status = Models.Enums.SeverityLevel.WARNING
                    });

                    return Page();
                }                 
            }

            //var result = await _signInManager.PasswordSignInAsync(user.UserName, Password, RememberMe, false);
       
            var result = await _signInManager.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                var existingClaims = await _userManager.GetClaimsAsync(user);

                var claims = existingClaims.ToList();

                var sessionId = Guid.NewGuid().ToString("N");
                claims.Add(new Claim("sId", sessionId));

                await _signInManager.SignInWithClaimsAsync(
                    user,
                    isPersistent: RememberMe,
                    claims
                );

                // 🔹 Successful login
                await _auditService.LogEventAsync(new AuditLogDto
                {
                    User = Email,
                    IPAddress = ipAddress,
                    UserAgent = userAgent,
                    Module = ModuleName.USER,
                    Action = AuditActionType.LOGIN,
                    Details = $"User {user.UserName} logged in successfully.",
                    Status = Models.Enums.SeverityLevel.WARNING,
                    EntityId = user.Id                  
                });

                // Land on the TabViewer shell, not the canvas's own /workflows route: this app hosts
                // pages as tabs, and routing straight to the page renders it bare, with no tab bar
                // or nav. TabService seeds the canvas as the default tab instead (WorkflowDefaultTab).
                return Redirect("/");

            }
            else if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "User account is locked.");
              
                await _auditService.LogEventAsync(new AuditLogDto
                {
                    User = Email,
                    IPAddress = ipAddress,
                    UserAgent = userAgent,
                    Module = ModuleName.USER,
                    Action = AuditActionType.LOGIN,
                    Details = $"User {user.UserName} account is locked out.",
                    Status = Models.Enums.SeverityLevel.WARNING,
                    EntityId = user.Id                
                });

            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                await _auditService.LogEventAsync(new AuditLogDto
                {
                    User = Email,
                    IPAddress = ipAddress,
                    UserAgent = userAgent,
                    Module = ModuleName.USER,
                    Action = AuditActionType.LOGIN,
                    Details = $"Invalid password for user {user.UserName}.",
                    Status = Models.Enums.SeverityLevel.WARNING,
                    EntityId = user.Id
                  
                });
            }


            return Page();
        }
    }
}
