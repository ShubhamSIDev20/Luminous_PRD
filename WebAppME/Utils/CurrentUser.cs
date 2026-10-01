using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using UAParser;

namespace BatteryTestingSystem.Utils
{
    public static class CurrentUser
    {
        private static IHttpContextAccessor? _httpContextAccessor;

        // Configure once at startup
        public static void Configure(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // Get the username (or email)
        public static string UserName
        {
            get
            {
                var user = _httpContextAccessor?.HttpContext?.User;

                if (user?.Identity?.IsAuthenticated == true)
                {
                    return user.Identity.Name
                           ?? user.FindFirst("preferred_username")?.Value
                           ?? "UNKNOWN_USER";
                }

                return string.Empty;
            }
        }

        public static ClaimsPrincipal? User
        {
            get
            {
                var user = _httpContextAccessor?.HttpContext?.User;

                if (user?.Identity?.IsAuthenticated == true)
                {
                    return user;
                }

                return null;
            }
        }
        // Optional: get user ID / object ID
        public static string? UserId =>
            _httpContextAccessor?.HttpContext?.User?
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        public static string? SessionId =>
         _httpContextAccessor?.HttpContext?.User?
             .FindFirst("sId")?.Value;

        public static string? IpAddress {

            get
            {
                try
                {
                    return _httpContextAccessor?.HttpContext?.Connection?.RemoteIpAddress?.ToString();
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
            
        public static ClientInfo? UserAgent
        {
            get
            {
                try
                {
                    string uaString = _httpContextAccessor?.HttpContext?.Request?.Headers["User-Agent"].ToString();
                    var parser = Parser.GetDefault();
                    ClientInfo client = parser.Parse(uaString);
                    return client;
                }
                catch
                {
                    return null;
                }
            }
        }

    }
}
