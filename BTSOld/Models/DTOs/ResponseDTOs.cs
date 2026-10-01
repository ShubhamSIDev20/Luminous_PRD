using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Models.DTOs;

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string? ErrorDescription { get; set; }
    public string? ErrorUri { get; set; }
    public string? State { get; set; }
}

public class txtFileLoadResult
{
    public string Hash { get; set; } = string.Empty;
    public string[] Lines { get; set; } = Array.Empty<string>();
}
public class CommonResponse<T>
{
    public bool Success { get; set; } = false;
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; } = default;

    // Helper: return success
    public static CommonResponse<T> Ok(T data, string message = "Success")
    {
        return new CommonResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    // Helper: return failure
    public static CommonResponse<T> Fail(string message, T data = default)
    {
        return new CommonResponse<T>
        {
            Success = false,
            Message = message,
            Data = data
        };
    }
}

public class IOStatus
{
    public int Id { get; set; }
    public int Value { get; set; }
    public string Board { get; set; }
    public string Type { get; set; } // "DI" or "DO"
}

public class AuditResponce 
{
    public int TotalRecords { get; set; } = 0;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public List<AuditLogDto> Logs { get; set; } = new List<AuditLogDto>();

}
