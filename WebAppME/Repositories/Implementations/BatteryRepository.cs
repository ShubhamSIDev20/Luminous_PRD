using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Utils;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using UAParser;

namespace BatteryTestingSystem.Repositories.Implementations
{
    public class BatteryRepository : Repository<Batteries>, IBatteryRepository
    {
        private readonly AppDbContext _AppDbcontext;
        private readonly IAuditRepository _audit;
        public BatteryRepository(AppDbContext context, IAuditRepository audit) : base(context)
        {
            _AppDbcontext = context;
            _audit = audit;
        }

        // ============================
        // Get All Batteries
        // ============================
        public async Task<CommonResponse<List<BatteryDTO>>> GetBatteries()
        {
            try
            {
                var batteries = await _dbSet
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .Select(x => new BatteryDTO
                    {
                        Id = x.Id,
                        Name = x.Name,
                        BatteryTypeId = x.BatteryTypeId,
                        Comments = x.Comments,
                        Quantity = x.Quantity,
                        Producer = x.Producer,
                        NominalVoltage = x.NominalVoltage,
                        NominalCurrent = x.NominalCurrent,
                        NominalCapacity = x.NominalCapacity,
                        ChargeFactor = x.ChargeFactor,
                        Impedance = x.Impedance,
                        EnergyDensity = x.EnergyDensity,
                        ColdCrankingCurrent = x.ColdCrankingCurrent,
                        NumberOfCells = x.NumberOfCells,
                        MaximumVoltage = x.MaximumVoltage,
                        GassingVoltage = x.GassingVoltage,
                        BreakVoltage = x.BreakVoltage,
                        Port1DbcId = x.Port1DbcId,
                        Port2DbcId = x.Port2DbcId,
                        Port3DbcId = x.Port3DbcId,
                        CreatedAt = x.CreatedAt,
                        UpdatedAt = x.UpdatedAt,
                        CreatedBy = x.CreatedBy,
                        UpdatedBy = x.UpdatedBy
                    })
                    .ToListAsync();

                return CommonResponse<List<BatteryDTO>>.Ok(batteries);
            }
            catch (Exception ex)
            {
                return CommonResponse<List<BatteryDTO>>
                    .Fail($"Failed to fetch batteries: {ex.Message}");
            }
        }

        // ============================
        // Get Battery by Id
        // ============================
        public async Task<CommonResponse<BatteryDTO>> GetBattery(long batteryId)
        {
            try
            {
                var battery = await _dbSet
                    .AsNoTracking()
                    .Where(x => x.Id == batteryId && !x.IsDeleted)
                    .Select(x => new BatteryDTO
                    {
                        Id = x.Id,
                        Name = x.Name,
                        BatteryTypeId = x.BatteryTypeId,
                        Comments = x.Comments,
                        Quantity = x.Quantity,
                        Producer = x.Producer,
                        NominalVoltage = x.NominalVoltage,
                        NominalCurrent = x.NominalCurrent,
                        NominalCapacity = x.NominalCapacity,
                        ChargeFactor = x.ChargeFactor,
                        Impedance = x.Impedance,
                        EnergyDensity = x.EnergyDensity,
                        ColdCrankingCurrent = x.ColdCrankingCurrent,
                        NumberOfCells = x.NumberOfCells,
                        MaximumVoltage = x.MaximumVoltage,
                        GassingVoltage = x.GassingVoltage,
                        BreakVoltage = x.BreakVoltage,
                        Port1DbcId = x.Port1DbcId,
                        Port2DbcId = x.Port2DbcId,
                        Port3DbcId = x.Port3DbcId,
                        CreatedAt = x.CreatedAt,
                        UpdatedAt = x.UpdatedAt,
                        CreatedBy = x.CreatedBy,
                        UpdatedBy = x.UpdatedBy
                    })
                    .FirstOrDefaultAsync();

                if (battery == null)
                    return CommonResponse<BatteryDTO>.Fail("Battery not found");

                return CommonResponse<BatteryDTO>.Ok(battery);
            }
            catch (Exception ex)
            {
                return CommonResponse<BatteryDTO>
                    .Fail($"Failed to fetch battery: {ex.Message}");
            }
        }

        // ============================
        // Soft Delete Battery
        // ============================
        public async Task<CommonResponse<bool>> BatteryDelete(long batteryId)
        {
            try
            {
                var battery = await _dbSet.FirstOrDefaultAsync(x => x.Id == batteryId && !x.IsDeleted);

                if (battery == null)
                    return CommonResponse<bool>.Fail("Battery not found");

                battery.IsDeleted = true;
                battery.UpdatedAt = DateTime.Now;

                await _AppDbcontext.SaveChangesAsync();

                await _audit.LogEventAsync(new AuditLog
                {
                    Action = Models.Enums.AuditActionType.DELETE,
                    Details = $"Battery {batteryId} is Deleted! by {CurrentUser.UserName}",
                    EntityId = batteryId.ToString(),
                    Status = Models.Enums.SeverityLevel.WARNING,
                    Module = Models.Enums.ModuleName.BATTERY,
                    Timestamp = DateTime.Now,
                    Metadata = "{}"
                });

                return CommonResponse<bool>.Ok(true, "Battery deleted successfully");
            }
            catch (Exception ex)
            {
                return CommonResponse<bool>
                    .Fail($"Failed to delete battery: {ex.Message}");
            }
        }

