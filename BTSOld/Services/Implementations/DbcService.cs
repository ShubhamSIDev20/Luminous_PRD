using Azure.Core;
using BatteryTestingSystem.Config;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.ViewModels;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Services.Interfaces;
using BatteryTestingSystem.Utils;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Components.Forms;
using Newtonsoft.Json;

namespace BatteryTestingSystem.Services.Implementations
{
    public class DbcService : IDbcService
    {
        private readonly IDbcRepository _repo;

        private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<DbcService>();
        public DbcService(IDbcRepository repo)
        {
            _repo = repo;
        }

        public async Task<CommonResponse<List<DbcFileRecordDto>>> GetByBatteryIdAsync(long batteryId)
        {
            var repoResult = await _repo.GetByBatteryIdAsync(batteryId);

            if (!repoResult.Success)
            {
                _logger.Error("Failed to load DBC files for battery {BatteryId}", batteryId);
                return CommonResponse<List<DbcFileRecordDto>>.Fail(repoResult.Message);
            }

            var data = repoResult.Data?.Select(Map).ToList() ?? new List<DbcFileRecordDto>();

            return CommonResponse<List<DbcFileRecordDto>>.Ok(data);
        }

        public async Task<CommonResponse<DbcFileRecordDto>> GetByIdAsync(long id)
        {
            var repoResult = await _repo.GetByIdAsync(id);

            if (!repoResult.Success)
                return CommonResponse<DbcFileRecordDto>.Fail(repoResult.Message);

            if (repoResult.Data == null)
                return CommonResponse<DbcFileRecordDto>.Fail($"DBC file with id {id} not found.");

            return CommonResponse<DbcFileRecordDto>.Ok(Map(repoResult.Data));
        }

        public async Task<CommonResponse<DbcFileRecordDto>> UploadAsync(DbcFileRequest request, IBrowserFile file)
        {
            try
            {
                var ext = Path.GetExtension(file.Name).ToLowerInvariant();

                if (ext != ".dbc")
                    return CommonResponse<DbcFileRecordDto>.Fail("Only .dbc files are allowed.");

                var existsResult = await _repo.ExistsAsync(request.BatteryId, request.Name, request.Version);

                if (existsResult.Success)
                    return CommonResponse<DbcFileRecordDto>.Fail(existsResult.Message);

                var folder = Path.Combine(GlobalConfig.AppSettings.Data, "DbcFiles");

                Directory.CreateDirectory(folder);

                var safeFile = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{SanitiseFileName(file.Name)}";
                var fullPath = Path.Combine(folder, safeFile);
                var relPath = Path.Combine("DbcFiles", safeFile);

                await using (var dest = File.Create(fullPath))
                {
                    await file.OpenReadStream().CopyToAsync(dest);
                }

                // Now the file is fully written & closed → safe to read
                DbcDatabase? dbc = new DbcParser()?.Parse(fullPath);

                var entity = new Models.Entities.DbcFileRecord
                {
                    BatteryId = request.BatteryId,
                    Name = request.Name.Trim(),
                    Version = request.Version.Trim(),
                    Description = request.Description?.Trim(),
                    OriginalFileName = file.Name,
                    FilePath = relPath,
                    FileSizeBytes = file.Size,
                    dbcstrJson = dbc != null ? Newtonsoft.Json.JsonConvert.SerializeObject(dbc) : "{}",
                    CreatedAt = DateTime.Now,
                    CreatedBy = CurrentUser.UserName,
                    UpdatedAt = DateTime.Now,
                    UpdatedBy = CurrentUser.UserName
                };

                var saveResult = await _repo.AddAsync(entity);

                if (!saveResult.Success || saveResult.Data == null)
                    return CommonResponse<DbcFileRecordDto>.Fail(saveResult.Message);

                _logger.Debug(
                    "DBC uploaded: {Name} v{Version} for battery {BatteryId} by {User}",
                    entity.Name,
                    entity.Version,
                    entity.BatteryId,
                    CurrentUser.UserName);

                return CommonResponse<DbcFileRecordDto>.Ok(Map(saveResult.Data), "DBC uploaded successfully.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Upload failed for battery {BatteryId}", request.BatteryId);
                return CommonResponse<DbcFileRecordDto>.Fail("Upload failed.");
            }

        }

