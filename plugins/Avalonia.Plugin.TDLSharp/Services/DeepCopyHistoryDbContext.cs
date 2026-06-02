using Avalonia.Plugin.TDLSharp.Models;
using Microsoft.EntityFrameworkCore;

namespace Avalonia.Plugin.TDLSharp.Services;

public class DeepCopyHistoryDbContext : DbContext
{
    private readonly string _dbPath;

    public DeepCopyHistoryDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    public DbSet<DeepCopyHistoryRecord> HistoryRecords { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeepCopyHistoryRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExecutedAt);
            entity.Property(e => e.SourceChannel).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2048);
        });
    }
}
