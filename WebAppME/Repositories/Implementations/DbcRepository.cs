using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Utils;
using DocumentFormat.OpenXml.Vml.Office;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;

namespace BatteryTestingSystem.Repositories.Implementations
{
    public class DbcRepository : Repository<DbcFileRecord>, IDbcRepository
    {
        private readonly AppDbContext _db;
        private readonly IAuditRepository _audit;

        public DbcRepository(AppDbContext context, IAuditRepository audit) : base(context)
        {
            _db = context;
            _audit = audit;
        }

        public async Task<CommonResponse<IEnumerable<DbcFileRecord>>> GetByBatteryIdAsync(long batteryId)
        {
            try
            {
                var data = await _db.dbcFileRecords
                    .Where(d => d.BatteryId == batteryId)
                    .OrderByDescending(d => d.UpdatedAt)
                    .AsNoTracking()
                    .ToListAsync();

                return CommonResponse<IEnumerable<DbcFileRecord>>.Ok(data);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error retrieving DBC files for BatteryId {BatteryId}", batteryId);
                return CommonResponse<IEnumerable<DbcFileRecord>>.Fail("Failed to retrieve DBC files.");
            }
        }

        public async Task<CommonResponse<IEnumerable<DbcFileRecord>>> GetAllAsync()
        {
            try
            {
                var data = await _db.dbcFileRecords
                    .OrderByDescending(d => d.UpdatedAt)
                    .AsNoTracking()
                    .ToListAsync();

                return CommonResponse<IEnumerable<DbcFileRecord>>.Ok(data);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error retrieving all DBC files");
                return CommonResponse<IEnumerable<DbcFileRecord>>.Fail("Failed to retrieve DBC files.");
            }
        }

        public async Task<CommonResponse<DbcFileRecord?>> GetByIdAsync(long id)
        {
            try
            {
                var data = await _db.dbcFileRecords
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (data == null)
                    return CommonResponse<DbcFileRecord?>.Fail("DBC file not found.");

                return CommonResponse<DbcFileRecord?>.Ok(data);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error retrieving DBC file {Id}", id);
                return CommonResponse<DbcFileRecord?>.Fail("Failed to retrieve DBC file.");
            }
        }

        public async Task<CommonResponse<bool>> ExistsAsync(long batteryId, string name, string version, long? excludeId = null)
        {
            try
            {
                var exists = await _db.dbcFileRecords
                    .AnyAsync(d => d.BatteryId == batteryId
                                && d.Name == name
                                && d.Version == version
                                && (excludeId == null || d.Id != excludeId));

                if (exists)
                {
                    return CommonResponse<bool>.Ok(true, $"A DBC named \"{name}\" version \"{version}\" already exists for this battery.");
                }
                else
                {
                    return CommonResponse<bool>.Fail($"not Exists!", false);
                }
            
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error checking DBC existence BatteryId:{BatteryId}", batteryId);
                return CommonResponse<bool>.Fail("Failed to check DBC existence.");
            }
        }

        public async Task<CommonResponse<DbcFileRecord?>> AddAsync(DbcFileRecord entity)
        {
            try
            {
                _db.dbcFileRecords.Add(entity);
                
                await _db.SaveChangesAsync();

                await _audit.LogEventAsync(new AuditLog
                {
                    Action = Models.Enums.AuditActionType.CREATE,
                    Details = $"Dbc {entity.Id} Created For Battery {entity.BatteryId} by {CurrentUser.UserName}",
                    EntityId = $"{entity.BatteryId.ToString()},{entity.Id.ToString()}",
                    Status = Models.Enums.SeverityLevel.INFO,
                    Module = Models.Enums.ModuleName.DBC,
                    Timestamp = DateTime.Now,
                    Metadata = "{}"
                });

                return CommonResponse<DbcFileRecord?>.Ok(entity, "DBC record created.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding DBC record {@Entity}", entity);
                return CommonResponse<DbcFileRecord?>.Fail("Failed to create DBC record.");
            }
        }

