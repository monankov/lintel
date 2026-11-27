using Lintel.EntityFrameworkCore.PostgreSql.Tests.TestUtils;
using Microsoft.EntityFrameworkCore;

namespace Lintel.EntityFrameworkCore.PostgreSql.Tests;

public sealed class AuditAndSoftDeleteTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset _created = new(2026, 3, 1, 8, 30, 0, TimeSpan.Zero);

    private readonly ManualTimeProvider _clock = new(_created);
    private string? _user = "editor@shop.test";

    private CancellationToken Token => TestContext.Current.CancellationToken;

    private AuditSaveChangesInterceptor Interceptor() => new(_clock, () => _user);

    [Fact]
    public async Task Utc_audit_timestamps_round_trip_through_timestamptz()
    {
        await using var context = await postgres.CreateDatabaseAsync(Interceptor());
        context.Customers.Add(new Customer("Foo"));
        await context.SaveChangesAsync(Token);

        _clock.Now = _created.AddHours(3);
        _user = "admin@shop.test";
        await using (var edit = PostgresFixture.Reopen(context, Interceptor()))
        {
            (await edit.Customers.SingleAsync(Token)).Rename("Bar");
            await edit.SaveChangesAsync(Token);
        }

        await using var read = PostgresFixture.Reopen(context);
        var customer = await read.Customers.SingleAsync(Token);
        customer.DateCreated.Should().Be(_created.UtcDateTime);
        customer.DateCreated.Kind.Should().Be(DateTimeKind.Utc);
        customer.CreatedBy.Should().Be("editor@shop.test");
        customer.DateModified.Should().Be(_created.AddHours(3).UtcDateTime);
        customer.ModifiedBy.Should().Be("admin@shop.test");
    }

    [Fact]
    public async Task Local_time_audit_is_rejected_by_npgsql_for_timestamptz_columns()
    {
        // The default timestamp (DateTime.Now, as in Structr) has Kind=Local. Npgsql maps DateTime to
        // "timestamp with time zone" and refuses non-UTC values: on PostgreSQL use the TimeProvider constructor.
        await using var context = await postgres.CreateDatabaseAsync(new AuditSaveChangesInterceptor());
        context.Customers.Add(new Customer("Foo"));

        var act = () => context.SaveChangesAsync(Token);

        (await act.Should().ThrowAsync<Exception>()).Which.ToString().Should().Contain("Kind=Local");
    }

    [Fact]
    public async Task Soft_delete_keeps_row_and_named_filters_are_translated_to_sql()
    {
        await using var context = await postgres.CreateDatabaseAsync(Interceptor());
        context.Customers.AddRange(new Customer("Visible"), new Customer("Deleted"), new Customer("Blocked") { IsBlocked = true });
        await context.SaveChangesAsync(Token);

        context.Customers.Remove(await context.Customers.SingleAsync(c => c.Name == "Deleted", Token));
        await context.SaveChangesAsync(Token);

        await using var read = PostgresFixture.Reopen(context);
        var all = WhereClause(read.Customers.ToQueryString());
        var withDeleted = WhereClause(read.Customers.IncludeSoftDeleted().ToQueryString());

        all.Should().Contain("\"DateDeleted\" IS NULL").And.Contain("\"IsBlocked\"");
        withDeleted.Should().NotContain("\"DateDeleted\"").And.Contain("\"IsBlocked\"");

        (await read.Customers.Select(c => c.Name).ToListAsync(Token)).Should().Equal("Visible");
        (await read.Customers.IncludeSoftDeleted().OrderBy(c => c.Name).Select(c => c.Name).ToListAsync(Token))
            .Should().Equal("Deleted", "Visible");
        (await read.Customers.IgnoreQueryFilters().CountAsync(Token)).Should().Be(3);

        var deleted = await read.Customers.IncludeSoftDeleted().SingleAsync(c => c.Name == "Deleted", Token);
        deleted.DateDeleted.Should().Be(_created.UtcDateTime);
        deleted.DeletedBy.Should().Be("editor@shop.test");
    }

    private static string WhereClause(string sql) => sql[sql.IndexOf("WHERE", StringComparison.Ordinal)..];

    [Fact]
    public async Task Soft_deleted_order_keeps_owned_columns()
    {
        await using var context = await postgres.CreateDatabaseAsync(Interceptor());
        context.Orders.Add(new Order("A-1") { Shipping = new Shipping { Carrier = "DHL", TrackingNumber = "JD0001" } });
        await context.SaveChangesAsync(Token);

        context.Orders.Remove(await context.Orders.SingleAsync(Token));
        await context.SaveChangesAsync(Token);

        await using var read = PostgresFixture.Reopen(context);
        var order = await read.Orders.IncludeSoftDeleted().SingleAsync(Token);
        order.DateDeleted.Should().NotBeNull();
        order.Shipping.Should().Be(new Shipping { Carrier = "DHL", TrackingNumber = "JD0001" });
    }

    [Fact]
    public async Task Soft_delete_of_derived_type_uses_filter_of_hierarchy_root()
    {
        await using var context = await postgres.CreateDatabaseAsync(Interceptor());
        context.Animals.AddRange(new Dog { Name = "Rex", Breed = "Husky" }, new Animal { Name = "Generic" });
        await context.SaveChangesAsync(Token);

        context.Dogs.Remove(await context.Dogs.SingleAsync(Token));
        await context.SaveChangesAsync(Token);

        await using var read = PostgresFixture.Reopen(context);
        (await read.Animals.Select(a => a.Name).ToListAsync(Token)).Should().Equal("Generic");
        (await read.Dogs.IncludeSoftDeleted().SingleAsync(Token)).DateDeleted.Should().NotBeNull();
    }
}