        public async Task<CommonResponse<DbcFileRecordDto>> UpdateMetaAsync(DbcFileRequest request)
        {
            try
            {
                if (request.Id is null)
                    return CommonResponse<DbcFileRecordDto>.Fail("Id is required for update.");

                var checkexisting = await _repo.GetByIdAsync(request.Id.Value);
                if (!checkexisting.Success)
                    return CommonResponse<DbcFileRecordDto>.Fail("DBC file not found.");
               
                var existing = checkexisting.Data;
               
                // Duplicate check — exclude self
                var c = await _repo.ExistsAsync(existing.BatteryId, request.Name, request.Version, request.Id);
                if (c.Success)
                    return CommonResponse<DbcFileRecordDto>.Fail(
                        $"Another DBC named \"{request.Name}\" version \"{request.Version}\" already exists for this battery.");

                existing.Name = request.Name.Trim();
                existing.Version = request.Version.Trim();
                existing.Description = request.Description?.Trim();
                existing.UpdatedAt = DateTime.Now;
                existing.UpdatedBy = CurrentUser.UserName;

                await _repo.UpdateAsync(existing);
                _logger.Debug("DBC metadata updated: {Id}", existing.Id);

                return CommonResponse<DbcFileRecordDto>.Ok(Map(existing), "DBC updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Update failed for DBC {Id}", request.Id);
                return CommonResponse<DbcFileRecordDto>.Fail("Update failed. Please try again.");
            }
        }

        public async Task<CommonResponse<DbcFileRecordDto>> UpdateDbcDatabaseAsync(long Id, DbcDatabase database)
        {
            try
            {

                var checkexisting = await _repo.GetByIdAsync(Id);
                if (!checkexisting.Success)
                    return CommonResponse<DbcFileRecordDto>.Fail("DBC file not found.");
                var existing = checkexisting.Data;
                existing.dbcstrJson = JsonConvert.SerializeObject(database) ?? "{}";
                existing.UpdatedAt = DateTime.Now;
                existing.UpdatedBy = CurrentUser.UserName;

                await _repo.UpdateAsync(existing);
                _logger.Debug("DBC Database updated: {Id}", existing.Id);

                return CommonResponse<DbcFileRecordDto>.Ok(Map(existing), "DBC Database updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Update failed for DBC Database {Id}", Id);
                return CommonResponse<DbcFileRecordDto>.Fail("Update failed. Please try again.");
            }
        }

        public async Task<CommonResponse<bool>> DeleteAsync(long id, string? deletedBy = null)
        {
            var repoResult = await _repo.GetByIdAsync(id);

            if (!repoResult.Success || repoResult.Data == null)
                return CommonResponse<bool>.Fail("DBC file not found.");

            var entity = repoResult.Data;

            var fullPath = Path.Combine(GlobalConfig.AppSettings.Data, entity.FilePath);

            if (File.Exists(fullPath))
                File.Delete(fullPath);

            var deleteResult = await _repo.DeleteAsync(id);

            if (!deleteResult.Success)
                return CommonResponse<bool>.Fail(deleteResult.Message);

            _logger.Debug("DBC deleted {Id} by {User}", id, deletedBy ?? "system");

            return CommonResponse<bool>.Ok(true, "DBC file deleted.");
        }

        public async Task<CommonResponse<string>> GetFilePathAsync(long id)
        {
            var repoResult = await _repo.GetByIdAsync(id);

            if (!repoResult.Success || repoResult.Data == null)
                return CommonResponse<string>.Fail("DBC file not found.");

            var fullPath = Path.Combine(GlobalConfig.AppSettings.Data, repoResult.Data.FilePath);

            if (!File.Exists(fullPath))
                return CommonResponse<string>.Fail("Physical file missing.");

            return CommonResponse<string>.Ok(fullPath);
        }

        private static DbcFileRecordDto Map(DbcFileRecord e) => new()
        {
            Id = e.Id,
            BatteryId = e.BatteryId,
            Name = e.Name,
            Version = e.Version,
            Description = e.Description,
            OriginalFileName = e.OriginalFileName,
            StoredFilePath = e.FilePath,
            FileSizeBytes = e.FileSizeBytes,
            DbcstrJson = e.dbcstrJson,
            IsDeleted = e.IsDeleted,
            CreatedAt = e.CreatedAt,
            CreatedBy = e.CreatedBy,
            UpdatedAt = e.UpdatedAt,
            UpdatedBy = e.UpdatedBy,
        };

        private static string SanitiseFileName(string fileName)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');
            return fileName;
        }
    }
}
