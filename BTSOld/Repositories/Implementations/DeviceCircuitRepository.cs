using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Utils;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace BatteryTestingSystem.Repositories.Implementations
{
    public class DeviceCircuitRepository : Repository<Device>, IDeviceCircuitRepository
    {
        private readonly IAuditRepository _audit;

        public DeviceCircuitRepository(AppDbContext context, IAuditRepository audit) : base(context)
        {
            _audit = audit;
        }

        public async Task<CommonResponse<CircuitDto>> GetCircuitAsync(CircuitDto circuit)
        {
            try
            {
                var result = await (
                        from device in _context.Devices
                        join cir in _context.Circuits
                            on device.DeviceID equals cir.DeviceId
                        where device.DeviceID == circuit.DeviceID &&
                              cir.CircuitID == circuit.CircuitID
                        select new CircuitDto
                        {
                            DeviceID = device.DeviceID,
                            CircuitID = cir.CircuitID,
                            DeviceName = device.DeviceName,
                            MACID = device.MACID,
                            IPAddress = device.IPAddress,
                            IsRegistered = cir.IsRegistered,
                            IsDeleted = cir.IsDeleted,
                            DHCPEnable = device.DHCPEnable,
                            MasterSwVersion = device.SwVersion,
                            ComSwVersion = device.ComSwVersion,
                            ManufactureDateTime = device.ManufactureDateTime,
                            CommissioningDateTime = device.CommissioningDateTime,
                            AssemblyDate = device.AssemblyDate,

                            CircuitType = cir.CircuitType,
                            SecondarySwVersion = cir.SwVersion,
                            SecondaryAssemblyDate = cir.AssemblyDate,
                            ZntMaxVoltage = cir.ZntMaxVoltage,
                            LntMaxVoltage = cir.LntMaxVoltage,
                            CircuitMaxVoltage = cir.CircuitMaxVoltage,
                            CircuitMinVoltage = cir.CircuitMinVoltage,
                            CircuitMaxDischargingCurrent = cir.CircuitMaxDischargingCurrent,
                            CircuitMaxChargingCurrent = cir.CircuitMaxChargingCurrent,
                            CreatedAt = cir.CreatedAt,
                            CreatedBy = cir.CreatedBy,
                            UpdatedAt = cir.UpdatedAt,
                            UpdatedBy = cir.UpdatedBy
                        }
                    ).FirstOrDefaultAsync();

                if (result == null)
                    return CommonResponse<CircuitDto>.Fail("Circuit not found.");

                return CommonResponse<CircuitDto>.Ok(result, "Circuit retrieved successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetCircuitAsync Error: {ex.Message}");
                return CommonResponse<CircuitDto>.Fail("Error retrieving circuit. Please try again.");
            }
        }

        public async Task<CommonResponse<List<CircuitDto>>> GetCircuitsAsync()
        {
            try
            {
                var result = await (
                    from device in _context.Devices
                    join circuit in _context.Circuits
                        on device.DeviceID equals circuit.DeviceId
                    select new CircuitDto
                    {
                        DeviceID = device.DeviceID,
                        CircuitID = circuit.CircuitID,
                        DeviceName = device.DeviceName,
                        MACID = device.MACID,
                        IPAddress = device.IPAddress,
                        IsRegistered = circuit.IsRegistered,
                        IsDeleted = circuit.IsDeleted,
                        DHCPEnable = device.DHCPEnable,
                        MasterSwVersion = device.SwVersion,
                        ComSwVersion = device.ComSwVersion,
                        ManufactureDateTime = device.ManufactureDateTime,
                        CommissioningDateTime = device.CommissioningDateTime,
                        AssemblyDate = device.AssemblyDate,
                        CircuitType = circuit.CircuitType,
                        SecondarySwVersion = circuit.SwVersion,
                        SecondaryAssemblyDate = circuit.AssemblyDate,
                        PrimarySerialNumber = device.PrimarySerialNumber,
                        SecondarySerialNumber = circuit.SecondarySerialNumber ?? string.Empty,
                        ZntMaxVoltage = circuit.ZntMaxVoltage,
                        LntMaxVoltage = circuit.LntMaxVoltage,
                        CircuitMaxVoltage = circuit.CircuitMaxVoltage,
                        CircuitMinVoltage = circuit.CircuitMinVoltage,
                        CircuitMaxDischargingCurrent = circuit.CircuitMaxDischargingCurrent,
                        CircuitMaxChargingCurrent = circuit.CircuitMaxChargingCurrent,
                        CreatedAt = circuit.CreatedAt,
                        CreatedBy = circuit.CreatedBy,
                        UpdatedAt = circuit.UpdatedAt,
                        UpdatedBy = circuit.UpdatedBy
                    }
                ).ToListAsync();

                return CommonResponse<List<CircuitDto>>.Ok(result, "Circuits retrieved successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetCircuitsAsync Error: {ex.Message}");

                return CommonResponse<List<CircuitDto>>.Fail("Error retrieving circuits. Please try again.");
            }
        }

        public async Task<CommonResponse<CircuitDto>> InsertAsync(CircuitDto dto)
        {
            try
            {
                if (dto == null)
                    return CommonResponse<CircuitDto>.Fail("Invalid request.");

                // ---------------------------
                // 1. DEVICE CHECK
                // ---------------------------
                var device = await _context.Devices
                    .FirstOrDefaultAsync(d => d.DeviceID == dto.DeviceID);

                // Device does not exist → INSERT new device
                if (device == null)
                {
                    device = new Device
                    {
                        DeviceID = dto.DeviceID,
                        DeviceName = dto.DeviceName,
                        MACID = dto.MACID,
                        IPAddress = dto.IPAddress,
                        DHCPEnable = dto.DHCPEnable,
                        SwVersion = dto.MasterSwVersion,
                        ComSwVersion = dto.ComSwVersion,
                        ManufactureDateTime = dto.ManufactureDateTime,
                        CommissioningDateTime = dto.CommissioningDateTime,
                        AssemblyDate = dto.AssemblyDate,
                        PrimarySerialNumber = dto.PrimarySerialNumber
                    };

                    await _context.Devices.AddAsync(device);
                }

                // ---------------------------
                // 2. CIRCUIT CHECK
                // ---------------------------
                var circuit = await _context.Circuits
                    .FirstOrDefaultAsync(c => c.DeviceId == dto.DeviceID && c.CircuitID == dto.CircuitID);

                // Circuit exists → return already exists (DO NOT MODIFY)
                if (circuit != null)
                {
                    circuit.IsDeleted = false;                  
                    dto.IsRegistered = circuit.IsRegistered;

                    await _context.SaveChangesAsync();

                    return CommonResponse<CircuitDto>.Ok(
                        dto,
                        "Circuit already exists."
                    );
                }

                // Circuit does not exist → INSERT new circuit
                circuit = new Circuit
                {
                    CircuitID = dto.CircuitID,
                    DeviceId = dto.DeviceID,
                    IsRegistered = dto.IsRegistered,
                    IsDeleted = false,
                    CircuitType = dto.CircuitType,
                    SwVersion = dto.SecondarySwVersion,
                    AssemblyDate = dto.SecondaryAssemblyDate,
                    ZntMaxVoltage = dto.ZntMaxVoltage,
                    LntMaxVoltage = dto.LntMaxVoltage,
                    CircuitMaxVoltage = dto.CircuitMaxVoltage,
                    CircuitMinVoltage = dto.CircuitMinVoltage,
                    CircuitMaxDischargingCurrent = dto.CircuitMaxDischargingCurrent,
                    CircuitMaxChargingCurrent = dto.CircuitMaxChargingCurrent,
                    SecondarySerialNumber = dto.SecondarySerialNumber,
                    CreatedAt = dto.CreatedAt ?? DateTime.Now,
                    CreatedBy = dto.CreatedBy,
                    UpdatedAt = dto.UpdatedAt ?? DateTime.Now,
                    UpdatedBy = dto.UpdatedBy
                };

                await _context.Circuits.AddAsync(circuit);

                // SAVE everything together
                await _context.SaveChangesAsync();

                dto.IsDeleted = circuit.IsDeleted;
                dto.IsRegistered = circuit.IsRegistered;


                await _audit.LogEventAsync(new AuditLog
                {
                    Action = Models.Enums.AuditActionType.CREATE,
                    Details = $"Circuit {dto.DeviceID}-{dto.CircuitID} Registered",
                    Status = Models.Enums.SeverityLevel.INFO,
                    Module = Models.Enums.ModuleName.CIRCUIT,
                    Timestamp = DateTime.Now
                });

                return CommonResponse<CircuitDto>.Ok(dto, "Circuit inserted successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InsertAsync Error: {ex.Message}");
                return CommonResponse<CircuitDto>.Fail("Insert failed. Please try again.");
            }
        }
        public async Task<CommonResponse<CircuitDto>> UpdateRegistration(CircuitDto circuit)
        {
            if (circuit == null)
                return CommonResponse<CircuitDto>.Fail("Invalid request.");

            try
            {
                // ---------------------------
                // 1. DEVICE CHECK
                // ---------------------------
                var device = await _context.Devices
                    .FirstOrDefaultAsync(d => d.DeviceID == circuit.DeviceID);

                if (device == null)
                    return CommonResponse<CircuitDto>.Fail($"Device with ID '{circuit.DeviceID}' not found.");

                device.PrimarySerialNumber = circuit.PrimarySerialNumber;
                device.IPAddress = circuit.IPAddress;
                device.MACID = circuit.MACID;
                device.SwVersion = circuit.MasterSwVersion;
                device.ComSwVersion = circuit.ComSwVersion;
                device.DHCPEnable = circuit.DHCPEnable;
                device.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return CommonResponse<CircuitDto>.Ok(circuit);
            }
            catch (DbUpdateException ex)
            {
                // Log ex here if you have a logger injected
                return CommonResponse<CircuitDto>.Fail($"Database error while updating device: {ex.Message}");
            }
            catch (Exception ex)
            {
                return CommonResponse<CircuitDto>.Fail($"Unexpected error: {ex.Message}");
            }
        }
        public async Task<CommonResponse<CircuitDto>> UpdateAsync(CircuitDto circuit)
        {
            try
            {
                if (circuit == null)
                    return CommonResponse<CircuitDto>.Fail("Invalid circuit data.");

                var device = await _context.Devices
                    .FirstOrDefaultAsync(x => x.DeviceID == circuit.DeviceID);

                if (device == null)
                    return CommonResponse<CircuitDto>.Fail("Device not found.");

                var existingCircuit = await _context.Circuits
                    .FirstOrDefaultAsync(x => x.CircuitID == circuit.CircuitID && x.DeviceId == device.DeviceID);

                if (existingCircuit == null)
                    return CommonResponse<CircuitDto>.Fail("Circuit not found.");

                // STORE ORIGINAL VALUES
                var changes = new Dictionary<string, object>();

                void TrackChange(string field, object oldVal, object newVal)
                {
                    if (!Equals(oldVal, newVal))
                    {
                        changes[field] = new { Old = oldVal, New = newVal };
                    }
                }

                // TRACK DEVICE CHANGES
                TrackChange("DeviceName", device.DeviceName, circuit.DeviceName);
                TrackChange("MACID", device.MACID, circuit.MACID);
                TrackChange("IPAddress", device.IPAddress, circuit.IPAddress);
                TrackChange("DHCPEnable", device.DHCPEnable, circuit.DHCPEnable);
                TrackChange("MasterSwVersion", device.SwVersion, circuit.MasterSwVersion);
                TrackChange("ComSwVersion", device.ComSwVersion, circuit.ComSwVersion);
                TrackChange("ManufactureDateTime", device.ManufactureDateTime, circuit.ManufactureDateTime);
                TrackChange("CommissioningDateTime", device.CommissioningDateTime, circuit.CommissioningDateTime);
                TrackChange("AssemblyDate", device.AssemblyDate, circuit.AssemblyDate);

                // TRACK CIRCUIT CHANGES
                TrackChange("CircuitType", existingCircuit.CircuitType, circuit.CircuitType);
                TrackChange("IsRegistered", existingCircuit.IsRegistered, circuit.IsRegistered);
                TrackChange("SecondarySwVersion", existingCircuit.SwVersion, circuit.SecondarySwVersion);
                TrackChange("SecondaryAssemblyDate", existingCircuit.AssemblyDate, circuit.SecondaryAssemblyDate);
                TrackChange("ZntMaxVoltage", existingCircuit.ZntMaxVoltage, circuit.ZntMaxVoltage);
                TrackChange("LntMaxVoltage", existingCircuit.LntMaxVoltage, circuit.LntMaxVoltage);
                TrackChange("CircuitMaxVoltage", existingCircuit.CircuitMaxVoltage, circuit.CircuitMaxVoltage);
                TrackChange("CircuitMinVoltage", existingCircuit.CircuitMinVoltage, circuit.CircuitMinVoltage);
                TrackChange("CircuitMaxChargingCurrent", existingCircuit.CircuitMaxChargingCurrent, circuit.CircuitMaxChargingCurrent);
                TrackChange("CircuitMaxDischargingCurrent", existingCircuit.CircuitMaxDischargingCurrent, circuit.CircuitMaxDischargingCurrent);

                // UPDATE DEVICE
                device.DeviceName = circuit.DeviceName;
                device.MACID = circuit.MACID;
                device.IPAddress = circuit.IPAddress;
                device.DHCPEnable = circuit.DHCPEnable;
                device.SwVersion = circuit.MasterSwVersion;
                device.ComSwVersion = circuit.ComSwVersion;
                device.ManufactureDateTime = circuit.ManufactureDateTime;
                device.CommissioningDateTime = circuit.CommissioningDateTime;
                device.AssemblyDate = circuit.AssemblyDate;

                // UPDATE CIRCUIT
                existingCircuit.CircuitType = circuit.CircuitType;
                existingCircuit.IsRegistered = circuit.IsRegistered;
                existingCircuit.SwVersion = circuit.SecondarySwVersion;
                existingCircuit.AssemblyDate = circuit.SecondaryAssemblyDate;
                existingCircuit.ZntMaxVoltage = circuit.ZntMaxVoltage;
                existingCircuit.LntMaxVoltage = circuit.LntMaxVoltage;
                existingCircuit.CircuitMaxVoltage = circuit.CircuitMaxVoltage;
                existingCircuit.CircuitMinVoltage = circuit.CircuitMinVoltage;
                existingCircuit.CircuitMaxChargingCurrent = circuit.CircuitMaxChargingCurrent;
                existingCircuit.CircuitMaxDischargingCurrent = circuit.CircuitMaxDischargingCurrent;
                existingCircuit.UpdatedBy = circuit.UpdatedBy;
                existingCircuit.UpdatedAt = circuit.UpdatedAt ?? DateTime.Now;

                await _context.SaveChangesAsync();

                // AUDIT LOG ONLY IF CHANGES EXIST
                if (changes.Any())
                {
                    await _audit.LogEventAsync(new AuditLog
                    {
                        Action = Models.Enums.AuditActionType.UPDATE,
                        Details = $"Circuit {circuit.DeviceID}-{circuit.CircuitID} Updated",
                        Status = Models.Enums.SeverityLevel.INFO,
                        Module = Models.Enums.ModuleName.CIRCUIT,
                        Timestamp = DateTime.Now,
                        Metadata = JsonConvert.SerializeObject(changes)
                    });
                }

                return CommonResponse<CircuitDto>.Ok(circuit, "Updated successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("UpdateAsync Error: " + ex.Message);
                return CommonResponse<CircuitDto>.Fail("Error updating circuit.");
            }
        }

        public async Task<CommonResponse<CircuitDto>> DeleteAsync(CircuitDto circuit)
        {
            try
            {
                // Check if circuit exists
                var existing = await _context.Circuits
                    .FirstOrDefaultAsync(c => c.CircuitID == circuit.CircuitID && c.DeviceId == circuit.DeviceID);

                if (existing == null)
                    return CommonResponse<CircuitDto>.Fail("Circuit not found.");

                // Soft delete: mark as disabled
                existing.IsDeleted = true;
                existing.IsRegistered = false;
                existing.UpdatedAt = circuit.UpdatedAt ?? DateTime.Now; // optional: track when disabled
                existing.UpdatedBy = circuit.UpdatedBy; // optional: track who disabled
                await _context.SaveChangesAsync();

                circuit.IsDeleted = true;
                circuit.IsRegistered = false;


                await _audit.LogEventAsync(new AuditLog
                {
                    Action = Models.Enums.AuditActionType.DELETE,
                    Details = $"Circuit {circuit.DeviceID}-{circuit.CircuitID} Deleted",
                    Status = Models.Enums.SeverityLevel.WARNING,
                    Module = Models.Enums.ModuleName.CIRCUIT,
                    Timestamp = DateTime.Now,
                });

                return CommonResponse<CircuitDto>.Ok(circuit, "Circuit has been disabled successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteAsync error: {ex.Message}");
                return CommonResponse<CircuitDto>.Fail("Error disabling the circuit.");
            }

        }

        public async Task<CommonResponse<CircuitDto>> UpdateIsDeleteAsync(CircuitDto circuit, bool IsDelete = false)
        {
            try
            {
                // Check if circuit exists
                var existing = await _context.Circuits
                    .FirstOrDefaultAsync(c => c.CircuitID == circuit.CircuitID && c.DeviceId == circuit.DeviceID);

                if (existing == null)
                    return CommonResponse<CircuitDto>.Fail("Circuit not found.");

                // Soft delete: mark as disabled
                existing.IsDeleted = IsDelete;
                existing.UpdatedAt = circuit.UpdatedAt ?? DateTime.Now; // optional: track when disabled
                existing.UpdatedBy = CurrentUser.UserName ?? "Service"; // optional: track who disabled
                await _context.SaveChangesAsync();
                circuit.IsDeleted = IsDelete;
                return CommonResponse<CircuitDto>.Ok(circuit, "Circuit has been disabled successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteAsync error: {ex.Message}");
                return CommonResponse<CircuitDto>.Fail("Error disabling the circuit.");
            }

        }

        public async Task<CommonResponse<CircuitDto>> AllowCicuitAsync(CircuitDto circuit, bool IsRegistred)
        {
            try
            {
                // Check if circuit exists
                var existing = await _context.Circuits
                    .FirstOrDefaultAsync(c => c.CircuitID == circuit.CircuitID && c.DeviceId == circuit.DeviceID);

                if (existing == null)
                    return CommonResponse<CircuitDto>.Fail("Circuit not found.");

                // Soft delete: mark as disabled
                existing.IsRegistered = IsRegistred;
                
                if (existing.CreatedBy == null)
                {
                    existing.CreatedBy = CurrentUser.UserName;
                    existing.CreatedAt = circuit.CreatedAt ?? DateTime.Now; // optional: track when disabled
                }

                existing.UpdatedAt = circuit.UpdatedAt ?? DateTime.Now; // optional: track when disabled
                existing.UpdatedBy = CurrentUser.UserName;

                await _context.SaveChangesAsync();
               
                circuit.IsRegistered = IsRegistred;

                await _audit.LogEventAsync(new AuditLog
                {
                    Action = Models.Enums.AuditActionType.APPROVE,
                    Details = $"Circuit {circuit.DeviceID}-{circuit.CircuitID} APPROVE",
                    Status = Models.Enums.SeverityLevel.INFO,
                    Module = Models.Enums.ModuleName.CIRCUIT,
                    Timestamp = DateTime.Now,
                });

                return CommonResponse<CircuitDto>.Ok(circuit, "Circuit has been disabled successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteAsync error: {ex.Message}");
                return CommonResponse<CircuitDto>.Fail("Error disabling the circuit.");
            }
        }

        public async Task<CommonResponse<ManufacturingDetailDTO>> UpdateManufacturingAsync(CircuitDto circuit, ManufacturingDetailDTO manufacturing)
        {
            try
            {
                // 1️⃣ Validate input
                if (manufacturing == null)
                    return CommonResponse<ManufacturingDetailDTO>.Fail("Manufacturing details are required.");

                if (circuit == null)
                    return CommonResponse<ManufacturingDetailDTO>.Fail("Circuit details are required.");

                // 2️⃣ Fetch Device (Primary)
                var device = await _context.Devices
                    .FirstOrDefaultAsync(d => d.DeviceID == circuit.DeviceID);

                if (device == null)
                    return CommonResponse<ManufacturingDetailDTO>.Fail("Device not found.");

                // 3️⃣ Update Device fields (Primary)
                device.SwVersion = manufacturing.MasterSWVersion;
                device.ComSwVersion = manufacturing.ComSWVersion;
                device.PrimarySerialNumber = manufacturing.PrimarySerialNumber;
                device.ManufactureDateTime = manufacturing.ManufactureDateTime;
                device.AssemblyDate = manufacturing.PrimaryPCBAssemblyDateTime;
                device.CommissioningDateTime = manufacturing.CommissioningDateTime;
                device.UpdatedAt = DateTime.Now;

                // 4️⃣ Fetch Circuit (Secondary)
                var circuitEntity = await _context.Circuits
                    .FirstOrDefaultAsync(c => c.DeviceId == circuit.DeviceID &&  c.CircuitID == circuit.CircuitID);

                if (circuitEntity == null)
                    return CommonResponse<ManufacturingDetailDTO>.Fail("Circuit not found.");

                // 5️⃣ Update Circuit fields (Secondary)
                circuitEntity.SwVersion = manufacturing.SecondarySWVersion;
                circuitEntity.SecondarySerialNumber = manufacturing.SecondarySerialNumber;
                circuitEntity.AssemblyDate = manufacturing.SecondaryPCBAssemblyDateTime;
                circuitEntity.UpdatedAt = DateTime.Now;

                // 6️⃣ Save
                await _context.SaveChangesAsync();

                await _audit.LogEventAsync(new AuditLog
                {
                    Action = Models.Enums.AuditActionType.CONFIGURE,
                    Details = $"Circuit {circuit.DeviceID}-{circuit.CircuitID} Manufacturing CONFIGURE Downloaded",
                    Status = Models.Enums.SeverityLevel.INFO,
                    Module = Models.Enums.ModuleName.CIRCUIT,
                    Timestamp = DateTime.Now,
                });

                return CommonResponse<ManufacturingDetailDTO>.Ok(
                    manufacturing,
                    "Manufacturing details updated successfully."
                );
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "UpdateManufacturingAsync error for Device={DeviceId} Circuit={CircuitId}", circuit?.DeviceID, circuit?.CircuitID);

                return CommonResponse<ManufacturingDetailDTO>.Fail(
                    "An error occurred while updating manufacturing details."
                );
            }
        }


        public async Task<CommonResponse<FactoryConfigDetailDTO>> UpdateFactoryAsync(CircuitDto circuit, FactoryConfigDetailDTO factory)
        {
            try
            {
                // 1️⃣ Validate input
                if (factory == null)
                    return CommonResponse<FactoryConfigDetailDTO>.Fail("Factory configuration is required.");

                // 2️⃣ Find Device by CircuitNumber (DeviceID)
                var device = await _context.Devices
                    .FirstOrDefaultAsync(d => d.DeviceID == circuit.DeviceID);

                if (device == null)
                    return CommonResponse<FactoryConfigDetailDTO>.Fail("Device not found.");

                // 3️⃣ Update Device-level configuration
                device.MACID = factory.MacID;
                device.IPAddress = factory.DeviceIPAddress;
                device.DHCPEnable = factory.DhcpEnabled;
                device.ClientRemoteIPAddress = factory.ClientRemoteIPAddress;
                device.TcpClientRemotePort = factory.TcpClientRemotePort;
                device.UdpClientRemotePort = factory.UdpClientRemotePort;
                device.UdpStoreRemotePort = factory.UdpStoreRemotePort;
                device.UpdatedAt = DateTime.Now;

                // 4️⃣ Fetch Circuit
                var circuitEntity = await _context.Circuits
                    .FirstOrDefaultAsync(c =>
                        c.DeviceId == device.DeviceID &&
                        c.CircuitID == circuit.CircuitID);

                if (circuitEntity == null)
                    return CommonResponse<FactoryConfigDetailDTO>.Fail("Circuit not found.");

                // 5️⃣ Update Circuit-level configuration
                circuitEntity.CircuitType = factory.CircuitType;
                circuitEntity.ZntMaxVoltage = factory.ZntMaxVoltage;
                circuitEntity.LntMaxVoltage = factory.LntMaxVoltage;
                circuitEntity.CircuitMaxVoltage = factory.CircuitMaxVoltage;
                circuitEntity.CircuitMinVoltage = factory.CircuitMinVoltage;
                circuitEntity.CircuitMaxDischargingCurrent = factory.CircuitMaxDischargeCurrent;
                circuitEntity.CircuitMaxChargingCurrent = factory.CircuitMaxChargeCurrent;
                circuitEntity.UpdatedAt = DateTime.UtcNow;

                // 6️⃣ Save changes
                await _context.SaveChangesAsync();


                await _audit.LogEventAsync(new AuditLog
                {
                    Action = Models.Enums.AuditActionType.CONFIGURE,
                    Details = $"Circuit {circuit.DeviceID}-{circuit.CircuitID} Factory CONFIGURE Downloaded",
                    Status = Models.Enums.SeverityLevel.INFO,
                    Module = Models.Enums.ModuleName.CIRCUIT,
                    Timestamp = DateTime.Now,
                });

                return CommonResponse<FactoryConfigDetailDTO>.Ok(
                    factory,
                    "Factory configuration updated successfully."
                );
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "UpdateFactoryAsync error for Device={DeviceId} Circuit={CircuitId}", circuit?.DeviceID, circuit?.CircuitID);

                return CommonResponse<FactoryConfigDetailDTO>.Fail(
                    "An error occurred while updating factory configuration."
                );
            }
        }

        public async Task<CommonResponse<ManufacturingDetailDTO>> GetManufacturingAsync(CircuitDto circuit)
        {
            try
            {
                if (circuit == null)
                    return CommonResponse<ManufacturingDetailDTO>.Fail("Circuit details are required.");

                // 1️⃣ Fetch Device (Primary)
                // AsNoTracking() is required here: AppDbContext is Scoped, and in Blazor Server a
                // "scope" lives for the whole SignalR circuit (the browser session), not one request.
                // Without it, a Device entity loaded once on this DbContext stays tracked/cached for
                // the rest of the session — a later UpdateManufacturingAsync (which runs on its own
                // freshly-scoped DbContext via ServiceLocator.GetScoped) commits new values to the DB,
                // but this method would keep returning the stale in-memory tracked instance instead of
                // re-reading the updated row.
                var device = await _context.Devices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.DeviceID == circuit.DeviceID);

                if (device == null)
                    return CommonResponse<ManufacturingDetailDTO>.Fail("Device not found.");

                // 2️⃣ Fetch Circuit (Secondary)
                var circuitEntity = await _context.Circuits
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c =>
                        c.DeviceId == circuit.DeviceID &&
                        c.CircuitID == circuit.CircuitID);

                if (circuitEntity == null)
                    return CommonResponse<ManufacturingDetailDTO>.Fail("Circuit not found.");

                // 3️⃣ Map to DTO
                var dto = new ManufacturingDetailDTO
                {
                    MasterSWVersion = device.SwVersion,
                    ComSWVersion = device.ComSwVersion,
                    PrimarySerialNumber = device.PrimarySerialNumber,
                    ManufactureDateTime = device.ManufactureDateTime ?? DateTime.MinValue,
                    PrimaryPCBAssemblyDateTime = device.AssemblyDate ?? DateTime.MinValue,
                    CommissioningDateTime = device.CommissioningDateTime ?? DateTime.MinValue,

                    SecondarySWVersion = circuitEntity.SwVersion,
                    SecondarySerialNumber = circuitEntity.SecondarySerialNumber,
                    SecondaryPCBAssemblyDateTime = circuitEntity.AssemblyDate ?? DateTime.MinValue
                };

                return CommonResponse<ManufacturingDetailDTO>.Ok(
                    dto,
                    "Manufacturing details fetched successfully."
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetManufacturingAsync error: {ex}");

                return CommonResponse<ManufacturingDetailDTO>.Fail(
                    "An error occurred while fetching manufacturing details."
                );
            }
        }

        public async Task<CommonResponse<FactoryConfigDetailDTO>> GetFactoryAsync(CircuitDto circuit)
        {
            try
            {
                if (circuit == null)
                    return CommonResponse<FactoryConfigDetailDTO>.Fail("Circuit details are required.");

                // 1️⃣ Fetch Device
                // AsNoTracking() — see the identical comment in GetManufacturingAsync above: this avoids
                // returning a stale tracked Device/Circuit entity cached from earlier in this Blazor
                // circuit's long-lived scoped DbContext.
                var device = await _context.Devices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.DeviceID == circuit.DeviceID);

                if (device == null)
                    return CommonResponse<FactoryConfigDetailDTO>.Fail("Device not found.");

                // 2️⃣ Fetch Circuit
                var circuitEntity = await _context.Circuits
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c =>
                        c.DeviceId == circuit.DeviceID &&
                        c.CircuitID == circuit.CircuitID);

                if (circuitEntity == null)
                    return CommonResponse<FactoryConfigDetailDTO>.Fail("Circuit not found.");

                // 3️⃣ Map to DTO
                var dto = new FactoryConfigDetailDTO
                {
                    MacID = device.MACID,
                    DeviceIPAddress = device.IPAddress,
                    DhcpEnabled = device.DHCPEnable,
                    ClientRemoteIPAddress = device.ClientRemoteIPAddress,
                    TcpClientRemotePort = device.TcpClientRemotePort,
                    UdpClientRemotePort = device.UdpClientRemotePort,
                    UdpStoreRemotePort = device.UdpStoreRemotePort,

                    CircuitType = circuitEntity.CircuitType,
                    ZntMaxVoltage = circuitEntity.ZntMaxVoltage ?? 0,
                    LntMaxVoltage = circuitEntity.LntMaxVoltage ?? 0,
                    CircuitMaxVoltage = circuitEntity.CircuitMaxVoltage ?? 0,
                    CircuitMinVoltage = circuitEntity.CircuitMinVoltage ?? 0,
                    CircuitMaxDischargeCurrent = circuitEntity.CircuitMaxDischargingCurrent ?? 0,
                    CircuitMaxChargeCurrent = circuitEntity.CircuitMaxChargingCurrent ?? 0
                };

                return CommonResponse<FactoryConfigDetailDTO>.Ok(
                    dto,
                    "Factory configuration fetched successfully."
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetFactoryAsync error: {ex}");

                return CommonResponse<FactoryConfigDetailDTO>.Fail(
                    "An error occurred while fetching factory configuration."
                );
            }
        }

        #region Calibration
        public async Task<CommonResponse<bool>> CreateOrUpdateCalibrationDataAsync(CircuitDto circuit, CalibrationDataPoint calibration)
        {
            try
            {
                var existing = await _context.CalibrationDataPoints
                    .FirstOrDefaultAsync(x =>
                        x.DeviceId == circuit.DeviceID &&
                        x.CircuitId == circuit.CircuitID &&
                        x.Type == calibration.Type &&
                        x.Mode == calibration.Mode &&
                        x.Range == calibration.Range);

                Dictionary<string, object> changes = new Dictionary<string, object>();


                if (existing == null)
                {
                    // CREATE
                    calibration.DeviceId = circuit.DeviceID;
                    calibration.CircuitId = circuit.CircuitID;
                    calibration.DateTime = DateTime.Now;
                    calibration.CreatedBy = CurrentUser.UserName;
                    calibration.CreatedAt = DateTime.Now;

                    _context.CalibrationDataPoints.Add(calibration);
                }
                else
                {
                    // UPDATE
                    // 🔹 CAPTURE OLD VALUES BEFORE UPDATE
                    if (!Equals(existing.Gain, calibration.Gain))
                    {
                        changes["Gain"] = new { Old = existing.Gain, New = calibration.Gain };
                    }

                    if (!Equals(existing.Offset, calibration.Offset))
                    {
                        changes["Offset"] = new { Old = existing.Offset, New = calibration.Offset };
                    }

                    existing.Gain = calibration.Gain;
                    existing.Offset = calibration.Offset;
                    existing.DateTime = DateTime.Now;

                    calibration.UpdatedBy = CurrentUser.UserName;
                    calibration.UpdatedAt = DateTime.Now;

                }

                await _context.SaveChangesAsync();

                // 🔹 AUDIT LOG (ONLY ADDITION)
                await _audit.LogEventAsync(new AuditLog
                {
                    Action = Models.Enums.AuditActionType.CONFIGURE,
                    Details = existing == null
                                ? $"Calibration CREATED for Circuit {circuit.DeviceID}-{circuit.CircuitID} [{calibration.Mode}-{calibration.Type}]"
                                : (changes.Any()
                                    ? $"Calibration UPDATED for Circuit {circuit.DeviceID}-{circuit.CircuitID} [{calibration.Mode}-{calibration.Type}]"
                                    : $"Calibration NO CHANGE for Circuit {circuit.DeviceID}-{circuit.CircuitID} [{calibration.Mode}-{calibration.Type}]"),
                                                Status = Models.Enums.SeverityLevel.INFO,
                    Module = Models.Enums.ModuleName.CIRCUIT,
                    Timestamp = DateTime.Now,
                    Metadata = existing == null
                        ? JsonConvert.SerializeObject(calibration) // CREATE
                        : (changes.Any()
                            ? JsonConvert.SerializeObject(changes) // UPDATE with changes
                            : null) // NO CHANGE
                });

                return CommonResponse<bool>.Ok(true, "Calibration data saved successfully");
            }
            catch (Exception ex)
            {
                // log ex here if you have logger
                return CommonResponse<bool>.Fail($"Failed to save calibration data: {ex.Message}");
            }
        }

        public async Task<CommonResponse<CalibrationDataPointDto>> GetCalibrationDataAsync(CircuitDto circuit)
        {
            try
            {
                var data = await _context.CalibrationDataPoints
                    .AsNoTracking()
                    .Where(x =>
                        x.DeviceId == circuit.DeviceID &&
                        x.CircuitId == circuit.CircuitID)
                    .OrderByDescending(x => x.DateTime)
                    .Select(selector: x => new CalibrationDataPointDto
                    {
                        Id = x.Id,
                        DeviceId = x.DeviceId,
                        CircuitId = x.CircuitId,
                        Mode = x.Mode,
                        Type = x.Type,
                        DateTime = x.DateTime,
                        Gain = x.Gain,
                        Offset = x.Offset
                    })
                    .FirstOrDefaultAsync();

                if (data == null)
                    return CommonResponse<CalibrationDataPointDto>
                        .Fail("Calibration data not found");

                return CommonResponse<CalibrationDataPointDto>
                    .Ok(data, "Calibration data retrieved");
            }
            catch (Exception ex)
            {
                return CommonResponse<CalibrationDataPointDto>.Fail($"Failed to fetch calibration data: {ex.Message}");
            }
        }

        public async Task<CommonResponse<CalibrationData>> GetAllCalibrationDataAsync(CircuitDto circuit)
        {
            try
            {
                var points = await _context.CalibrationDataPoints
                    .Where(x =>
                        x.DeviceId == circuit.DeviceID &&
                        x.CircuitId == circuit.CircuitID)
                    .GroupBy(x => new { x.Type, x.Mode, x.Range })
                    .Select(g => g
                        .OrderByDescending(x => x.DateTime)
                        .First())
                    .Select(x => new CalibrationDataPointDto
                    {
                        Id = x.Id,
                        DeviceId = x.DeviceId,
                        CircuitId = x.CircuitId,
                        Mode = x.Mode,
                        Type = x.Type,
                        Range = x.Range,
                        DateTime = x.DateTime,
                        Gain = x.Gain,
                        Offset = x.Offset
                    })
                    .OrderByDescending(x => x.DateTime)
                    .ToListAsync();

                if (!points.Any())
                {
                    return CommonResponse<CalibrationData>
                        .Fail("No calibration data found", new CalibrationData());
                }

                var result = new CalibrationData
                {
                    // Current: group by Range so each range gets its own entry in the list
                    CurrentCharge = points
                        .Where(x => x.Type == CalibrationType.Current && x.Mode == CalibrationMode.Charge)
                        .ToList(),

                    CurrentDischarge = points
                        .Where(x => x.Type == CalibrationType.Current && x.Mode == CalibrationMode.Discharge)
                        .ToList(),

                    VoltageCharge = points.FirstOrDefault(x =>
                        x.Type == CalibrationType.Voltage &&
                        x.Mode == CalibrationMode.Charge),

                    VoltageDischarge = points.FirstOrDefault(x =>
                        x.Type == CalibrationType.Voltage &&
                        x.Mode == CalibrationMode.Discharge),
                };

                return CommonResponse<CalibrationData>
                    .Ok(result, "Calibration data retrieved successfully");
            }
            catch (Exception ex)
            {
                return CommonResponse<CalibrationData>
                    .Fail($"Failed to retrieve calibration data: {ex.Message}");
            }
        }

        #endregion
    }
}