        // ============================
        // Update Port Assignments
        // ============================
        public async Task<CommonResponse<bool>> UpdatePortAssignmentsAsync(
            long batteryId, long? port1DbcId, long? port2DbcId, long? port3DbcId)
        {
            try
            {
                var entity = await _dbSet.FirstOrDefaultAsync(x => x.Id == batteryId && !x.IsDeleted);
                if (entity == null)
                    return CommonResponse<bool>.Fail("Battery not found");

                entity.Port1DbcId = port1DbcId;
                entity.Port2DbcId = port2DbcId;
                entity.Port3DbcId = port3DbcId;
                entity.UpdatedAt = DateTime.Now;
                entity.UpdatedBy = CurrentUser.UserName;

                await _AppDbcontext.SaveChangesAsync();
                return CommonResponse<bool>.Ok(true, "Port assignments updated");
            }
            catch (Exception ex)
            {
                return CommonResponse<bool>.Fail($"Failed to update port assignments: {ex.Message}");
            }
        }

        // ============================
        // Create or Update Battery
        // ============================
        public async Task<CommonResponse<BatteryDTO>> CreateOrUpdateBattery(BatteryDTO battery)
        {
            try
            {
                // =========================
                // UPDATE
                // =========================
                if (battery.Id > 0)
                {
                    var entity = await _dbSet
                        .FirstOrDefaultAsync(x => x.Id == battery.Id && !x.IsDeleted);

                    if (entity == null)
                        return CommonResponse<BatteryDTO>.Fail("Battery not found");

                    // Check duplicate ONLY if name is changed
                    if (!string.IsNullOrWhiteSpace(battery.Name) &&
                        !string.Equals(entity.Name, battery.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        bool nameExists = await _dbSet.AnyAsync(x =>
                            !x.IsDeleted &&
                            x.Name.ToLower() == battery.Name.Trim().ToLower() &&
                            x.Id != battery.Id
                        );

                        if (nameExists)
                            return CommonResponse<BatteryDTO>.Fail("Battery name already exists");

                        entity.Name = battery.Name.Trim();
                    }

                    // Partial update
                    if (battery.BatteryTypeId > 0)
                        entity.BatteryTypeId = battery.BatteryTypeId;

                    if (battery.Comments != null)
                        entity.Comments = battery.Comments;

                    if (battery.Quantity > 0)
                        entity.Quantity = battery.Quantity;

                    if (!string.IsNullOrWhiteSpace(battery.Producer))
                        entity.Producer = battery.Producer;

                    if (battery.NominalVoltage > 0)
                        entity.NominalVoltage = battery.NominalVoltage;

                    if (battery.NominalCurrent > 0)
                        entity.NominalCurrent = battery.NominalCurrent;

                    if (battery.NominalCapacity > 0)
                        entity.NominalCapacity = battery.NominalCapacity;

                    if (battery.ChargeFactor > 0)
                        entity.ChargeFactor = battery.ChargeFactor;

                    if (battery.Impedance > 0)
                        entity.Impedance = battery.Impedance;

                    if (battery.EnergyDensity > 0)
                        entity.EnergyDensity = battery.EnergyDensity;

                    if (battery.ColdCrankingCurrent > 0)
                        entity.ColdCrankingCurrent = battery.ColdCrankingCurrent;

                    if (battery.NumberOfCells > 0)
                        entity.NumberOfCells = battery.NumberOfCells;

                    if (battery.MaximumVoltage > 0)
                        entity.MaximumVoltage = battery.MaximumVoltage;

                    if (battery.GassingVoltage > 0)
                        entity.GassingVoltage = battery.GassingVoltage;

                    if (battery.BreakVoltage > 0)
                        entity.BreakVoltage = battery.BreakVoltage;

                    // Always write port assignments — null is valid (no DBC assigned)
                    entity.Port1DbcId = battery.Port1DbcId;
                    entity.Port2DbcId = battery.Port2DbcId;
                    entity.Port3DbcId = battery.Port3DbcId;

                    entity.UpdatedAt = DateTime.Now;
                    entity.UpdatedBy = CurrentUser.UserName;

                    await _AppDbcontext.SaveChangesAsync();

                    var changes = GetChanges(entity, battery);

                    if (changes.Any())
                    {
                        await _audit.LogEventAsync(new AuditLog
                        {
                            Action = Models.Enums.AuditActionType.UPDATE,
                            Details = $"Battery {battery.Id} updated by {CurrentUser.UserName}",
                            EntityId = battery.Id.ToString(),
                            Status = Models.Enums.SeverityLevel.INFO,
                            Module = Models.Enums.ModuleName.BATTERY,
                            Timestamp = DateTime.Now,
                            Metadata = JsonConvert.SerializeObject(changes)
                        });
                    }

                    return CommonResponse<BatteryDTO>.Ok(battery, "Battery updated successfully");
                }

                // =========================
                // CREATE
                // =========================
                else
                {
                    bool nameExists = await _dbSet.AnyAsync(x =>
                        !x.IsDeleted &&
                        x.Name.ToLower() == battery.Name.Trim().ToLower()
                    );

                    if (nameExists)
                        return CommonResponse<BatteryDTO>.Fail("Battery name already exists");

                    var entity = new Batteries
                    {
                        Name = battery.Name.Trim(),
                        BatteryTypeId = battery.BatteryTypeId,
                        Comments = battery.Comments,
                        Quantity = battery.Quantity,
                        Producer = battery.Producer,
                        NominalVoltage = battery.NominalVoltage,
                        NominalCurrent = battery.NominalCurrent,
                        NominalCapacity = battery.NominalCapacity,
                        ChargeFactor = battery.ChargeFactor,
                        Impedance = battery.Impedance,
                        EnergyDensity = battery.EnergyDensity,
                        ColdCrankingCurrent = battery.ColdCrankingCurrent,
                        NumberOfCells = battery.NumberOfCells,
                        MaximumVoltage = battery.MaximumVoltage,
                        GassingVoltage = battery.GassingVoltage,
                        BreakVoltage = battery.BreakVoltage,
                        Port1DbcId = battery.Port1DbcId,
                        Port2DbcId = battery.Port2DbcId,
                        Port3DbcId = battery.Port3DbcId,
                        CreatedAt = DateTime.Now,
                        CreatedBy = CurrentUser.UserName,
                        UpdatedAt = DateTime.Now,
                        UpdatedBy = CurrentUser.UserName
                    };

                    await _dbSet.AddAsync(entity);
                    await _AppDbcontext.SaveChangesAsync();

                    battery.Id = entity.Id;

                    await _audit.LogEventAsync(new AuditLog
                    {
                        Action = Models.Enums.AuditActionType.CREATE,
                        Details = $"Battery {battery.Id} CREATE by {CurrentUser.UserName}",
                        EntityId = battery.Id.ToString(),
                        Status = Models.Enums.SeverityLevel.INFO,
                        Module = Models.Enums.ModuleName.BATTERY,
                        Timestamp = DateTime.Now,
                        Metadata = "{}"
                    });

                    return CommonResponse<BatteryDTO>.Ok(battery, "Battery created successfully");
                }
            }
            catch (Exception ex)
            {
                return CommonResponse<BatteryDTO>
                    .Fail($"Failed to save battery: {ex.Message}");
            }
        }

        private Dictionary<string, object> GetChanges(Batteries entity, BatteryDTO dto)
        {
            var changes = new Dictionary<string, object>();

            void AddChange(string key, object oldVal, object newVal)
            {
                if (!Equals(oldVal, newVal))
                {
                    changes[key] = new
                    {
                        Old = oldVal,
                        New = newVal
                    };
                }
            }

            AddChange("Name", entity.Name, dto.Name);
            AddChange("BatteryTypeId", entity.BatteryTypeId, dto.BatteryTypeId);
            AddChange("Comments", entity.Comments, dto.Comments);
            AddChange("Quantity", entity.Quantity, dto.Quantity);
            AddChange("Producer", entity.Producer, dto.Producer);
            AddChange("NominalVoltage", entity.NominalVoltage, dto.NominalVoltage);
            AddChange("NominalCurrent", entity.NominalCurrent, dto.NominalCurrent);
            AddChange("NominalCapacity", entity.NominalCapacity, dto.NominalCapacity);
            AddChange("ChargeFactor", entity.ChargeFactor, dto.ChargeFactor);
            AddChange("Impedance", entity.Impedance, dto.Impedance);
            AddChange("EnergyDensity", entity.EnergyDensity, dto.EnergyDensity);
            AddChange("ColdCrankingCurrent", entity.ColdCrankingCurrent, dto.ColdCrankingCurrent);
            AddChange("NumberOfCells", entity.NumberOfCells, dto.NumberOfCells);
            AddChange("MaximumVoltage", entity.MaximumVoltage, dto.MaximumVoltage);
            AddChange("GassingVoltage", entity.GassingVoltage, dto.GassingVoltage);
            AddChange("BreakVoltage", entity.BreakVoltage, dto.BreakVoltage);

            return changes;
        }
    }
}
