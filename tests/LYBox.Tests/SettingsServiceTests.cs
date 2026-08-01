using System.Collections.Concurrent;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Models;
using LYBox.Layout.Core.Data;
using LYBox.Layout.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Core;
using TUnit.Assertions;

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

    [Test]
    public async Task GetValue_ReturnsDefault_WhenKeyNotExists()
    {
        var service = new SettingsService(_dbFactory);
        var result = service.GetValue<string>("nonexistent_key");
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task RegisterSetting_AndGetValue_RoundTrip()
    {
        var service = new SettingsService(_dbFactory);
        service.RegisterSetting(SettingDefinition.Text("test.key", "Test Key",
            defaultValue: "default_value", group: "Test Group"));

        var value = service.GetValue<string>("test.key");
        await Assert.That(value).IsEqualTo("default_value");
    }

    [Test]
    public async Task SetValue_UpdatesCache_WithoutFullReload()
    {
        var service = new SettingsService(_dbFactory);
        service.RegisterSetting(SettingDefinition.Text("test.update", "Update Test",
            defaultValue: "initial", group: "Test Group"));

        service.SetValue("test.update", "updated_value");

        var value = service.GetValue<string>("test.update");
        await Assert.That(value).IsEqualTo("updated_value");
    }

    [Test]
    public async Task InvalidateCache_AndGetValue_RebuildsFromDb()
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
        await Assert.That(value).IsEqualTo("db_modified_value");
    }

    [Test]
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

        foreach (var v in results) await Assert.That(v).IsEqualTo("value");
        await Assert.That(results.Count).IsEqualTo(50);
    }

    [Test]
    public async Task ConcurrentReadWithCacheInvalidation_DoesNotThrow()
    {
        var service = new SettingsService(_dbFactory);
        service.RegisterSetting(SettingDefinition.Text("base.key", "Base",
            defaultValue: "base_value", group: "Test Group"));

        // Warm up EF Core's internal service provider single-threaded before the
        // concurrent phase. The shared in-memory SQLite + DbContextFactory triggers
        // a first-time model/service-provider build that isn't thread-safe; building
        // it once here keeps the race from masking the actual cache-safety behaviour.
        _ = service.GetValue<string>("base.key");

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
        await Assert.That(exceptions).IsEmpty();
    }

    [Test]
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
        await Assert.That(createCount).IsEqualTo(1);
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
