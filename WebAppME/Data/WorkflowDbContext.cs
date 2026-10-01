using BatteryTestingSystem.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BatteryTestingSystem.Data;

/// <summary>
/// Workflow-canvas persistence, deliberately separate from AppDbContext.
///
/// This exists for branch hygiene, not domain modelling. feat/workflow-canvas-experiment is a
/// permanent side branch that is never merged, and every EF migration rewrites the single
/// generated file Migrations/AppDbContextModelSnapshot.cs — which git cannot merge meaningfully.
/// Giving the canvas its own context and its own migration history removes that entire class of
/// conflict with main (spec section 3).
///
/// The schema annotation on the WorkflowLayout entity is dropped here on purpose: SQLite ignores
/// schemas and warns about them, and an unschemaed mapping keeps the generated DDL byte-identical
/// to the table that already exists in the field.
/// </summary>
public class WorkflowDbContext : DbContext
{
    public WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : base(options) { }

    public DbSet<WorkflowLayout> WorkflowLayouts { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<WorkflowLayout>().ToTable("WorkflowLayouts");
    }
}
