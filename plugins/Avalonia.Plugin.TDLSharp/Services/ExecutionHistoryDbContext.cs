using Avalonia.Plugin.TDLSharp.Models;
using Microsoft.EntityFrameworkCore;

namespace Avalonia.Plugin.TDLSharp.Services;

public class ExecutionHistoryDbContext : DbContext
{
    private readonly string _dbPath;

    public ExecutionHistoryDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    public DbSet<ExecutionHistoryRecord> ExecutionRecords { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExecutionHistoryRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ScriptId);
            entity.HasIndex(e => e.ExecutedAt);
            entity.Property(e => e.ScriptId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.ScriptName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ParametersJson).HasMaxLength(4096);
            entity.Property(e => e.ParameterSummary).HasMaxLength(1024);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2048);
        });
    }
}
