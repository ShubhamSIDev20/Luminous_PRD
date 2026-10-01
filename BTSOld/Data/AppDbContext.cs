using BatteryTestingSystem.DbSecurity;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.Entities.Program;
using BatteryTestingSystem.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BatteryTestingSystem.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        //Add-Migration InitialCreate -Context AppDbContext
        //Remove-Migration -Name "InitialCreate" -Project Repository
        //Update-Database  -Context AppDbContext
        //private readonly IColumnEncryptionService? _encryption;
        public AppDbContext(DbContextOptions<AppDbContext> options ) : base(options) //IColumnEncryptionService? encryptionService
        {
            //_encryption = encryptionService;
        }

        public DbSet<Device> Devices { get; set; } = null!;
        public DbSet<Circuit> Circuits { get; set; } = null!;
        public DbSet<BtsPrograms> Programs { get; set; } = null!;
        public DbSet<RegistrationStandard> RegistrationStandards { get; set; } = null!;
        public DbSet<CalibrationDataPoint> CalibrationDataPoints { get; set; } = null!;

        // Programs and Batteries Sessions
        public DbSet<BatterySession> BatterySessions { get; set; } = null!;
        public DbSet<Batteries> Batteries { get; set; } = null!;
        public DbSet<BatteryType> BatteryTypes { get; set; } = null!;

        public DbSet<DbcFileRecord> dbcFileRecords { get; set; } = null!;
       
        public DbSet<ConfigurationEntity> ConfigurationEntitys { get; set; } = null!;

        public DbSet<CodeMessage> CodeMessages { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;

        public DbSet<UserCircuitAccess> UserCircuitAccesses { get; set; } = null!;

        // Scheduler
        public DbSet<ProgramSchedule> ProgramSchedules { get; set; } = null!;
        public DbSet<ScheduleExecutionLog> ScheduleExecutionLogs { get; set; } = null!;

        // Export jobs
        public DbSet<ExportRecord> ExportRecords { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>(b => b.ToTable("Users", "auth"));
            modelBuilder.Entity<ApplicationRole>(b => b.ToTable("Roles", "auth"));
            modelBuilder.Entity<IdentityUserRole<string>>(b => b.ToTable("UserRoles", "auth"));
            modelBuilder.Entity<IdentityUserClaim<string>>(b => b.ToTable("UserClaims", "auth"));
            modelBuilder.Entity<IdentityUserLogin<string>>(b => b.ToTable("UserLogins", "auth"));
            modelBuilder.Entity<IdentityRoleClaim<string>>(b => b.ToTable("RoleClaims", "auth"));
            modelBuilder.Entity<IdentityUserToken<string>>(b => b.ToTable("UserTokens", "auth"));

            base.OnModelCreating(modelBuilder);

            //if (_encryption is not null)
            //    modelBuilder.ApplyEncryption(_encryption);

        }
    }
}
