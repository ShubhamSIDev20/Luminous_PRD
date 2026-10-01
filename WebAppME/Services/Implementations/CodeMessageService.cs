using BatteryTestingSystem.Components.UI.Calibration;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Services.Interfaces;
using BatteryTestingSystem.Utils;

namespace BatteryTestingSystem.Services.Implementations
{
    public class CodeMessageService : ICodeMessageService
    {
        private readonly ICodeMessageRepository _repository;
        public CodeMessageService(ICodeMessageRepository repository)
        {
            _repository = repository;
        }

        public async Task<CommonResponse<List<CodeMessageDto>>> GetAllAsync()
        {
            try
            {
                var entities = await _repository.GetAllAsync();

                if (entities == null || !entities.Any())
                {
                    return CommonResponse<List<CodeMessageDto>>.Ok(
                        new List<CodeMessageDto>(),
                        "No data found"
                    );
                }

                var dtos = entities.Select(MapToDto).ToList();
                return CommonResponse<List<CodeMessageDto>>.Ok(dtos, "Data retrieved successfully");
            }
            catch (Exception ex)
            {
                return CommonResponse<List<CodeMessageDto>>.Fail(
                    $"Failed to retrieve data: {ex.Message}",
                    new List<CodeMessageDto>()
                );
            }
        }

        public async Task<CommonResponse<List<CodeMessageDto>>> GetErrorsAsync()
        {
            try
            {
                var entities = await _repository.GetByTypeAsync((int)CircuitStatus.Error);
                var dtos = new List<CodeMessageDto>();

                // Ensure we have 9 entries with all audit properties
                for (int i = 0; i <= 9; i++)
                {
                    var existing = entities?.FirstOrDefault(e => e.Index == i);
                    if (existing != null)
                    {
                        dtos.Add(MapToDto(existing));
                    }
                    else
                    {
                        dtos.Add(new CodeMessageDto
                        {
                            Index = i,
                            Type = (int)CircuitStatus.Error,
                            Message = string.Empty,
                            CreatedBy = null,
                            UpdatedBy = null,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        });
                    }
                }

                return CommonResponse<List<CodeMessageDto>>.Ok(
                    dtos.OrderBy(d => d.Index).ToList(),
                    "Errors retrieved successfully"
                );
            }
            catch (Exception ex)
            {
                return CommonResponse<List<CodeMessageDto>>.Fail(
                    $"Failed to retrieve errors: {ex.Message}",
                    new List<CodeMessageDto>()
                );
            }
        }

        public async Task<CommonResponse<List<CodeMessageDto>>> GetMessagesAsync()
        {
            try
            {
                var entities = await _repository.GetByTypeAsync((int)CircuitStatus.Msg);
                var dtos = new List<CodeMessageDto>();

                // Ensure we have 9 entries with all audit properties
                for (int i = 0; i <= 9; i++)
                {
                    var existing = entities?.FirstOrDefault(e => e.Index == i);
                    if (existing != null)
                    {
                        dtos.Add(MapToDto(existing));
                    }
                    else
                    {
                        dtos.Add(new CodeMessageDto
                        {
                            Index = i,
                            Type = (int)CircuitStatus.Msg,
                            Message = string.Empty,
                            CreatedBy = null,
                            UpdatedBy = null,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        });
                    }
                }

                return CommonResponse<List<CodeMessageDto>>.Ok(
                    dtos.OrderBy(d => d.Index).ToList(),
                    "Messages retrieved successfully"
                );
            }
            catch (Exception ex)
            {
                return CommonResponse<List<CodeMessageDto>>.Fail(
                    $"Failed to retrieve messages: {ex.Message}",
                    new List<CodeMessageDto>()
                );
            }
        }

        public async Task<CommonResponse<CodeMessageDto>> GetByIdAsync(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return CommonResponse<CodeMessageDto>.Fail("Invalid ID provided");
                }

                var entity = await _repository.GetByIdAsync(id);

                if (entity == null)
                {
                    return CommonResponse<CodeMessageDto>.Fail("Code message not found");
                }

