using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LYBox.Layout.Core.Data;

public sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var dbPath = Path.Combine(AppContext.BaseDirectory, "appdata.db");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        return new AppDbContext(options);
    }
}
