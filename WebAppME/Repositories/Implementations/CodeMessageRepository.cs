using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Utils;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Vml.Office;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace BatteryTestingSystem.Repositories.Implementations
{
    public class CodeMessageRepository : Repository<CodeMessage>,  ICodeMessageRepository
    {
        private readonly AppDbContext _AppDbcontext;
        private readonly IAuditRepository _audit;

        public CodeMessageRepository(AppDbContext context, IAuditRepository audit) : base(context)
        {
            _AppDbcontext = context;
            _audit = audit;
        }
        public async Task<List<CodeMessage>> GetAllAsync()
        {
            try
            {
                return await _AppDbcontext.CodeMessages
                    .Where(cm => !cm.IsDeleted)
                    .OrderBy(cm => cm.Type)
                    .ThenBy(cm => cm.Index)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch
            {
                return new List<CodeMessage>();
            }
        }

        public async Task<List<CodeMessage>> GetByTypeAsync(int type)
        {
            try
            {
                if (type <= 0)
                {
                    return new List<CodeMessage>();
                }

                return await _AppDbcontext.CodeMessages
                    .Where(cm => cm.Type == type && !cm.IsDeleted)
                    .OrderBy(cm => cm.Index)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch
            {
                return new List<CodeMessage>();
            }
        }

        public async Task<CodeMessage?> GetByIdAsync(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return null;
                }

                return await _AppDbcontext.CodeMessages
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cm => cm.Id == id && !cm.IsDeleted);
            }
            catch
            {
                return null;
            }
        }

        public async Task<CodeMessage?> GetByIndexAndTypeAsync(int index, int type)
        {
            try
            {
                if (index <= 0 || type <= 0)
                {
                    return null;
                }

                return await _AppDbcontext.CodeMessages
                    .FirstOrDefaultAsync(cm => cm.Index == index && cm.Type == type && !cm.IsDeleted);
            }
            catch
            {
                return null;
            }
        }

        public async Task<CodeMessage> CreateAsync(CodeMessage codeMessage)
        {
            try
            {
                if (codeMessage == null)
                {
                    return null;
                }

                codeMessage.CreatedAt = DateTime.Now;
                codeMessage.CreatedBy= CurrentUser.UserName;
                codeMessage.UpdatedAt = DateTime.Now;
                codeMessage.UpdatedBy = CurrentUser.UserName;
                codeMessage.IsDeleted = false;

                _AppDbcontext.CodeMessages.Add(codeMessage);
                await _AppDbcontext.SaveChangesAsync();

                // Detach and reload to get the exact database state
                _AppDbcontext.Entry(codeMessage).State = EntityState.Detached;
                var savedEntity = await _AppDbcontext.CodeMessages
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cm => cm.Id == codeMessage.Id);


                await _audit.LogEventAsync(new AuditLog
                {
                    Action = Models.Enums.AuditActionType.DELETE,
                    Details = $"{(codeMessage.Type == (int)CircuitStatus.Error ? "Error" : "Message")} Deleted by {CurrentUser.UserName}",
                    EntityId = codeMessage.Id.ToString(),
                    Status = Models.Enums.SeverityLevel.WARNING,
                    Module = Models.Enums.ModuleName.SYSTEM,
                    Timestamp = DateTime.Now
                });

                return savedEntity;
            }
            catch
            {
                return null;
            }
        }

        public async Task<CodeMessage> UpdateAsync(CodeMessage codeMessage)
        {
            try
            {
                if (codeMessage == null || codeMessage.Id <= 0)
                {
                    return null;
                }

                var existingEntity = await _AppDbcontext.CodeMessages
                    .FirstOrDefaultAsync(cm => cm.Id == codeMessage.Id && !cm.IsDeleted);

                if (existingEntity == null)
                {
                    return null;
                }

                // ✅ Store old Message only
                var oldMessage = existingEntity.Message;

                // Update metadata
                codeMessage.UpdatedAt = DateTime.Now;
                codeMessage.UpdatedBy = CurrentUser.UserName;

                // Apply updates
                _AppDbcontext.Entry(existingEntity).CurrentValues.SetValues(codeMessage);

                // Save changes
                await _AppDbcontext.SaveChangesAsync();

                // Detach entity
                _AppDbcontext.Entry(existingEntity).State = EntityState.Detached;

                // Reload updated entity (optional but kept for consistency)
                var updatedEntity = await _AppDbcontext.CodeMessages
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cm => cm.Id == codeMessage.Id);

                // ✅ Audit ONLY if Message changed
                if (oldMessage != updatedEntity?.Message)
                {
                    await _audit.LogEventAsync(new AuditLog
                    {
                        Action = Models.Enums.AuditActionType.UPDATE,
                        Details = $"{(updatedEntity.Type == (int)CircuitStatus.Error ? "Error" : "Message")} {updatedEntity.Id} updated by {CurrentUser.UserName}",
                        Status = Models.Enums.SeverityLevel.INFO,
                        Module = Models.Enums.ModuleName.SYSTEM,
                        Timestamp = DateTime.Now,
                        Metadata = JsonConvert.SerializeObject(new
                        {
                            OldMessage = oldMessage,
                            NewMessage = updatedEntity?.Message
                        })
                    });
                }

                return updatedEntity;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return false;
                }

                var entity = await _AppDbcontext.CodeMessages
                    .FirstOrDefaultAsync(cm => cm.Id == id && !cm.IsDeleted);

                if (entity == null)
                {
                    return false;
                }

                entity.IsDeleted = true;
                entity.UpdatedAt = DateTime.Now;

                await _AppDbcontext.SaveChangesAsync();

                await _audit.LogEventAsync(new AuditLog
                {
                    Action = Models.Enums.AuditActionType.DELETE,
                    Details = $"{(entity.Type == (int)CircuitStatus.Error ? "Error" : "Message")} {id} Deleted by {CurrentUser.UserName}",
                    EntityId = id.ToString(),
                    Status = Models.Enums.SeverityLevel.WARNING,
                    Module = Models.Enums.ModuleName.SYSTEM,
                    Timestamp = DateTime.Now,
                    Metadata = JsonConvert.SerializeObject(entity)
                });

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> SaveChangesAsync()
        {

            try
            {
                return await _AppDbcontext.SaveChangesAsync() > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
