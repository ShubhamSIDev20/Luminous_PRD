using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities.Program;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Utils;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System;
using System.Security.Cryptography;
using BatteryTestingSystem.Models.Entities;

namespace BatteryTestingSystem.Repositories.Implementations
{
    public class ProgramRepository : Repository<BtsPrograms>, IProgramRepository
    {
        public ProgramRepository(AppDbContext context) : base(context) { }

        #region Program CRUD

        public async Task<CommonResponse<ProgramDTO>> CreateProgramAsync(CreateProgramRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Name))
                    return CommonResponse<ProgramDTO>.Fail("Program name is required.");

                bool exists = await _dbSet
                    .AsNoTracking()
                    .AnyAsync(p =>
                        !p.IsDeleted &&
                        EF.Functions.Like(p.ProgramName, request.Name.Trim())
                    );

                if (exists)
                    return CommonResponse<ProgramDTO>
                        .Fail($"Program '{request.Name}' already exists.");

                var entity = new BtsPrograms
                {
                    ProgramName = request.Name.Trim(),
                    Description = request.Description,
                    MaxAh = request.MaxAh,
                    ProgramTimeTicks = request.ProgramTimeTicks,
                    ProgramJson = request.ProgramJson ?? "[]",
                    ProgramSteps = 0,
                    IsVaild = false,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    ProgramHash = string.Empty,
                    CreatedBy = CurrentUser.UserName,
                    UpdatedBy = CurrentUser.UserName
                };

                await _dbSet.AddAsync(entity);
                await _context.SaveChangesAsync();

