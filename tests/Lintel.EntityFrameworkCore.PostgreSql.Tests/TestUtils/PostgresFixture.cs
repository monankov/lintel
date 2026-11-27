using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Lintel.EntityFrameworkCore.PostgreSql.Tests.TestUtils.PostgresFixture))]

namespace Lintel.EntityFrameworkCore.PostgreSql.Tests.TestUtils;

/// <summary>
/// One PostgreSQL container for all tests of the assembly; every test works in its own database.
/// </summary>
/// <remarks>
/// Without Docker the tests are skipped, unless the environment variable <c>LINTEL_REQUIRE_DOCKER=true</c> is set
/// (as in CI), in which case they fail.
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _unavailableReason;

    public async ValueTask InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder("postgres:18-alpine").Build();
            await _container.StartAsync();
        }
        catch (Exception ex) when (ex is DockerUnavailableException or HttpRequestException or TimeoutException)
        {
            _unavailableReason = $"Docker is not available: {ex.GetBaseException().Message}";
            _container = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>Creates a context on a new, empty database with the schema created.</summary>
    public async Task<TestDbContext> CreateDatabaseAsync(params IInterceptor[] interceptors)
    {
        var connectionString = ConnectionStringForNewDatabase();
        await using (var context = CreateContext(connectionString))
        {
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        }
        return CreateContext(connectionString, interceptors);
    }

    /// <summary>Creates another context on the database of <paramref name="existing"/> (as a new request would).</summary>
    public static TestDbContext Reopen(TestDbContext existing, params IInterceptor[] interceptors)
        => CreateContext(existing.Database.GetConnectionString()!, interceptors);

    private string ConnectionStringForNewDatabase()
    {
        if (_container is null)
        {
            if (string.Equals(Environment.GetEnvironmentVariable("LINTEL_REQUIRE_DOCKER"), "true", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(_unavailableReason);
            }
            Assert.Skip(_unavailableReason ?? "Docker is not available.");
        }

        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = "lintel_" + Guid.NewGuid().ToString("N"),
        }.ConnectionString;
    }

    private static TestDbContext CreateContext(string connectionString, params IInterceptor[] interceptors)
        => new(new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(interceptors)
            .Options);
}