                return CommonResponse<CodeMessageDto>.Ok(
                    MapToDto(entity),
                    "Data retrieved successfully"
                );
            }
            catch (Exception ex)
            {
                return CommonResponse<CodeMessageDto>.Fail($"Failed to retrieve data: {ex.Message}");
            }
        }

        public async Task<CommonResponse<bool>> SaveErrorsAsync(List<CodeMessageDto> errors)
        {
            try
            {
                if (errors == null || !errors.Any())
                {
                    return CommonResponse<bool>.Fail("No errors to save", false);
                }

                var username = CurrentUser.UserName;

                if (string.IsNullOrWhiteSpace(username))
                {
                    return CommonResponse<bool>.Fail("Unable to identify current user", false);
                }

                var now = DateTime.Now;
                var successCount = 0;
                var failCount = 0;

                foreach (var error in errors)
                {
                    if (error.Index < 1 || error.Index > 9)
                    {
                        failCount++;
                        continue;
                    }

                    error.Type = (int)CircuitStatus.Error;

                    var existingEntity = await _repository.GetByIndexAndTypeAsync(error.Index, (int)CircuitStatus.Error);

                    if (existingEntity != null)
                    {
                        // Update existing - preserve CreatedBy and CreatedAt
                        existingEntity.Message = error.Message ?? string.Empty;
                        existingEntity.UpdatedBy = username;
                        existingEntity.UpdatedAt = now;

                        var updatedEntity = await _repository.UpdateAsync(existingEntity);

                        if (updatedEntity != null)
                        {
                            // Update DTO with all properties from saved entity
                            error.Id = updatedEntity.Id;
                            error.CreatedBy = updatedEntity.CreatedBy;
                            error.CreatedAt = updatedEntity.CreatedAt;
                            error.UpdatedBy = updatedEntity.UpdatedBy;
                            error.UpdatedAt = updatedEntity.UpdatedAt;
                            successCount++;
                        }
                        else
                        {
                            failCount++;
                        }
                    }
                    else
                    {
                        // Create new
                        var newEntity = new CodeMessage
                        {
                            Index = error.Index,
                            Type = (int)CircuitStatus.Error,
                            Message = error.Message ?? string.Empty,
                            CreatedBy = username,
                            UpdatedBy = username,
                            CreatedAt = now,
                            UpdatedAt = now,
                            IsDeleted = false
                        };

                        var createdEntity = await _repository.CreateAsync(newEntity);

                        if (createdEntity != null)
                        {
                            // Update DTO with all properties from saved entity
                            error.Id = createdEntity.Id;
                            error.CreatedBy = createdEntity.CreatedBy;
                            error.CreatedAt = createdEntity.CreatedAt;
                            error.UpdatedBy = createdEntity.UpdatedBy;
                            error.UpdatedAt = createdEntity.UpdatedAt;
                            successCount++;
                        }
                        else
                        {
                            failCount++;
                        }
                    }
                }

                if (failCount > 0)
                {
                    return CommonResponse<bool>.Fail(
                        $"Partially saved: {successCount} successful, {failCount} failed",
                        false
                    );
                }

                return CommonResponse<bool>.Ok(true, $"All {successCount} errors saved successfully");
            }
            catch (Exception ex)
            {
                return CommonResponse<bool>.Fail($"Failed to save errors: {ex.Message}", false);
            }
        }

        public async Task<CommonResponse<bool>> SaveMessagesAsync(List<CodeMessageDto> messages)
        {
            try
            {
                if (messages == null || !messages.Any())
                {
                    return CommonResponse<bool>.Fail("No messages to save", false);
                }

                var username = CurrentUser.UserName;

                if (string.IsNullOrWhiteSpace(username))
                {
                    return CommonResponse<bool>.Fail("Unable to identify current user", false);
                }

                var now = DateTime.Now;
                var successCount = 0;
                var failCount = 0;

                foreach (var message in messages)
                {
                    if (message.Index < 1 || message.Index > 9)
                    {
                        failCount++;
                        continue;
                    }
                    message.Type = (int)CircuitStatus.Msg;

                    var existingEntity = await _repository.GetByIndexAndTypeAsync(message.Index, (int)CircuitStatus.Msg);

                    if (existingEntity != null)
                    {
                        // Update existing - preserve CreatedBy and CreatedAt
                        existingEntity.Message = message.Message ?? string.Empty;
                        existingEntity.UpdatedBy = username;
                        existingEntity.UpdatedAt = now;

                        var updatedEntity = await _repository.UpdateAsync(existingEntity);

                        if (updatedEntity != null)
                        {
                            // Update DTO with all properties from saved entity
                            message.Id = updatedEntity.Id;
                            message.CreatedBy = updatedEntity.CreatedBy;
                            message.CreatedAt = updatedEntity.CreatedAt;
                            message.UpdatedBy = updatedEntity.UpdatedBy;
                            message.UpdatedAt = updatedEntity.UpdatedAt;
                            successCount++;
                        }
                        else
                        {
                            failCount++;
                        }
                    }
                    else
                    {
                        // Create new
                        var newEntity = new CodeMessage
                        {
                            Index = message.Index,
                            Type = (int)CircuitStatus.Msg,
                            Message = message.Message ?? string.Empty,
                            CreatedBy = username,
                            UpdatedBy = username,
                            CreatedAt = now,
                            UpdatedAt = now,
                            IsDeleted = false
                        };

                        var createdEntity = await _repository.CreateAsync(newEntity);

                        if (createdEntity != null)
                        {
                            // Update DTO with all properties from saved entity
                            message.Id = createdEntity.Id;
                            message.CreatedBy = createdEntity.CreatedBy;
                            message.CreatedAt = createdEntity.CreatedAt;
                            message.UpdatedBy = createdEntity.UpdatedBy;
                            message.UpdatedAt = createdEntity.UpdatedAt;
                            successCount++;
                        }
                        else
                        {
                            failCount++;
                        }
                    }
                }

                if (failCount > 0)
                {
                    return CommonResponse<bool>.Fail(
                        $"Partially saved: {successCount} successful, {failCount} failed",
                        false
                    );
                }

                return CommonResponse<bool>.Ok(true, $"All {successCount} messages saved successfully");
            }
            catch (Exception ex)
            {
                return CommonResponse<bool>.Fail($"Failed to save messages: {ex.Message}", false);
            }
        }

        public async Task<CommonResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return CommonResponse<bool>.Fail("Invalid ID provided", false);
                }

                var result = await _repository.DeleteAsync(id);

                if (!result)
                {
                    return CommonResponse<bool>.Fail("Code message not found or already deleted", false);
                }

                return CommonResponse<bool>.Ok(true, "Code message deleted successfully");
            }
            catch (Exception ex)
            {
                return CommonResponse<bool>.Fail($"Failed to delete data: {ex.Message}", false);
            }
        }

        private CodeMessageDto MapToDto(CodeMessage entity)
        {
            return new CodeMessageDto
            {
                Id = entity.Id,
                Index = entity.Index,
                Type = entity.Type,
                Message = entity.Message ?? string.Empty,
                CreatedBy = entity.CreatedBy,
                UpdatedBy = entity.UpdatedBy,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}
