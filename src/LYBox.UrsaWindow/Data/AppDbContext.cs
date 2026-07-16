using LYBox.Plugin.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace LYBox.UrsaWindow.Data;

public class AppDbContext : DbContext
{
    public DbSet<SettingItem> Settings { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SettingItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.Property(e => e.Key).IsRequired().HasMaxLength(256);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.GroupName).IsRequired().HasMaxLength(128);
            entity.Property(e => e.RawValue).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.OptionsJson).HasMaxLength(4096);
            entity.Property(e => e.DefaultValue).HasMaxLength(2048);
            entity.Property(e => e.PluginId).HasMaxLength(128);
        });
    }
}
