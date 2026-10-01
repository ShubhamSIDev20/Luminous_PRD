using BatteryTestingSystem.Config;
using BatteryTestingSystem.Models.DTOs;
using System.Globalization;

namespace BatteryTestingSystem.Services;
public static class FileManagerService
{
    private static string _folderPath = Path.Combine(GlobalConfig.AppSettings.Data, "TableFiles");

    static FileManagerService()
    {
        // Ensure folder exists
        if (!Directory.Exists(_folderPath))
        {
            Directory.CreateDirectory(_folderPath);
        }
    }

    public static void SetFolderPath(string path)
    {
        _folderPath = path;
        if (!Directory.Exists(_folderPath))
        {
            Directory.CreateDirectory(_folderPath);
        }
    }

    public static List<FileInfoModel> GetAllFiles()
    {
        var files = Directory.GetFiles(_folderPath, "*.txt")
            .Select(f => new FileInfo(f))
            .Select(fi =>
            {
                var fileModel = new FileInfoModel
                {
                    FileName = fi.Name,
                    FileNameWithoutExt = Path.GetFileNameWithoutExtension(fi.Name),
                    FullPath = fi.DirectoryName,
                    Size = fi.Length,
                    SizeInKB = Math.Round(fi.Length / 1024.0, 2),
                    CreatedDate = fi.CreationTime,
                    ModifiedDate = fi.LastWriteTime
                };

                // Validate file content
                var content = File.ReadAllText(fi.FullName);
                var validationResult = ValidateFileContent(content);
                fileModel.IsValid = validationResult.Success;
                fileModel.ValidationErrors = validationResult.Data ?? new List<string>();

                return fileModel;
            })
            .OrderByDescending(f => f.ModifiedDate)
            .ToList();

        return files;
    }

    public static CommonResponse<List<string>> ValidateFile(string FileName)
    {
        var Isfile = ReadFileContent(FileName);
        
        if (Isfile.Success)
        {
            return ValidateFileContent(Isfile.Data ?? string.Empty);
        }
        else
        {
            return CommonResponse<List<string>>.Fail(Isfile.Message);
        }
    }

    public static CommonResponse<List<string>> ValidateFileContent(string content)
    {
        var errors = new List<string>();
        int lineNum = 1;

        if (string.IsNullOrWhiteSpace(content))
        {
            return CommonResponse<List<string>>.Fail("File content is empty or null.");
        }

        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        foreach (var raw in lines)
        {
            string line = raw?.Trim() ?? string.Empty;

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                lineNum++;
                continue;
            }

            var parts = line.Split(';', StringSplitOptions.None);

            // Remove trailing empty column if line ends with ;
            if (parts.Length > 0 && string.IsNullOrEmpty(parts[^1]))
                parts = parts[..^1];

            if (parts.Length < 1 || parts.Length > 4)
            {
                errors.Add($"Line {lineNum}: Allowed values = 1 to 4, found {parts.Length}");
                lineNum++;
                continue;
            }

            // X (Time) - required
            if (!TryParseTime(parts[0], out _))
            {
                errors.Add($"Line {lineNum}: First value X (time) is missing or invalid → '{parts[0]}'");
                lineNum++;
                continue;
            }

            // Optional numeric values
            TryParseFloat(parts, 1, out string? errY);
            if (errY != null)
                errors.Add($"Line {lineNum}: Invalid Y value → '{errY}'");

            TryParseFloat(parts, 2, out string? errZ);
            if (errZ != null)
                errors.Add($"Line {lineNum}: Invalid Z value → '{errZ}'");

            TryParseFloat(parts, 3, out string? errU);
            if (errU != null)
                errors.Add($"Line {lineNum}: Invalid U value → '{errU}'");

            lineNum++;
        }

