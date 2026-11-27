using Lintel.EntityFrameworkCore.PostgreSql.Tests.TestUtils;
using Microsoft.EntityFrameworkCore;

namespace Lintel.EntityFrameworkCore.PostgreSql.Tests;

/// <summary>The tests run on the PostgreSQL version that applications use.</summary>
public sealed class ServerVersionTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Server_is_PostgreSQL_18_or_later()
    {
        await using var context = await postgres.CreateDatabaseAsync();

        var version = await context.Database.SqlQueryRaw<int>("SELECT current_setting('server_version_num')::int AS \"Value\"")
            .SingleAsync(TestContext.Current.CancellationToken);

        version.Should().BeGreaterThanOrEqualTo(180000);
    }
}
