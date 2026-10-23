using LinkShortener.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace LinkShortener.Tests.Integration;

public class LinkShortenerFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"links-tests-{Guid.NewGuid():N}.db");

    public ManualTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));

    public SelectCommandCounter Selects { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_databasePath}");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(Selects));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

        return host;
    }

    public async Task<T> QueryDatabaseAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }
}
