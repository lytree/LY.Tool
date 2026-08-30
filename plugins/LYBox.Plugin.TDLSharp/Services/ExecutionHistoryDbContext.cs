using LYBox.Plugin.TDLSharp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;

namespace LYBox.Plugin.TDLSharp.Services;

public class ExecutionHistoryDbContext : DbContext
{
    private static readonly SemaphoreSlim _initLock = new(1, 1);
    private static readonly HashSet<string> _initialized = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _dbPath;

    public ExecutionHistoryDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    public DbSet<ExecutionHistoryRecord> ExecutionRecords { get; set; }

    /// <summary>
    /// 仅在该 db path 第一次被访问时执行 schema 创建；后续访问跳过 EF schema 校验。
    /// </summary>
    public async Task EnsureSchemaInitializedAsync()
    {
        if (_initialized.Contains(_dbPath)) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized.Contains(_dbPath)) return;
            await Database.EnsureCreatedAsync();
            _initialized.Add(_dbPath);
        }
        finally { _initLock.Release(); }
    }

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

    /// <summary>
    /// 根据脚本ID创建独立的数据库上下文，每个脚本使用独立的 db 文件。
    /// </summary>
    public static ExecutionHistoryDbContext CreateForScript(string scriptId)
    {
        var dataDir = TdlPaths.HistoryDir;
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, $"history-{TdlPaths.SafeFileName(scriptId)}.db");
        return new ExecutionHistoryDbContext(dbPath);
    }
}
