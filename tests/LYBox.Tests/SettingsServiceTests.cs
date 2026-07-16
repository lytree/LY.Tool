using System.Collections.Concurrent;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Models;
using LYBox.UrsaWindow.Data;
using LYBox.UrsaWindow.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LYBox.Tests;

/// <summary>
/// SettingsService 缓存线程安全与功能的单元测试。
/// 使用 SQLite 内存数据库避免文件 I/O。
/// </summary>
public class SettingsServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ServiceProvider _serviceProvider;

    public SettingsServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite(_connection));
        _serviceProvider = services.BuildServiceProvider();

        _dbFactory = _serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        ServiceLocator.Initialize(_serviceProvider);

        using var db = _dbFactory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    [Fact]
    public void GetValue_ReturnsDefault_WhenKeyNotExists()
    {
        var service = new SettingsService(_dbFactory);
        var result = service.GetValue<string>("nonexistent_key");
        Assert.Null(result);
    }

    [Fact]
    public void RegisterSetting_AndGetValue_RoundTrip()
    {
        var service = new SettingsService(_dbFactory);
        service.RegisterSetting(SettingDefinition.Text("test.key", "Test Key",
            defaultValue: "default_value", group: "Test Group"));

        var value = service.GetValue<string>("test.key");
        Assert.Equal("default_value", value);
    }

    [Fact]
    public void SetValue_UpdatesCache_WithoutFullReload()
    {
        var service = new SettingsService(_dbFactory);
        service.RegisterSetting(SettingDefinition.Text("test.update", "Update Test",
            defaultValue: "initial", group: "Test Group"));

        service.SetValue("test.update", "updated_value");

        var value = service.GetValue<string>("test.update");
        Assert.Equal("updated_value", value);
    }

    [Fact]
    public void InvalidateCache_AndGetValue_RebuildsFromDb()
    {
        var service = new SettingsService(_dbFactory);
        service.RegisterSetting(SettingDefinition.Text("test.invalidate", "Invalidate Test",
            defaultValue: "v1", group: "Test Group"));

        // 直接修改数据库（绕过缓存）
        using var db = _dbFactory.CreateDbContext();
        var item = db.Settings.First(s => s.Key == "test.invalidate");
        item.SetValue("db_modified_value");
        db.SaveChanges();

        // 注册新设置会触发 InvalidateCache
        service.RegisterSetting(SettingDefinition.Text("trigger.invalidate", "Trigger",
            defaultValue: "x", group: "Test Group"));

        var value = service.GetValue<string>("test.invalidate");
        Assert.Equal("db_modified_value", value);
    }

    [Fact]
    public async Task ConcurrentGetValue_DoesNotThrow()
    {
        var service = new SettingsService(_dbFactory);
        service.RegisterSetting(SettingDefinition.Text("concurrent.key", "Concurrent",
            defaultValue: "value", group: "Test Group"));

        var results = new ConcurrentBag<string?>();
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() =>
            {
                var v = service.GetValue<string>("concurrent.key");
                results.Add(v);
            }));

        await Task.WhenAll(tasks);

        Assert.All(results, v => Assert.Equal("value", v));
        Assert.Equal(50, results.Count);
    }

    [Fact]
    public async Task ConcurrentReadWithCacheInvalidation_DoesNotThrow()
    {
        var service = new SettingsService(_dbFactory);
        service.RegisterSetting(SettingDefinition.Text("base.key", "Base",
            defaultValue: "base_value", group: "Test Group"));

        var exceptions = new ConcurrentBag<Exception>();
        var barrier = new Barrier(2);

        var readerTask = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < 100; i++)
            {
                try
                {
                    service.GetValue<string>("base.key");
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        });

        var invalidatorTask = Task.Run(() =>
        {
            barrier.SignalAndWait();
            // 短暂延迟后触发一次缓存失效（RemoveSetting → InvalidateCache）
            Thread.Sleep(10);
            try
            {
                service.RemoveSetting("base.key");
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        await Task.WhenAll(readerTask, invalidatorTask);
        Assert.Empty(exceptions);
    }

    [Fact]
    public async Task EnsureCache_CreatesOnlyOneDbContext_UnderConcurrentAccess()
    {
        var createCount = 0;
        var countingFactory = new CountingDbContextFactoryDecorator(_dbFactory,
            () => Interlocked.Increment(ref createCount));

        var service = new SettingsService(countingFactory);
        service.RegisterSetting(SettingDefinition.Text("count.test", "Count",
            defaultValue: "val", group: "Test Group"));

        Interlocked.Exchange(ref createCount, 0);
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => service.GetValue<string>("count.test")));

        await Task.WhenAll(tasks);

        // EnsureCache 应该只创建 1 次 DbContext（首次加载）
        Assert.Equal(1, createCount);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _connection.Dispose();
    }

    private sealed class CountingDbContextFactoryDecorator : IDbContextFactory<AppDbContext>
    {
        private readonly IDbContextFactory<AppDbContext> _inner;
        private readonly Action _onCreate;

        public CountingDbContextFactoryDecorator(IDbContextFactory<AppDbContext> inner, Action onCreate)
        {
            _inner = inner;
            _onCreate = onCreate;
        }

        public AppDbContext CreateDbContext()
        {
            _onCreate();
            return _inner.CreateDbContext();
        }
    }
}
