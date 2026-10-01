
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services;
using BatteryTestingSystem.Services.Interfaces;
using BatteryTestingSystem.Utils;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BatteryTestingSystem.Pages
{
    public class LogoutModel : PageModel
    {

        private readonly SignInManager<ApplicationUser> SignInManager;
        private readonly IAuditService _auditService;
        private ServerSessionStorageService storageService;
        public LogoutModel(
            SignInManager<ApplicationUser> _signInManager, 
            IAuditService auditService,
            ServerSessionStorageService SService)
        {
            SignInManager = _signInManager;
            _auditService = auditService;
            storageService = SService;

        }

        public async Task<IActionResult> OnGet(string id = "", string user = "")
        {
            // ✅ Get client info
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();

            var sId = HttpContext.User.FindFirst("sId")?.Value;

            if (!string.IsNullOrEmpty(sId))
            {
                //Clear tabViewer data from session storage
                storageService.RemoveComponentState($"{sId}_tabData");
            }

            var Name = HttpContext.User.FindFirst("Name")?.Value;

            await SignInManager.SignOutAsync();

            // ✅ Audit log (for database history)
            await _auditService.LogEventAsync(new AuditLogDto
            {
                User = Name,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                Module = ModuleName.USER,
                Action = AuditActionType.LOGOUT,
                Details = $"User {user} logged out successfully.",
                Status = SeverityLevel.WARNING,
                EntityId = sId
            });
           
            return Redirect("/login");
        }
    }
}