        public async Task<CommonResponse<bool>> UpdateAsync(DbcFileRecord entity)
        {
            try
            {
                // 1️⃣ Get existing record (for audit comparison)
                var existing = await _db.dbcFileRecords
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == entity.Id);

                if (existing == null)
                    return CommonResponse<bool>.Fail("DBC record not found.");

                // 2️⃣ Build change log
                var changes = new Dictionary<string, object>();

                void AddChange(string key, object oldVal, object newVal)
                {
                    if (!Equals(oldVal, newVal))
                    {
                        changes[key] = new { Old = oldVal, New = newVal };
                    }
                }

                AddChange("Name", existing.Name, entity.Name);
                AddChange("Version", existing.Version, entity.Version);
                AddChange("Description", existing.Description, entity.Description);
                AddChange("dbcstrJson", existing.dbcstrJson, entity.dbcstrJson);

                // 3️⃣ Execute update
                var affected = await _db.dbcFileRecords
                    .Where(d => d.Id == entity.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(d => d.Name, entity.Name)
                        .SetProperty(d => d.Version, entity.Version)
                        .SetProperty(d => d.Description, entity.Description)
                        .SetProperty(d => d.dbcstrJson, entity.dbcstrJson)
                        .SetProperty(d => d.UpdatedAt, entity.UpdatedAt)
                        .SetProperty(d => d.UpdatedBy, entity.UpdatedBy));

                if (affected == 0)
                    return CommonResponse<bool>.Fail("DBC record not found.");

                // 4️⃣ Audit only if changes exist
                if (changes.Any())
                {
                    await _audit.LogEventAsync(new AuditLog
                    {
                        Action = Models.Enums.AuditActionType.UPDATE,
                        Details = $"Dbc {entity.Id} Updated For Battery {entity.BatteryId} by {CurrentUser.UserName}",
                        EntityId = $"{entity.BatteryId},{entity.Id}",
                        Status = Models.Enums.SeverityLevel.INFO,
                        Module = Models.Enums.ModuleName.DBC,
                        Timestamp = DateTime.Now,
                        Metadata = JsonConvert.SerializeObject(changes)
                    });
                }

                return CommonResponse<bool>.Ok(true, "DBC record updated.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating DBC record {@Entity}", entity);
                return CommonResponse<bool>.Fail("Failed to update DBC record.");
            }
        }

        public async Task<CommonResponse<bool>> DeleteAsync(long id)
        {
            try
            {
                // 1️⃣ Check record exists + join Battery
                var record = await (from d in _db.dbcFileRecords
                                    join b in _db.Batteries
                                    on d.BatteryId equals b.Id
                                    where d.Id == id
                                    select new
                                    {
                                        DbcId = d.Id,
                                        BatteryId = d.BatteryId,
                                        BatteryName = b.Name
                                    })
                                    .FirstOrDefaultAsync();

                if (record == null)
                    return CommonResponse<bool>.Fail("DBC record not found.");

                // 2️⃣ Delete
                var affected = await _db.dbcFileRecords
                    .Where(d => d.Id == id)
                    .ExecuteDeleteAsync();

                if (affected == 0)
                    return CommonResponse<bool>.Fail("DBC record not found.");

                // 3️⃣ Audit with Battery info
                await _audit.LogEventAsync(new AuditLog
                {
                    Action = Models.Enums.AuditActionType.DELETE,
                    Details = $"DBC {record.DbcId} for Battery '{record.BatteryName}' deleted by {CurrentUser.UserName}",
                    EntityId = $"{record.BatteryId},{record.DbcId}",
                    Status = Models.Enums.SeverityLevel.INFO,
                    Module = Models.Enums.ModuleName.DBC,
                    Timestamp = DateTime.Now
                });

                return CommonResponse<bool>.Ok(true, "DBC record deleted.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error deleting DBC record {Id}", id);
                return CommonResponse<bool>.Fail("Failed to delete DBC record.");
            }
        }
    }
}