                return await GetProgramAsync(entity.Id);
            }
            catch (Exception ex)
            {
                return CommonResponse<ProgramDTO>
                    .Fail($"Failed to create program: {ex.Message}");
            }
        }

        public async Task<CommonResponse<bool>> UpdateProgramAsync(ProgramDTO program)
        {
            try
            {
                if (program == null || program.ProgramId <= 0)
                    return CommonResponse<bool>.Fail("Invalid program data.");

                var entity = await _dbSet.FirstOrDefaultAsync(p => p.Id == program.ProgramId);
                if (entity == null)
                    return CommonResponse<bool>.Fail("Program not found.");

                if (!string.IsNullOrWhiteSpace(program.ProgramName))
                    entity.ProgramName = program.ProgramName.Trim();

                if (program.Description != null)
                    entity.Description = program.Description;

                if (program.MaxAh.HasValue)
                    entity.MaxAh = program.MaxAh.Value;

                if (program.ProgramTimeTicks.HasValue)
                    entity.ProgramTimeTicks = program.ProgramTimeTicks.Value;


                if (!string.IsNullOrWhiteSpace(program.ProgramJson))
                {
                    entity.ProgramJson = program.ProgramJson;
                    entity.ProgramSteps = program.ProgramSteps;
                    entity.ProgramHash = ComputeHash(program.ProgramJson);
                }

                entity.IsVaild = program.IsVaild;
                entity.UpdatedAt = DateTime.Now;
                entity.UpdatedBy = CurrentUser.UserName;

                await _context.SaveChangesAsync();
                return CommonResponse<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return CommonResponse<bool>
                    .Fail($"Failed to update program: {ex.Message}");
            }
        }

        public async Task<CommonResponse<bool>> DeleteProgramAsync(long programId)
        {
            try
            {
                if (programId <= 0)
                    return CommonResponse<bool>.Fail("Invalid program.");

                var entity = await _dbSet.FirstOrDefaultAsync(p => p.Id == programId);
                if (entity == null)
                    return CommonResponse<bool>.Fail("Program not found.");

                entity.IsDeleted = true;
                entity.UpdatedAt = DateTime.Now;
                entity.UpdatedBy = CurrentUser.UserName;

                await _context.SaveChangesAsync();
                return CommonResponse<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return CommonResponse<bool>
                    .Fail($"Failed to delete program: {ex.Message}");
            }
        }

        public async Task<CommonResponse<bool>> RecoverProgramAsync(long programId)
        {
            try
            {
                if (programId <= 0)
                    return CommonResponse<bool>.Fail("Invalid program.");

                var entity = await _dbSet.FirstOrDefaultAsync(p => p.Id == programId);
                if (entity == null)
                    return CommonResponse<bool>.Fail("Program not found.");

                entity.IsDeleted = false;
                entity.UpdatedAt = DateTime.Now;
                entity.UpdatedBy = CurrentUser.UserName;

                await _context.SaveChangesAsync();
                return CommonResponse<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return CommonResponse<bool>
                    .Fail($"Failed to recover program: {ex.Message}");
            }
        }

        #endregion

        #region Program Queries

        public async Task<CommonResponse<ProgramDTO>> GetProgramAsync(long programId)
        {
            try
            {
                if (programId <= 0)
                    return CommonResponse<ProgramDTO>.Fail("Invalid program ID.");

                var program = await _dbSet
                    .AsNoTracking()
                    .Where(p => p.Id == programId && !p.IsDeleted)
                    .Select(p => new ProgramDTO
                    {
                        ProgramId = p.Id,
                        ProgramName = p.ProgramName,
                        Description = p.Description,
                        MaxAh = p.MaxAh,
                        ProgramSteps = p.ProgramSteps,
                        ProgramJson = p.ProgramJson,
                        ProgramTimeTicks = p.ProgramTimeTicks,
                        ProgramHash = p.ProgramHash,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt,
                        IsDeleted = p.IsDeleted,
                        IsVaild = p.IsVaild
                    })
                    .FirstOrDefaultAsync();

                return program == null
                    ? CommonResponse<ProgramDTO>.Fail("Program not found.")
                    : CommonResponse<ProgramDTO>.Ok(program);
            }
            catch (Exception ex)
            {
                return CommonResponse<ProgramDTO>
                    .Fail($"Failed to retrieve program: {ex.Message}");
            }
        }

        public async Task<CommonResponse<ProgramDTO>> GetProgramByNameAsync(string programName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(programName))
                    return CommonResponse<ProgramDTO>.Fail("Program name is required.");

                var program = await _dbSet
                    .AsNoTracking()
                    .Where(p => p.ProgramName == programName.Trim() && !p.IsDeleted)
                    .Select(p => new ProgramDTO
                    {
                        ProgramId = p.Id,
                        ProgramName = p.ProgramName,
                        Description = p.Description,
                        MaxAh = p.MaxAh,
                        ProgramSteps = p.ProgramSteps,
                        ProgramJson = p.ProgramJson,
                        ProgramTimeTicks = p.ProgramTimeTicks,
                        ProgramHash = p.ProgramHash,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt,
                        IsDeleted = p.IsDeleted,
                        IsVaild = p.IsVaild
                    })
                    .FirstOrDefaultAsync();

                return program == null
                    ? CommonResponse<ProgramDTO>.Fail($"Program '{programName}' not found.")
                    : CommonResponse<ProgramDTO>.Ok(program);
            }
            catch (Exception ex)
            {
                return CommonResponse<ProgramDTO>
                    .Fail($"Failed to retrieve program by name: {ex.Message}");
            }
        }

        public async Task<CommonResponse<List<ProgramDTO>>> GetProgramsAsync()
        {
            try
            {
                var programs = await _dbSet
                    .AsNoTracking()
                    .Where(p => !p.IsDeleted)
                    .OrderByDescending(p => p.UpdatedAt)
                    .Select(p => new ProgramDTO
                    {
                        ProgramId = p.Id,
                        ProgramName = p.ProgramName,
                        Description = p.Description,
                        MaxAh = p.MaxAh,
                        IsVaild = p.IsVaild,
                        ProgramSteps = p.ProgramSteps,
                        ProgramJson = p.ProgramJson,
                        ProgramTimeTicks = p.ProgramTimeTicks,
                        ProgramHash = p.ProgramHash,
                        CreatedBy = p.CreatedBy,
                        UpdatedBy = p.UpdatedBy,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                    })
                    .ToListAsync();

                return CommonResponse<List<ProgramDTO>>.Ok(programs);
            }
            catch (Exception ex)
            {
                return CommonResponse<List<ProgramDTO>>
                    .Fail($"Failed to load programs: {ex.Message}");
            }
        }

        #endregion

        #region Sessions

        public async Task<CommonResponse<bool>> AddOrUpdateSession(SessionRecordDto session)
        {
            try
            {
                BatterySession? entity = null;

                if (session.SessionID > 0)
                {
                    entity = await _context.BatterySessions
                        .FirstOrDefaultAsync(x => x.SessionID == session.SessionID);
                }

                if (entity == null && session.DeviceID > 0 && session.ChannelNumber > 0)
                {
                    entity = await _context.BatterySessions
                        .OrderByDescending(x => x.CreatedAt)
                        .FirstOrDefaultAsync(x =>
                            x.DeviceID == session.DeviceID &&
                            x.SecondaryBoardNumber == session.SecondaryBoardNumber &&
                            x.ChannelNumber == session.ChannelNumber);
                }

                if (entity == null && !session.EndTime.HasValue)
                {
                    entity = new BatterySession
                    {
                        SessionName = session.SessionName,
                        StartTime = session.StartTime,
                        EndTime = null,
                        ProgramHash = session.ProgramHash,
                        DeviceID = session.DeviceID,
                        SecondaryBoardNumber = session.SecondaryBoardNumber,
                        ChannelNumber = session.ChannelNumber,
                        BatteryID = session.BatteryID,
                        BatteryName = session.BatteryName,
                        ProgramID = session.ProgramID,
                        ProgramName = session.ProgramName,
                        DbcFileRecordID = (long)(session.DbcFileRecordID ?? 0),
                        DbcName = session.DbcName,
                        SessionFilePath = session.SessionFilePath,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        CreatedBy = CurrentUser.UserName,
                        UpdatedBy = CurrentUser.UserName
                    };

                    _context.BatterySessions.Add(entity);
                }

                if (entity != null)
                {
                    //if (!string.IsNullOrWhiteSpace(session.SessionName))
                    //    entity.SessionName = session.SessionName;

                    //if (session.StartTime != default)
                    //    entity.StartTime = session.StartTime;

                    if (session.EndTime.HasValue)
                    {
                        entity.EndTime = session.EndTime;
                        entity.UpdatedBy = CurrentUser.UserName ?? "End";
                    }                        

                    //if (!string.IsNullOrWhiteSpace(session.ProgramHash))
                    //    entity.ProgramHash = session.ProgramHash;

                    //if (session.DeviceID > 0)
                    //    entity.DeviceID = session.DeviceID;

                    //if (session.CircuitID > 0)
                    //    entity.CircuitID = session.CircuitID;

                    //if (session.BatteryID > 0)
                    //    entity.BatteryID = session.BatteryID;

                    //if (session.ProgramID > 0)
                    //    entity.ProgramID = session.ProgramID;

                    //if (session.DbcFileRecordID.HasValue)
                    //    entity.DbcFileRecordID = session.DbcFileRecordID;

                    //if (!string.IsNullOrWhiteSpace(session.SessionFilePath))
                    //    entity.SessionFilePath = session.SessionFilePath;

                    entity.UpdatedAt = DateTime.Now;

                }

                await _context.SaveChangesAsync();
                return CommonResponse<bool>.Ok(true);
            }
            catch (DbUpdateException dbEx)
            {
                return CommonResponse<bool>.Fail(
                    $"Database error while saving session: {dbEx.Message}");
            }
            catch (Exception ex)
            {
                return CommonResponse<bool>.Fail(
                    $"Failed to save session: {ex.Message}");
            }
        }


        public async Task<CommonResponse<List<SessionRecordDto>>> GetSessionsAsync()
        {
            try
            {
                var sessions = await _context.BatterySessions
                    .AsNoTracking()
                    .OrderByDescending(s => s.SessionID)
                    .Select(s => new SessionRecordDto
                    {
                        SessionName = s.SessionName,
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        UpdatedAt = s.UpdatedAt,
                        CreatedAt = s.CreatedAt,
                        UpdatedBy = s.UpdatedBy,
                        CreatedBy = s.CreatedBy,
                        SessionID = s.SessionID,
                        ProgramID = s.ProgramID,
                        ProgramName = s.ProgramName,
                        ProgramHash = s.ProgramHash,
                        DeviceID = s.DeviceID,
                        SecondaryBoardNumber = s.SecondaryBoardNumber,
                        ChannelNumber = s.ChannelNumber,
                        BatteryID = s.BatteryID,
                        BatteryName = s.BatteryName,
                        DbcFileRecordID = s.DbcFileRecordID,
                        DbcName = s.DbcName,
                        SessionFilePath = s.SessionFilePath,
                    })
                    .ToListAsync();

                if (!sessions.Any())
                    return CommonResponse<List<SessionRecordDto>>
                        .Fail("No session records found.");

                return CommonResponse<List<SessionRecordDto>>.Ok(sessions);
            }
            catch (Exception ex)
            {
                return CommonResponse<List<SessionRecordDto>>
                    .Fail($"Failed to load session steps: {ex.Message}");
            }
        }

        public async Task<CommonResponse<SessionRecordDto>> GetLastSessionAsync(ChannelDto cuitDto)
        {
            try
            {
                if (cuitDto == null || cuitDto.ChannelNumber <= 0)
                    return CommonResponse<SessionRecordDto>.Fail("Invalid Channel ID.");

                var session = await
                    (from s in _context.BatterySessions.AsNoTracking()

                     join p in _context.Programs.AsNoTracking()
                         on s.ProgramID equals p.Id into programJoin
                     from p in programJoin.DefaultIfEmpty()

                     join b in _context.Batteries.AsNoTracking()
                         on s.BatteryID equals b.Id into batteryJoin
                     from b in batteryJoin.DefaultIfEmpty()

                     where s.ChannelNumber == cuitDto.ChannelNumber && s.SecondaryBoardNumber == cuitDto.SecondaryBoardNumber && s.DeviceID == cuitDto.DeviceID
                     orderby s.SessionID descending   // or CreatedAt if you have it

                     select new SessionRecordDto
                     {
                         SessionName = s.SessionName,
                         StartTime = s.StartTime,
                         EndTime = s.EndTime,
                         UpdatedAt = s.UpdatedAt,
                         CreatedAt = s.CreatedAt,
                         UpdatedBy = s.UpdatedBy,
                         CreatedBy = s.CreatedBy,
                         SessionID = s.SessionID,
                         ProgramHash = s.ProgramHash,
                         DeviceID = s.DeviceID,
                         SecondaryBoardNumber = s.SecondaryBoardNumber,
                         ChannelNumber = s.ChannelNumber,
                         BatteryID = s.BatteryID,
                         BatteryName = s.BatteryName,
                         ProgramID = s.ProgramID,
                         ProgramName = s.ProgramName,
                         DbcFileRecordID = s.DbcFileRecordID,
                         DbcName = s.DbcName,
                         Port2DbcFileRecordID = s.Port2DbcFileRecordID,
                         Port2DbcName = s.Port2DbcName,
                         Port3DbcFileRecordID = s.Port3DbcFileRecordID,
                         Port3DbcName = s.Port3DbcName,
                         SessionFilePath = s.SessionFilePath,

                         programs = p == null
                             ? null
                             : new ProgramDTO
                             {
                                 ProgramId = p.Id,
                                 ProgramName = p.ProgramName,
                                 Description = p.Description,
                                 MaxAh = p.MaxAh,
                                 ProgramSteps = p.ProgramSteps,
                                 ProgramJson = p.ProgramJson,
                                 ProgramTimeTicks = p.ProgramTimeTicks,
                                 CreatedAt = p.CreatedAt,
                                 UpdatedAt = p.UpdatedAt,
                                 IsDeleted = p.IsDeleted,
                                 ProgramHash = p.ProgramHash
                             },

                         battery = b == null
                             ? null
                             : new BatteryDTO
                             {
                                 Id = b.Id,
                                 Name = b.Name,
                                 Comments = b.Comments,
                                 ChargeFactor = b.ChargeFactor,
                                 BreakVoltage = b.BreakVoltage,
                                 GassingVoltage = b.GassingVoltage,
                                 BatteryTypeId = b.BatteryTypeId,
                                 ColdCrankingCurrent = b.ColdCrankingCurrent,
                                 EnergyDensity = b.EnergyDensity,
                                 Impedance = b.Impedance,
                                 MaximumVoltage = b.MaximumVoltage,
                                 NominalCapacity = b.NominalCapacity,
                                 NominalCurrent = b.NominalCurrent,
                                 NominalVoltage = b.NominalVoltage,
                                 NumberOfCells = b.NumberOfCells,
                                 Producer = b.Producer,
                                 Quantity = b.Quantity,
                                 CreatedBy = b.CreatedBy,
                                 UpdatedBy = b.UpdatedBy,
                                 CreatedAt = b.CreatedAt,
                                 UpdatedAt = b.UpdatedAt,
                             }
                     })
                    .FirstOrDefaultAsync();

                if (session == null)
                    return CommonResponse<SessionRecordDto>
                        .Fail("No session found for the given circuit.");

                return CommonResponse<SessionRecordDto>.Ok(session);
            }
            catch (Exception ex)
            {
                return CommonResponse<SessionRecordDto>
                    .Fail("Error retrieving session: " + ex.Message);
            }
        }

        public async Task<CommonResponse<bool>> CreateSession(SessionRecordDto session)
        {
            try
            {
                if (session == null)
                    return CommonResponse<bool>.Fail("Session data is required.");

                var entity = new BatterySession
                {
                    SessionID = session.SessionID,
                    SessionName = session.SessionName,
                    StartTime = session.StartTime,
                    EndTime = null,
                    ProgramHash = session.ProgramHash,
                    DeviceID = session.DeviceID,
                    SecondaryBoardNumber = session.SecondaryBoardNumber,
                    ChannelNumber = session.ChannelNumber,
                    BatteryID = session.BatteryID,
                    BatteryName = session.BatteryName,
                    ProgramName = session.ProgramName,
                    ProgramID = session.ProgramID,
                    DbcFileRecordID = session.DbcFileRecordID ?? 0,
                    DbcName = session.DbcName,
                    Port2DbcFileRecordID = session.Port2DbcFileRecordID,
                    Port2DbcName = session.Port2DbcName,
                    Port3DbcFileRecordID = session.Port3DbcFileRecordID,
                    Port3DbcName = session.Port3DbcName,
                    SessionFilePath = session.SessionFilePath,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    CreatedBy = CurrentUser.UserName ?? "System",
                    UpdatedBy = CurrentUser.UserName ?? "System"
                };

                _context.BatterySessions.Add(entity);
                await _context.SaveChangesAsync();

                return CommonResponse<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                // log ex here
                return CommonResponse<bool>.Fail($"Failed to create session: {ex.Message}");
            }
        }

        public async Task<CommonResponse<bool>> EndSession(SessionRecordDto session)
        {
            try
            {
                if (session == null || session.SessionID <= 0)
                    return CommonResponse<bool>.Fail("Invalid session ID.");

                var entity = await _context.BatterySessions
                    .FirstOrDefaultAsync(x => x.SessionID == session.SessionID);

                if (entity == null)
                    return CommonResponse<bool>.Fail("Session not found.");

                if (!session.EndTime.HasValue)
                    return CommonResponse<bool>.Fail("End time is required.");

                entity.EndTime = session.EndTime;
                entity.UpdatedAt = DateTime.Now;
                entity.UpdatedBy = CurrentUser.UserName ?? "End";

                await _context.SaveChangesAsync();

                return CommonResponse<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                // log ex here
                return CommonResponse<bool>.Fail($"Failed to end session: {ex.Message}");
            }
        }


        #endregion

        #region ComputeHash 
        public string ComputeHash(string json)
        {
            try
            {
                using var sha256 = SHA256.Create();
                var bytes = Encoding.UTF8.GetBytes(json);
                return Convert.ToHexString(sha256.ComputeHash(bytes));
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
        #endregion

        #region Registration 
        public async Task<CommonResponse<RegistrationStandardsDTO>> CreateRegistrationStandardAsync(RegistrationStandardsDTO dto)
        {
            try
            {
                bool exists = await _context.RegistrationStandards
                    .AnyAsync(x => x.StandardName == dto.StandardName && !x.IsDeleted);

                if (exists)
                    return CommonResponse<RegistrationStandardsDTO>
                        .Fail("Standard already exists.");

                var entity = new RegistrationStandard
                {
                    StandardName = dto.StandardName,
                    Description = dto.Description,
                    UnitList = dto.UnitList,
                    CreatedBy = CurrentUser.UserName,
                    UpdatedBy = CurrentUser.UserName,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.RegistrationStandards.Add(entity);
                await _context.SaveChangesAsync();

                dto.Id = entity.Id;

                return CommonResponse<RegistrationStandardsDTO>.Ok(dto);
            }
            catch (Exception ex)
            {
                return CommonResponse<RegistrationStandardsDTO>
                    .Fail($"Create failed: {ex.Message}");
            }
        }

        public async Task<CommonResponse<List<RegistrationStandardsDTO>>> GetRegistrationStandardAsync()
        {
            try
            {
                var data = await _context.RegistrationStandards
                    .Where(x => !x.IsDeleted)
                    .Select(x => new RegistrationStandardsDTO
                    {
                        Id = x.Id,
                        StandardName = x.StandardName,
                        Description = x.Description,
                        UnitList = x.UnitList,
                        CreatedAt = x.CreatedAt,
                        UpdatedAt = x.UpdatedAt,
                        CreatedBy = x.CreatedBy,
                        UpdatedBy = x.UpdatedBy
                    })
                    .ToListAsync();

                return CommonResponse<List<RegistrationStandardsDTO>>.Ok(data);
            }
            catch (Exception ex)
            {
                return CommonResponse<List<RegistrationStandardsDTO>>
                    .Fail($"Fetch failed: {ex.Message}");
            }
        }

        public async Task<CommonResponse<RegistrationStandardsDTO>> UpdateRegistrationStandardAsync(RegistrationStandardsDTO dto)
        {
            try
            {
                var entity = await _context.RegistrationStandards
                    .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.IsDeleted);

                if (entity == null)
                    return CommonResponse<RegistrationStandardsDTO>
                        .Fail("Standard not found.");

                entity.StandardName = dto.StandardName;
                entity.Description = dto.Description;
                if (dto.UnitList != null && dto.UnitList.Count != 0)
                    entity.UnitList = dto.UnitList;
                else
                    return CommonResponse<RegistrationStandardsDTO>.Fail("At List one unit Required");
                entity.UpdatedBy = CurrentUser.UserName;
                entity.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return CommonResponse<RegistrationStandardsDTO>.Ok(dto);
            }
            catch (Exception ex)
            {
                return CommonResponse<RegistrationStandardsDTO>
                    .Fail($"Update failed: {ex.Message}");
            }
        }

        public async Task<CommonResponse<bool>> RemoveRegistrationStandardAsync(int id)
        {
            try
            {
                var entity = await _context.RegistrationStandards
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

                if (entity == null)
                    return CommonResponse<bool>.Fail("Standard not found.");

                if (entity.StandardName != "STANDARD" || entity.StandardName != "SIMPLE")
                    entity.IsDeleted = true;
                else
                    return CommonResponse<bool>.Fail("STANDARD/SIMPLE Can not be delete!");

                entity.UpdatedBy = CurrentUser.UserName;
                entity.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return CommonResponse<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return CommonResponse<bool>
                    .Fail($"Remove failed: {ex.Message}");
            }
        }

        #endregion
    }
}
