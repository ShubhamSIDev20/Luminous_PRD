using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.ViewModels;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Services.Interfaces;

namespace BatteryTestingSystem.Services.Implementations
{
    public class AuditService : IAuditService
    {
        private readonly IAuditRepository _auditRepository;

        public AuditService(IAuditRepository auditRepository)
        {
            _auditRepository = auditRepository;
        }

        public async Task<CommonResponse<List<AuditLogDto>>> GetAuditLogsAsync(AuditLogQueryParameters parameters)
        {
            var logsResponse = await _auditRepository.GetAuditRecordsAsync(parameters);

            var response = new CommonResponse<List<AuditLogDto>>();
            if (logsResponse.Success && logsResponse.Data != null)
            {
                var dtoList = logsResponse.Data.Select(e => ToDto(e)).ToList();
                response = CommonResponse<List<AuditLogDto>>.Ok(dtoList);
            }
            else
            {
                response = CommonResponse<List<AuditLogDto>>.Fail("No audit logs found or failed to fetch logs.");
            }

            return response;
        }

        public async Task LogEventAsync(AuditLogDto auditEntry)
        {
            if (auditEntry == null) return;
            var entity = ToEntity(auditEntry);
            await _auditRepository.LogEventAsync(entity);
        }
        public AuditLogDto ToDto(AuditLog entity)
        {
            if (entity == null) return null!;

            return new AuditLogDto
            {
                LogId = entity.LogId,
                Timestamp = entity.Timestamp,
                User = entity.User,
                Action = entity.Action,
                Module = entity.Module,
                EntityId = entity.EntityId,
                Status = entity.Status,
                IPAddress = entity.IPAddress,
                UserAgent = entity.UserAgent,
                Details = entity.Details,
                Metadata = entity.Metadata
            };
        }

        public AuditLog ToEntity(AuditLogDto dto)
        {
            if (dto == null) return null!;

            return new AuditLog
            {
                LogId = dto.LogId,
                Timestamp = dto.Timestamp,
                User = dto.User,
                Action = dto.Action,
                Module = dto.Module,
                EntityId = dto.EntityId,
                Status = dto.Status,
                IPAddress = dto.IPAddress,
                UserAgent = dto.UserAgent,
                Details = dto.Details,
                Metadata = dto.Metadata
            };
        }
    }
}