using System.Linq.Expressions;
using Lintel.EntityFrameworkCore.PostgreSql.Tests.TestUtils;
using Lintel.Primitives;
using Lintel.Querying;
using Lintel.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Lintel.EntityFrameworkCore.PostgreSql.Tests;

/// <summary>Lintel query building blocks translated to PostgreSQL SQL.</summary>
public sealed class QueryTranslationTests(PostgresFixture postgres)
{
    private CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly string[] _cities = ["Omsk", "Kazan", "Moscow", "Perm", "Sochi"];

    private async Task<TestDbContext> SeededAsync()
    {
        var context = await postgres.CreateDatabaseAsync(new AuditSaveChangesInterceptor(TimeProvider.System));
        context.Customers.AddRange(Enumerable.Range(1, 25).Select(i => new Customer($"Customer {i:00}")
        {
            Rating = i % 5,
            Address = new Address { City = _cities[i % _cities.Length], Street = $"Street {i}", Location = new Location { Latitude = i } },
        }));
        await context.SaveChangesAsync(Token);
        return PostgresFixture.Reopen(context);
    }

    private sealed class RatingAtLeast(int rating) : Specification<Customer>
    {
        public override Expression<Func<Customer, bool>> ToExpression() => c => c.Rating >= rating;
    }

    private sealed class LivesIn(string city) : Specification<Customer>
    {
        public override Expression<Func<Customer, bool>> ToExpression() => c => c.Address.City == city;
    }

    [Fact]
    public async Task ToPagedListAsync_uses_count_and_limit_offset()
    {
        await using var context = await SeededAsync();
        var query = context.Customers.OrderBy(c => c.Name);

        var page = await query.ToPagedListAsync(3, 10, Token);

        query.Skip(20).Take(10).ToQueryString().Should().Contain("LIMIT").And.Contain("OFFSET");
        page.Select(c => c.Name).Should().Equal("Customer 21", "Customer 22", "Customer 23", "Customer 24", "Customer 25");
        page.TotalItems.Should().Be(25);
        page.TotalPages.Should().Be(3);
        page.IsLastPage.Should().BeTrue();
    }

    [Fact]
    public async Task Composed_specifications_are_translated_to_a_single_where()
    {
        await using var context = await SeededAsync();
        var specification = new RatingAtLeast(3) & !new LivesIn("Moscow");

        var query = context.Customers.Where(specification);
        var customers = await query.ToListAsync(Token);

        query.ToQueryString().Should().Contain("WHERE NOT (c.\"IsBlocked\") AND c.\"DateDeleted\" IS NULL AND c.\"Rating\" >= @rating AND c.\"Address_City\" <> @city");
        customers.Should().NotBeEmpty().And.OnlyContain(c => c.Rating >= 3 && c.Address.City != "Moscow");
        customers.Should().HaveCount(context.Customers.AsEnumerable().Count(c => c.Rating >= 3 && c.Address.City != "Moscow"));
    }

    [Fact]
    public async Task Sorting_by_property_name_works_on_complex_type_paths()
    {
        await using var context = await SeededAsync();

        var byCity = context.Customers.OrderBy("address.city").ThenBy("Name");
        var bySort = context.Customers.OrderBy(new Dictionary<string, SortOrder> { ["Rating"] = SortOrder.Desc, ["Name"] = SortOrder.Asc });

        byCity.ToQueryString().Should().Contain("ORDER BY c.\"Address_City\"");
        (await byCity.Select(c => c.Address.City).Take(5).ToListAsync(Token)).Should().Equal("Kazan", "Kazan", "Kazan", "Kazan", "Kazan");
        (await bySort.Select(c => c.Name).FirstAsync(Token)).Should().Be("Customer 04");
    }

    [Fact]
    public async Task Changing_complex_value_object_updates_columns_and_modification_date()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));
        await using var context = await postgres.CreateDatabaseAsync(new AuditSaveChangesInterceptor(clock));
        context.Customers.Add(new Customer("Foo") { Address = new Address { City = "Omsk", Location = new Location { Latitude = 55 } } });
        await context.SaveChangesAsync(Token);

        clock.Now = clock.Now.AddDays(1);
        await using (var edit = PostgresFixture.Reopen(context, new AuditSaveChangesInterceptor(clock)))
        {
            var customer = await edit.Customers.SingleAsync(Token);
            customer.Address = new Address { City = "Kazan", Street = "Bauman", Location = new Location { Latitude = 55.79 } };
            await edit.SaveChangesAsync(Token);
        }

        await using var read = PostgresFixture.Reopen(context);
        var saved = await read.Customers.SingleAsync(c => c.Address.City == "Kazan", Token);
        saved.Address.Should().Be(new Address { City = "Kazan", Street = "Bauman", Location = new Location { Latitude = 55.79 } });
        saved.DateModified.Should().Be(clock.Now.UtcDateTime);
    }

    [Fact]
    public async Task Sequential_guids_of_string_type_are_ordered_in_postgres_uuid_order()
    {
        // PostgreSQL compares uuid values byte by byte, the same order as their string form.
        await using var context = await postgres.CreateDatabaseAsync();
        var guids = new SequentialGuidProvider(Guid.NewGuid, new UniqueSequentialGuidTimestampProvider().GetTimestamp);

        var tags = Enumerable.Range(1, 50).Select(i => new Tag(guids.GetSequentialGuid(SequentialGuidType.String), i)).ToList();
        context.Tags.AddRange(tags.OrderBy(_ => Random.Shared.Next()));
        await context.SaveChangesAsync(Token);

        var sequences = await context.Tags.OrderBy(t => t.Id).Select(t => t.Sequence).ToListAsync(Token);

        sequences.Should().Equal(Enumerable.Range(1, 50));
    }
}
