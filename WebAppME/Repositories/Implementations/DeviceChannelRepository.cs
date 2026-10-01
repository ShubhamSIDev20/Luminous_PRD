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
    public class DeviceChannelRepository : Repository<Device>, IDeviceChannelRepository
    {
        private readonly IAuditRepository _audit;

        public DeviceChannelRepository(AppDbContext context, IAuditRepository audit) : base(context)
        {
            _audit = audit;
        }

        public async Task<SecondaryBoard> GetOrCreateBoardAsync(int deviceId, int boardNumber)
        {
            var board = await _context.SecondaryBoards
                .FirstOrDefaultAsync(b => b.DeviceId == deviceId && b.BoardNumber == boardNumber);

            if (board != null)
                return board;

            board = new SecondaryBoard
            {
                DeviceId = deviceId,
                BoardNumber = boardNumber,
                IsImplicit = boardNumber == 1
            };

            await _context.SecondaryBoards.AddAsync(board);
            await _context.SaveChangesAsync();

            return board;
        }

        public async Task EnsureBoardOneAsync(int deviceId)
        {
            await GetOrCreateBoardAsync(deviceId, 1);
        }

        public async Task<CommonResponse<ChannelDto>> GetChannelAsync(ChannelDto channel)
        {
            try
            {
                // Read-only lookup: do NOT create the board here. The device row may not
                // exist yet (first-time registration of a new device), and creating a
                // SecondaryBoard for a non-existent DeviceId would violate the FK constraint.
                // Board creation belongs to InsertAsync, which creates the Device first.
                var board = await _context.SecondaryBoards
                    .FirstOrDefaultAsync(b => b.DeviceId == channel.DeviceID && b.BoardNumber == channel.SecondaryBoardNumber);

                if (board == null)
                    return CommonResponse<ChannelDto>.Fail("Channel not found.");

                var result = await (
                        from device in _context.Devices
                        join ch in _context.Channels
                            on device.DeviceID equals ch.DeviceId
                        where device.DeviceID == channel.DeviceID &&
                              ch.SecondaryBoardId == board.Id &&
                              ch.ChannelNumber == channel.ChannelNumber
                        select new ChannelDto
                        {
                            DeviceID = device.DeviceID,
                            SecondaryBoardNumber = channel.SecondaryBoardNumber,
                            ChannelNumber = ch.ChannelNumber,
                            DeviceName = device.DeviceName,
                            MACID = device.MACID,
                            IPAddress = device.IPAddress,
                            IsRegistered = ch.IsRegistered,
                            IsDeleted = ch.IsDeleted,
                            DHCPEnable = device.DHCPEnable,
                            MasterSwVersion = device.SwVersion,
                            ComSwVersion = device.ComSwVersion,
                            ManufactureDateTime = device.ManufactureDateTime,
                            CommissioningDateTime = device.CommissioningDateTime,
                            AssemblyDate = device.AssemblyDate,

                            ChannelType = ch.ChannelType,
                            SecondarySwVersion = ch.SwVersion,
                            SecondaryAssemblyDate = ch.AssemblyDate,
                            ZntMaxVoltage = ch.ZntMaxVoltage,
                            LntMaxVoltage = ch.LntMaxVoltage,
                            ChannelMaxVoltage = ch.ChannelMaxVoltage,
                            ChannelMinVoltage = ch.ChannelMinVoltage,
                            ChannelMaxDischargingCurrent = ch.ChannelMaxDischargingCurrent,
                            ChannelMaxChargingCurrent = ch.ChannelMaxChargingCurrent,
                            CreatedAt = ch.CreatedAt,
                            CreatedBy = ch.CreatedBy,
                            UpdatedAt = ch.UpdatedAt,
                            UpdatedBy = ch.UpdatedBy
                        }
                    ).FirstOrDefaultAsync();

                if (result == null)
                    return CommonResponse<ChannelDto>.Fail("Channel not found.");

                return CommonResponse<ChannelDto>.Ok(result, "Channel retrieved successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetChannelAsync Error: {ex.Message}");
                return CommonResponse<ChannelDto>.Fail("Error retrieving channel. Please try again.");
            }
        }

        public async Task<CommonResponse<List<ChannelDto>>> GetChannelsAsync()
        {
            try
            {
                var result = await (
                    from device in _context.Devices
                    join channel in _context.Channels
                        on device.DeviceID equals channel.DeviceId
                    join board in _context.SecondaryBoards
                        on channel.SecondaryBoardId equals board.Id
                    select new ChannelDto
                    {
                        DeviceID = device.DeviceID,
                        SecondaryBoardNumber = board.BoardNumber,
                        ChannelNumber = channel.ChannelNumber,
                        DeviceName = device.DeviceName,
                        MACID = device.MACID,
                        IPAddress = device.IPAddress,
                        IsRegistered = channel.IsRegistered,
                        IsDeleted = channel.IsDeleted,
                        DHCPEnable = device.DHCPEnable,
                        MasterSwVersion = device.SwVersion,
                        ComSwVersion = device.ComSwVersion,
                        ManufactureDateTime = device.ManufactureDateTime,
                        CommissioningDateTime = device.CommissioningDateTime,
                        AssemblyDate = device.AssemblyDate,
                        ChannelType = channel.ChannelType,
                        SecondarySwVersion = channel.SwVersion,
                        SecondaryAssemblyDate = channel.AssemblyDate,
                        PrimarySerialNumber = device.PrimarySerialNumber,
                        SecondarySerialNumber = channel.SecondarySerialNumber ?? string.Empty,
                        ZntMaxVoltage = channel.ZntMaxVoltage,
                        LntMaxVoltage = channel.LntMaxVoltage,
                        ChannelMaxVoltage = channel.ChannelMaxVoltage,
                        ChannelMinVoltage = channel.ChannelMinVoltage,
                        ChannelMaxDischargingCurrent = channel.ChannelMaxDischargingCurrent,
                        ChannelMaxChargingCurrent = channel.ChannelMaxChargingCurrent,
                        CreatedAt = channel.CreatedAt,
                        CreatedBy = channel.CreatedBy,
                        UpdatedAt = channel.UpdatedAt,
                        UpdatedBy = channel.UpdatedBy
                    }
                ).ToListAsync();

                return CommonResponse<List<ChannelDto>>.Ok(result, "Channels retrieved successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetChannelsAsync Error: {ex.Message}");

                return CommonResponse<List<ChannelDto>>.Fail("Error retrieving channels. Please try again.");
            }
        }

        public async Task<CommonResponse<ChannelDto>> InsertAsync(ChannelDto dto)
        {
            try
            {
                if (dto == null)
                    return CommonResponse<ChannelDto>.Fail("Invalid request.");

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
                    await _context.SaveChangesAsync();
                }

                // ---------------------------
                // 2. SECONDARY BOARD CHECK
                // ---------------------------
                var board = await GetOrCreateBoardAsync(dto.DeviceID, dto.SecondaryBoardNumber);

                // ---------------------------
                // 3. CHANNEL CHECK
                // ---------------------------
                var channel = await _context.Channels
                    .FirstOrDefaultAsync(c => c.SecondaryBoardId == board.Id && c.ChannelNumber == dto.ChannelNumber);

                // Channel exists → return already exists (DO NOT MODIFY)
                if (channel != null)
                {
                    channel.IsDeleted = false;
                    dto.IsRegistered = channel.IsRegistered;

                    await _context.SaveChangesAsync();

                    return CommonResponse<ChannelDto>.Ok(
                        dto,
                        "Channel already exists."
                    );
                }

                // Channel does not exist → INSERT new channel
                channel = new Channel
                {
                    ChannelNumber = dto.ChannelNumber,
                    DeviceId = dto.DeviceID,
                    SecondaryBoardId = board.Id,
                    IsRegistered = dto.IsRegistered,
                    IsDeleted = false,
                    ChannelType = dto.ChannelType,
                    SwVersion = dto.SecondarySwVersion,
                    AssemblyDate = dto.SecondaryAssemblyDate,
                    ZntMaxVoltage = dto.ZntMaxVoltage,
                    LntMaxVoltage = dto.LntMaxVoltage,
                    ChannelMaxVoltage = dto.ChannelMaxVoltage,
                    ChannelMinVoltage = dto.ChannelMinVoltage,
                    ChannelMaxDischargingCurrent = dto.ChannelMaxDischargingCurrent,
                    ChannelMaxChargingCurrent = dto.ChannelMaxChargingCurrent,
                    SecondarySerialNumber = dto.SecondarySerialNumber,
                    CreatedAt = dto.CreatedAt ?? DateTime.Now,
                    CreatedBy = dto.CreatedBy,
                    UpdatedAt = dto.UpdatedAt ?? DateTime.Now,
                    UpdatedBy = dto.UpdatedBy
                };

                await _context.Channels.AddAsync(channel);

                // SAVE everything together
                await _context.SaveChangesAsync();

                dto.IsDeleted = channel.IsDeleted;
                dto.IsRegistered = channel.IsRegistered;


                await _audit.LogEventAsync(new AuditLog
                {
                    Action = Models.Enums.AuditActionType.CREATE,
                    Details = $"Channel {dto.DeviceID}-{dto.SecondaryBoardNumber}-{dto.ChannelNumber} Registered",
                    Status = Models.Enums.SeverityLevel.INFO,
                    Module = Models.Enums.ModuleName.CIRCUIT,
                    Timestamp = DateTime.Now
                });

                return CommonResponse<ChannelDto>.Ok(dto, "Channel inserted successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InsertAsync Error: {ex.Message}");
                return CommonResponse<ChannelDto>.Fail("Insert failed. Please try again.");
            }
        }
        public async Task<CommonResponse<ChannelDto>> UpdateRegistration(ChannelDto channel)
        {
            if (channel == null)
                return CommonResponse<ChannelDto>.Fail("Invalid request.");

            try
            {
                // ---------------------------
                // 1. DEVICE CHECK
                // ---------------------------
                var device = await _context.Devices
                    .FirstOrDefaultAsync(d => d.DeviceID == channel.DeviceID);

                if (device == null)
                    return CommonResponse<ChannelDto>.Fail($"Device with ID '{channel.DeviceID}' not found.");

                device.PrimarySerialNumber = channel.PrimarySerialNumber;
                device.IPAddress = channel.IPAddress;
                device.MACID = channel.MACID;
                device.SwVersion = channel.MasterSwVersion;
                device.ComSwVersion = channel.ComSwVersion;
                device.DHCPEnable = channel.DHCPEnable;
                device.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return CommonResponse<ChannelDto>.Ok(channel);
            }
            catch (DbUpdateException ex)
            {
                // Log ex here if you have a logger injected
                return CommonResponse<ChannelDto>.Fail($"Database error while updating device: {ex.Message}");
            }
            catch (Exception ex)
            {
                return CommonResponse<ChannelDto>.Fail($"Unexpected error: {ex.Message}");
            }
        }
        public async Task<CommonResponse<ChannelDto>> UpdateAsync(ChannelDto channel)
        {
            try
            {
                if (channel == null)
                    return CommonResponse<ChannelDto>.Fail("Invalid channel data.");

                var device = await _context.Devices
                    .FirstOrDefaultAsync(x => x.DeviceID == channel.DeviceID);

                if (device == null)
                    return CommonResponse<ChannelDto>.Fail("Device not found.");

                var board = await GetOrCreateBoardAsync(channel.DeviceID, channel.SecondaryBoardNumber);
                var existingChannel = await _context.Channels
                    .FirstOrDefaultAsync(x => x.SecondaryBoardId == board.Id && x.ChannelNumber == channel.ChannelNumber);

                if (existingChannel == null)
                    return CommonResponse<ChannelDto>.Fail("Channel not found.");

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
                TrackChange("DeviceName", device.DeviceName, channel.DeviceName);
                TrackChange("MACID", device.MACID, channel.MACID);
                TrackChange("IPAddress", device.IPAddress, channel.IPAddress);
                TrackChange("DHCPEnable", device.DHCPEnable, channel.DHCPEnable);
                TrackChange("MasterSwVersion", device.SwVersion, channel.MasterSwVersion);
                TrackChange("ComSwVersion", device.ComSwVersion, channel.ComSwVersion);
                TrackChange("ManufactureDateTime", device.ManufactureDateTime, channel.ManufactureDateTime);
                TrackChange("CommissioningDateTime", device.CommissioningDateTime, channel.CommissioningDateTime);
                TrackChange("AssemblyDate", device.AssemblyDate, channel.AssemblyDate);

                // TRACK CHANNEL CHANGES
                TrackChange("ChannelType", existingChannel.ChannelType, channel.ChannelType);
                TrackChange("IsRegistered", existingChannel.IsRegistered, channel.IsRegistered);
                TrackChange("SecondarySwVersion", existingChannel.SwVersion, channel.SecondarySwVersion);
                TrackChange("SecondaryAssemblyDate", existingChannel.AssemblyDate, channel.SecondaryAssemblyDate);
                TrackChange("ZntMaxVoltage", existingChannel.ZntMaxVoltage, channel.ZntMaxVoltage);
                TrackChange("LntMaxVoltage", existingChannel.LntMaxVoltage, channel.LntMaxVoltage);
                TrackChange("ChannelMaxVoltage", existingChannel.ChannelMaxVoltage, channel.ChannelMaxVoltage);
                TrackChange("ChannelMinVoltage", existingChannel.ChannelMinVoltage, channel.ChannelMinVoltage);
                TrackChange("ChannelMaxChargingCurrent", existingChannel.ChannelMaxChargingCurrent, channel.ChannelMaxChargingCurrent);
                TrackChange("ChannelMaxDischargingCurrent", existingChannel.ChannelMaxDischargingCurrent, channel.ChannelMaxDischargingCurrent);

                // UPDATE DEVICE
                device.DeviceName = channel.DeviceName;
                device.MACID = channel.MACID;
                device.IPAddress = channel.IPAddress;
                device.DHCPEnable = channel.DHCPEnable;
                device.SwVersion = channel.MasterSwVersion;
                device.ComSwVersion = channel.ComSwVersion;
                device.ManufactureDateTime = channel.ManufactureDateTime;
                device.CommissioningDateTime = channel.CommissioningDateTime;
                device.AssemblyDate = channel.AssemblyDate;

                // UPDATE CHANNEL
                existingChannel.ChannelType = channel.ChannelType;
                existingChannel.IsRegistered = channel.IsRegistered;
                existingChannel.SwVersion = channel.SecondarySwVersion;
                existingChannel.AssemblyDate = channel.SecondaryAssemblyDate;
                existingChannel.ZntMaxVoltage = channel.ZntMaxVoltage;
                existingChannel.LntMaxVoltage = channel.LntMaxVoltage;
                existingChannel.ChannelMaxVoltage = channel.ChannelMaxVoltage;
                existingChannel.ChannelMinVoltage = channel.ChannelMinVoltage;
                existingChannel.ChannelMaxChargingCurrent = channel.ChannelMaxChargingCurrent;
                existingChannel.ChannelMaxDischargingCurrent = channel.ChannelMaxDischargingCurrent;
                existingChannel.UpdatedBy = channel.UpdatedBy;
                existingChannel.UpdatedAt = channel.UpdatedAt ?? DateTime.Now;

                await _context.SaveChangesAsync();

                // AUDIT LOG ONLY IF CHANGES EXIST
                if (changes.Any())
                {
                    await _audit.LogEventAsync(new AuditLog
                    {
                        Action = Models.Enums.AuditActionType.UPDATE,
                        Details = $"Channel {channel.DeviceID}-{channel.SecondaryBoardNumber}-{channel.ChannelNumber} Updated",
                        Status = Models.Enums.SeverityLevel.INFO,
                        Module = Models.Enums.ModuleName.CIRCUIT,
                        Timestamp = DateTime.Now,
                        Metadata = JsonConvert.SerializeObject(changes)
                    });
                }

                return CommonResponse<ChannelDto>.Ok(channel, "Updated successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("UpdateAsync Error: " + ex.Message);
                return CommonResponse<ChannelDto>.Fail("Error updating channel.");
            }
        }

        public async Task<CommonResponse<ChannelDto>> DeleteAsync(ChannelDto channel)
        {
            try
            {
                var board = await GetOrCreateBoardAsync(channel.DeviceID, channel.SecondaryBoardNumber);

                // Check if channel exists
                var existing = await _context.Channels
                    .FirstOrDefaultAsync(c => c.SecondaryBoardId == board.Id && c.ChannelNumber == channel.ChannelNumber);

                if (existing == null)
                    return CommonResponse<ChannelDto>.Fail("Channel not found.");

                // Soft delete: mark as disabled
                existing.IsDeleted = true;
                existing.IsRegistered = false;
                existing.UpdatedAt = channel.UpdatedAt ?? DateTime.Now; // optional: track when disabled
                existing.UpdatedBy = channel.UpdatedBy; // optional: track who disabled
                await _context.SaveChangesAsync();

                channel.IsDeleted = true;
                channel.IsRegistered = false;


                await _audit.LogEventAsync(new AuditLog
                {
                    Action = Models.Enums.AuditActionType.DELETE,
                    Details = $"Channel {channel.DeviceID}-{channel.SecondaryBoardNumber}-{channel.ChannelNumber} Deleted",
                    Status = Models.Enums.SeverityLevel.WARNING,
                    Module = Models.Enums.ModuleName.CIRCUIT,
                    Timestamp = DateTime.Now,
                });

                return CommonResponse<ChannelDto>.Ok(channel, "Channel has been disabled successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteAsync error: {ex.Message}");
                return CommonResponse<ChannelDto>.Fail("Error disabling the channel.");
            }

        }

        public async Task<CommonResponse<ChannelDto>> UpdateIsDeleteAsync(ChannelDto channel, bool IsDelete = false)
        {
            try
            {
                var board = await GetOrCreateBoardAsync(channel.DeviceID, channel.SecondaryBoardNumber);

                // Check if channel exists
                var existing = await _context.Channels
                    .FirstOrDefaultAsync(c => c.SecondaryBoardId == board.Id && c.ChannelNumber == channel.ChannelNumber);

                if (existing == null)
                    return CommonResponse<ChannelDto>.Fail("Channel not found.");

                // Soft delete: mark as disabled
                existing.IsDeleted = IsDelete;
                existing.UpdatedAt = channel.UpdatedAt ?? DateTime.Now; // optional: track when disabled
                existing.UpdatedBy = CurrentUser.UserName ?? "Service"; // optional: track who disabled
                await _context.SaveChangesAsync();
                channel.IsDeleted = IsDelete;
                return CommonResponse<ChannelDto>.Ok(channel, "Channel has been disabled successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteAsync error: {ex.Message}");
                return CommonResponse<ChannelDto>.Fail("Error disabling the channel.");
            }

        }

        public async Task<CommonResponse<ChannelDto>> AllowCicuitAsync(ChannelDto channel, bool IsRegistred)
        {
            try
            {
                var board = await GetOrCreateBoardAsync(channel.DeviceID, channel.SecondaryBoardNumber);

                // Check if channel exists
                var existing = await _context.Channels
                    .FirstOrDefaultAsync(c => c.SecondaryBoardId == board.Id && c.ChannelNumber == channel.ChannelNumber);

                if (existing == null)
                    return CommonResponse<ChannelDto>.Fail("Channel not found.");

                // Soft delete: mark as disabled
                existing.IsRegistered = IsRegistred;

                if (existing.CreatedBy == null)
                {
                    existing.CreatedBy = CurrentUser.UserName;
                    existing.CreatedAt = channel.CreatedAt ?? DateTime.Now; // optional: track when disabled
                }

                existing.UpdatedAt = channel.UpdatedAt ?? DateTime.Now; // optional: track when disabled
                existing.UpdatedBy = CurrentUser.UserName;

                await _context.SaveChangesAsync();

                channel.IsRegistered = IsRegistred;

                await _audit.LogEventAsync(new AuditLog
                {
                    Action = Models.Enums.AuditActionType.APPROVE,
                    Details = $"Channel {channel.DeviceID}-{channel.SecondaryBoardNumber}-{channel.ChannelNumber} APPROVE",
                    Status = Models.Enums.SeverityLevel.INFO,
                    Module = Models.Enums.ModuleName.CIRCUIT,
                    Timestamp = DateTime.Now,
                });

                return CommonResponse<ChannelDto>.Ok(channel, "Channel has been disabled successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteAsync error: {ex.Message}");
                return CommonResponse<ChannelDto>.Fail("Error disabling the channel.");
            }
        }

        public async Task<CommonResponse<ManufacturingDetailDTO>> UpdateManufacturingAsync(ChannelDto channel, ManufacturingDetailDTO manufacturing)
        {
            try
            {
                // 1️⃣ Validate input
                if (manufacturing == null)
                    return CommonResponse<ManufacturingDetailDTO>.Fail("Manufacturing details are required.");

                if (channel == null)
                    return CommonResponse<ManufacturingDetailDTO>.Fail("Channel details are required.");

                // 2️⃣ Fetch Device (Primary)
                var device = await _context.Devices
                    .FirstOrDefaultAsync(d => d.DeviceID == channel.DeviceID);

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
                device.LastSyncedAt = DateTime.Now;

                // 4️⃣ Fetch Channel (Secondary)
                var board = await GetOrCreateBoardAsync(channel.DeviceID, channel.SecondaryBoardNumber);
                var channelEntity = await _context.Channels
                    .FirstOrDefaultAsync(c => c.SecondaryBoardId == board.Id && c.ChannelNumber == channel.ChannelNumber);

                if (channelEntity == null)
                    return CommonResponse<ManufacturingDetailDTO>.Fail("Channel not found.");

                // 5️⃣ Update Channel fields (Secondary)
                channelEntity.SwVersion = manufacturing.SecondarySWVersion;
                channelEntity.SecondarySerialNumber = manufacturing.SecondarySerialNumber;
                channelEntity.AssemblyDate = manufacturing.SecondaryPCBAssemblyDateTime;
                channelEntity.UpdatedAt = DateTime.Now;
                channelEntity.LastSyncedAt = DateTime.Now;

                // 6️⃣ Save
                await _context.SaveChangesAsync();

                await _audit.LogEventAsync(new AuditLog
                {
                    Action = Models.Enums.AuditActionType.CONFIGURE,
                    Details = $"Channel {channel.DeviceID}-{channel.SecondaryBoardNumber}-{channel.ChannelNumber} Manufacturing CONFIGURE Downloaded",
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
                Serilog.Log.Error(ex, "UpdateManufacturingAsync error for Device={DeviceId} Channel={ChannelId}", channel?.DeviceID, channel?.ChannelNumber);

                return CommonResponse<ManufacturingDetailDTO>.Fail(
                    "An error occurred while updating manufacturing details."
                );
            }
        }


        public async Task<CommonResponse<FactoryConfigDetailDTO>> UpdateFactoryAsync(ChannelDto channel, FactoryConfigDetailDTO factory)
        {
            try
            {
                // 1️⃣ Validate input
                if (factory == null)
                    return CommonResponse<FactoryConfigDetailDTO>.Fail("Factory configuration is required.");

                // 2️⃣ Find Device
                var device = await _context.Devices
                    .FirstOrDefaultAsync(d => d.DeviceID == channel.DeviceID);

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
                device.LastSyncedAt = DateTime.Now;

                // 4️⃣ Fetch Channel
                var board = await GetOrCreateBoardAsync(channel.DeviceID, channel.SecondaryBoardNumber);
                var channelEntity = await _context.Channels
                    .FirstOrDefaultAsync(c =>
                        c.SecondaryBoardId == board.Id &&
                        c.ChannelNumber == channel.ChannelNumber);

                if (channelEntity == null)
                    return CommonResponse<FactoryConfigDetailDTO>.Fail("Channel not found.");

                // 5️⃣ Update Channel-level configuration
                channelEntity.ChannelType = factory.CircuitType;
                channelEntity.ZntMaxVoltage = factory.ZntMaxVoltage;
                channelEntity.LntMaxVoltage = factory.LntMaxVoltage;
                channelEntity.ChannelMaxVoltage = factory.CircuitMaxVoltage;
                channelEntity.ChannelMinVoltage = factory.CircuitMinVoltage;
                channelEntity.ChannelMaxDischargingCurrent = factory.CircuitMaxDischargeCurrent;
                channelEntity.ChannelMaxChargingCurrent = factory.CircuitMaxChargeCurrent;
                channelEntity.UpdatedAt = DateTime.UtcNow;
                channelEntity.LastSyncedAt = DateTime.Now;

                // 6️⃣ Save changes
                await _context.SaveChangesAsync();


                await _audit.LogEventAsync(new AuditLog
                {
                    Action = Models.Enums.AuditActionType.CONFIGURE,
                    Details = $"Channel {channel.DeviceID}-{channel.SecondaryBoardNumber}-{channel.ChannelNumber} Factory CONFIGURE Downloaded",
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
                Serilog.Log.Error(ex, "UpdateFactoryAsync error for Device={DeviceId} Channel={ChannelId}", channel?.DeviceID, channel?.ChannelNumber);

                return CommonResponse<FactoryConfigDetailDTO>.Fail(
                    "An error occurred while updating factory configuration."
                );
            }
        }

        public async Task<CommonResponse<ManufacturingDetailDTO>> GetManufacturingAsync(ChannelDto channel)
        {
            try
            {
                if (channel == null)
                    return CommonResponse<ManufacturingDetailDTO>.Fail("Channel details are required.");

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
                    .FirstOrDefaultAsync(d => d.DeviceID == channel.DeviceID);

                if (device == null)
                    return CommonResponse<ManufacturingDetailDTO>.Fail("Device not found.");

                // 2️⃣ Fetch Channel (Secondary)
                var board = await GetOrCreateBoardAsync(channel.DeviceID, channel.SecondaryBoardNumber);
                var channelEntity = await _context.Channels
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c =>
                        c.SecondaryBoardId == board.Id &&
                        c.ChannelNumber == channel.ChannelNumber);

                if (channelEntity == null)
                    return CommonResponse<ManufacturingDetailDTO>.Fail("Channel not found.");

                // 3️⃣ Map to DTO
                var dto = new ManufacturingDetailDTO
                {
                    MasterSWVersion = device.SwVersion,
                    ComSWVersion = device.ComSwVersion,
                    PrimarySerialNumber = device.PrimarySerialNumber,
                    ManufactureDateTime = device.ManufactureDateTime ?? DateTime.MinValue,
                    PrimaryPCBAssemblyDateTime = device.AssemblyDate ?? DateTime.MinValue,
                    CommissioningDateTime = device.CommissioningDateTime ?? DateTime.MinValue,

                    SecondarySWVersion = channelEntity.SwVersion,
                    SecondarySerialNumber = channelEntity.SecondarySerialNumber,
                    SecondaryPCBAssemblyDateTime = channelEntity.AssemblyDate ?? DateTime.MinValue,

                    // Device and channel are stamped together by UpdateManufacturingAsync, so either
                    // is representative - take the later of the two defensively in case one is stale.
                    LastSyncedAt = (device.LastSyncedAt, channelEntity.LastSyncedAt) switch
                    {
                        (null, null) => null,
                        (var d, null) => d,
                        (null, var c) => c,
                        (var d, var c) => d > c ? d : c
                    }
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

        public async Task<CommonResponse<FactoryConfigDetailDTO>> GetFactoryAsync(ChannelDto channel)
        {
            try
            {
                if (channel == null)
                    return CommonResponse<FactoryConfigDetailDTO>.Fail("Channel details are required.");

                // 1️⃣ Fetch Device
                // AsNoTracking() — see the identical comment in GetManufacturingAsync above: this avoids
                // returning a stale tracked Device/Channel entity cached from earlier in this Blazor
                // circuit's long-lived scoped DbContext.
                var device = await _context.Devices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.DeviceID == channel.DeviceID);

                if (device == null)
                    return CommonResponse<FactoryConfigDetailDTO>.Fail("Device not found.");

                // 2️⃣ Fetch Channel
                var board = await GetOrCreateBoardAsync(channel.DeviceID, channel.SecondaryBoardNumber);
                var channelEntity = await _context.Channels
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c =>
                        c.SecondaryBoardId == board.Id &&
                        c.ChannelNumber == channel.ChannelNumber);

                if (channelEntity == null)
                    return CommonResponse<FactoryConfigDetailDTO>.Fail("Channel not found.");

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

                    CircuitType = channelEntity.ChannelType,
                    ZntMaxVoltage = channelEntity.ZntMaxVoltage ?? 0,
                    LntMaxVoltage = channelEntity.LntMaxVoltage ?? 0,
                    CircuitMaxVoltage = channelEntity.ChannelMaxVoltage ?? 0,
                    CircuitMinVoltage = channelEntity.ChannelMinVoltage ?? 0,
                    CircuitMaxDischargeCurrent = channelEntity.ChannelMaxDischargingCurrent ?? 0,
                    CircuitMaxChargeCurrent = channelEntity.ChannelMaxChargingCurrent ?? 0,

                    LastSyncedAt = (device.LastSyncedAt, channelEntity.LastSyncedAt) switch
                    {
                        (null, null) => null,
                        (var d, null) => d,
                        (null, var c) => c,
                        (var d, var c) => d > c ? d : c
                    }
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
        public async Task<CommonResponse<bool>> CreateOrUpdateCalibrationDataAsync(ChannelDto channel, CalibrationDataPoint calibration)
        {
            try
            {
                var existing = await _context.CalibrationDataPoints
                    .FirstOrDefaultAsync(x =>
                        x.DeviceId == channel.DeviceID &&
                        x.SecondaryBoardNumber == channel.SecondaryBoardNumber &&
                        x.ChannelId == channel.ChannelNumber &&
                        x.Type == calibration.Type &&
                        x.Mode == calibration.Mode &&
                        x.Range == calibration.Range);

                Dictionary<string, object> changes = new Dictionary<string, object>();


                if (existing == null)
                {
                    // CREATE
                    calibration.DeviceId = channel.DeviceID;
                    calibration.SecondaryBoardNumber = channel.SecondaryBoardNumber;
                    calibration.ChannelId = channel.ChannelNumber;
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
                                ? $"Calibration CREATED for Channel {channel.DeviceID}-{channel.SecondaryBoardNumber}-{channel.ChannelNumber} [{calibration.Mode}-{calibration.Type}]"
                                : (changes.Any()
                                    ? $"Calibration UPDATED for Channel {channel.DeviceID}-{channel.SecondaryBoardNumber}-{channel.ChannelNumber} [{calibration.Mode}-{calibration.Type}]"
                                    : $"Calibration NO CHANGE for Channel {channel.DeviceID}-{channel.SecondaryBoardNumber}-{channel.ChannelNumber} [{calibration.Mode}-{calibration.Type}]"),
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

        public async Task<CommonResponse<CalibrationDataPointDto>> GetCalibrationDataAsync(ChannelDto channel)
        {
            try
            {
                var data = await _context.CalibrationDataPoints
                    .AsNoTracking()
                    .Where(x =>
                        x.DeviceId == channel.DeviceID &&
                        x.SecondaryBoardNumber == channel.SecondaryBoardNumber &&
                        x.ChannelId == channel.ChannelNumber)
                    .OrderByDescending(x => x.DateTime)
                    .Select(selector: x => new CalibrationDataPointDto
                    {
                        Id = x.Id,
                        DeviceId = x.DeviceId,
                        SecondaryBoardNumber = x.SecondaryBoardNumber,
                        ChannelId = x.ChannelId,
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

        public async Task<CommonResponse<CalibrationData>> GetAllCalibrationDataAsync(ChannelDto channel)
        {
            try
            {
                var points = await _context.CalibrationDataPoints
                    .Where(x =>
                        x.DeviceId == channel.DeviceID &&
                        x.SecondaryBoardNumber == channel.SecondaryBoardNumber &&
                        x.ChannelId == channel.ChannelNumber)
                    .GroupBy(x => new { x.Type, x.Mode, x.Range })
                    .Select(g => g
                        .OrderByDescending(x => x.DateTime)
                        .First())
                    .Select(x => new CalibrationDataPointDto
                    {
                        Id = x.Id,
                        DeviceId = x.DeviceId,
                        SecondaryBoardNumber = x.SecondaryBoardNumber,
                        ChannelId = x.ChannelId,
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