        return errors.Count == 0
            ? CommonResponse<List<string>>.Ok(errors, "File is valid.")
            : CommonResponse<List<string>>.Fail("File validation failed.", errors);
    }

    private static bool TryParseTime(string input, out int ms)
    {
        ms = 0;
        input = input.Trim().ToLower();

        // Extract numeric part
        string number = new string(input.TakeWhile(c =>
            char.IsDigit(c) || c == '.' || c == '-'
        ).ToArray());

        if (!double.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
            return false;

        // Extract unit
        string? unit = input.Length > number.Length
            ? input[number.Length..].Trim().ToLowerInvariant()
            : null;
        double multiplier = 0;
       
        if (!string.IsNullOrWhiteSpace(unit))
        {
            multiplier = unit switch
            {
                "s" or "sec" or "second" or "seconds" => 1000,
                "m" or "min" or "minute" or "minutes" => 60000,
                "h" or "hr" or "hour" or "hours" => 3600000,
                "" => 1000,
                _ => 0
            };
        }
        else
        {
            multiplier = 1000; // Default to seconds
        }


        if (multiplier == 0)
            return false;

        double msDouble = value * multiplier;

        // Range check for int32
        if (msDouble < int.MinValue || msDouble > int.MaxValue)
            return false;

        ms = (int)msDouble;
        return true;
    }

    private static void TryParseFloat(string[] parts, int index, out string? error)
    {
        error = null;

        if (index >= parts.Length)
            return; // Optional field not present

        var value = parts[index]?.Trim();

        if (string.IsNullOrWhiteSpace(value))
            return; // Empty optional field is OK

        if (!double.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            error = value;
        }
    }

    public static async Task<CommonResponse<bool>> SaveFileAsync(string fileName, Stream fileStream)
    {
        try
        {
            // Validate filename
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            if (nameWithoutExt.Length > 8)
            {
                return CommonResponse<bool>.Fail("Filename must be 8 characters or less (excluding extension)");
            }

            if (!fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                return CommonResponse<bool>.Fail("Only .txt files are allowed");
            }

            var filePath = Path.Combine(_folderPath, fileName);

            // Read content for validation
            string content;
            using (var reader = new StreamReader(fileStream, leaveOpen: true))
            {
                content = await reader.ReadToEndAsync();
            }

            // Validate content before saving
            var validationResult = ValidateFileContent(content);
            if (!validationResult.Success)
            {
                var errorMsg = "File validation failed:\n" + string.Join("\n", validationResult.Data?.Take(5) ?? new List<string>());
                if (validationResult.Data?.Count > 5)
                {
                    errorMsg += $"\n... and {validationResult.Data.Count - 5} more errors";
                }
                return CommonResponse<bool>.Fail(errorMsg);
            }

            // Save file
            await File.WriteAllTextAsync(filePath, content);

            return CommonResponse<bool>.Ok(true, "File uploaded and validated successfully");
        }
        catch (Exception ex)
        {
            return CommonResponse<bool>.Fail($"Error: {ex.Message}");
        }
    }

    public static CommonResponse<string[]> ReadFileLines(string fileName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return CommonResponse<string[]>.Fail("File name is required");
            }

            var filePath = Path.Combine(_folderPath, fileName);

            if (!File.Exists(filePath))
            {
                return CommonResponse<string[]>.Fail("File not found");
            }

            var content = File.ReadAllLines(filePath);

            return CommonResponse<string[]>.Ok(content);
        }
        catch (Exception ex)
        {
            return CommonResponse<string[]>.Fail($"Error reading file: {ex.Message}");
        }
    }

    public static CommonResponse<string> ReadFileContent(string fileName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return CommonResponse<string>.Fail("File name is required");
            }

            var filePath = Path.Combine(_folderPath, fileName);

            if (!File.Exists(filePath))
            {
                return CommonResponse<string>.Fail("File not found");
            }

            var content = File.ReadAllText(filePath);

            return CommonResponse<string>.Ok(content);
        }
        catch (Exception ex)
        {
            return CommonResponse<string>.Fail($"Error reading file: {ex.Message}");
        }
    }


    public static async Task<CommonResponse<bool>> DeleteFileAsync(string fileName)
    {
        try
        {
            var filePath = Path.Combine(_folderPath, fileName);

            if (!File.Exists(filePath))
                return CommonResponse<bool>.Fail("File not found");

            await Task.Run(() => File.Delete(filePath));

            return CommonResponse<bool>.Ok(true, "File deleted successfully");
        }
        catch (Exception ex)
        {
            return CommonResponse<bool>.Fail($"Error: {ex.Message}");
        }
    }

    public static async Task<CommonResponse<bool>> UpdateFileAsync(string fileName, string content)
    {
        try
        {
            var validationResult = ValidateFileContent(content);
            if (!validationResult.Success)
            {
                var errorMsg = "File validation failed:\n" +
                               string.Join("\n", validationResult.Data?.Take(5) ?? Enumerable.Empty<string>());

                if (validationResult.Data?.Count > 5)
                {
                    errorMsg += $"\n... and {validationResult.Data.Count - 5} more errors";
                }

                return CommonResponse<bool>.Fail(errorMsg);
            }

            var filePath = Path.Combine(_folderPath, fileName);

            await File.WriteAllTextAsync(filePath, content);

            return CommonResponse<bool>.Ok(true, "File updated and validated successfully");
        }
        catch (Exception ex)
        {
            return CommonResponse<bool>.Fail($"Error: {ex.Message}");
        }
    }

}

// FileInfoModel.cs - Model Class
public class FileInfoModel
{
    public string FileHash { get; set; }
    public string FileName { get; set; }
    public string FileNameWithoutExt { get; set; }
    public string FullPath { get; set; }
    public long Size { get; set; }
    public double SizeInKB { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public bool IsValid { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
}
