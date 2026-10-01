namespace BatteryTestingSystem.Config;

/// <summary>
/// JWT configuration settings
/// </summary>
public class JwtSettings
{
    public string SecretKey { get; set; } = "s7K9mP2xQvL4nR8wT1uY6aB3cF5hJ0eI+dG7kN2oW4zA1yX6bM9qV3pE8tH5jU=";
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 30;
    public int IdTokenExpirationMinutes { get; set; } = 60;
    public int ClockSkewMinutes { get; set; } = 5;
}

/// <summary>
/// CORS configuration
/// </summary>
public class CorsSettings
{
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    public string[] AllowedMethods { get; set; } = Array.Empty<string>();
    public string[] AllowedHeaders { get; set; } = Array.Empty<string>();
    public bool AllowCredentials { get; set; } = true;
    public int MaxAgeSeconds { get; set; } = 3600;
}

public class AppSettings
{
    public string Data { get; set; }
    public string DbEncryption { get; set; } = "abcdefghijklmnopqrstuvwxyz";


}

public static class GlobalConfig
{
    public static AppSettings AppSettings { get; set; }

}
