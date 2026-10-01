using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.Entities.Program;
using BatteryTestingSystem.Models.SqliteEntities;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Metrics;
using System.Reflection.Emit;

namespace BatteryTestingSystem.Data
{
    public class SqliteDbContext : DbContext
    {
        private readonly string _dbPath;
        //public DbSet<RealTimeRecord> Records { get; set; }
        public DbSet<MeasurementData> Measurements { get; set; }
        public DbSet<RegLogRecord> RegLogs { get; set; }
        public DbSet<BtsPrograms> Program { get; set; }
        public DbSet<Batteries> Battery { get; set; }
        public DbSet<ConfigurationEntity> configurationEntities { get; set; }
        public SqliteDbContext(string dbPath)
        {
            _dbPath = dbPath;
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (Database.IsSqlite())
            {
            
            }
        }
    }
}
