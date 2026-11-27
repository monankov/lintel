using Lintel.Collections;
using Lintel.EntityFrameworkCore.Tests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.EntityFrameworkCore.Tests;

public sealed class QueryableExtensionsTests : IDisposable
{
    private readonly SqliteDatabase _database = new();

    private CancellationToken Token => TestContext.Current.CancellationToken;

    private TestDbContext SeededContext(int count)
    {
        var context = _database.CreateContext();
        context.Tags.AddRange(Enumerable.Range(1, count).Select(i => new Tag { Name = $"tag{i:00}" }));
        context.SaveChanges();
        return context;
    }

    [Theory]
    [InlineData(1, 3, new[] { "tag01", "tag02", "tag03" })]
    [InlineData(4, 3, new[] { "tag10" })]
    [InlineData(5, 3, new string[0])]
    public async Task ToPagedListAsync_returns_requested_page(int pageNumber, int pageSize, string[] expected)
    {
        await using var context = SeededContext(10);

        var page = await context.Tags.OrderBy(t => t.Name).Select(t => t.Name).ToPagedListAsync(pageNumber, pageSize, Token);

        page.Should().Equal(expected);
        page.TotalItems.Should().Be(10);
        page.TotalPages.Should().Be(4);
        page.PageNumber.Should().Be(pageNumber);
    }

    [Fact]
    public async Task ToPagedListAsync_with_non_positive_page_size_returns_all()
    {
        await using var context = SeededContext(5);

        var page = await context.Tags.ToPagedListAsync(1, 0, Token);

        page.Should().HaveCount(5);
        page.PageSize.Should().Be(5);
    }

    [Fact]
    public async Task ToPagedListAsync_of_empty_query_returns_empty_page()
    {
        await using var context = SeededContext(0);

        var page = await context.Tags.ToPagedListAsync(3, 10, Token);

        page.Should().BeEquivalentTo(PagedList.Empty<Tag>());
    }

    [Fact]
    public async Task ToPagedListAsync_validates_arguments()
    {
        await using var context = SeededContext(1);

        var withoutSource = () => ((IQueryable<Tag>)null!).ToPagedListAsync(1, 10, Token);
        var withBadPage = () => context.Tags.ToPagedListAsync(0, 10, Token);

        await withoutSource.Should().ThrowAsync<ArgumentNullException>();
        await withBadPage.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    public interface IClock;

    private sealed class Clock : IClock;

    [Fact]
    public void GetService_resolves_application_services_from_options()
    {
        var services = new ServiceCollection()
            .AddSingleton<IClock, Clock>()
            .AddDbContext<TestDbContext>(options => options.UseSqlite("DataSource=:memory:"))
            .BuildServiceProvider();

        var options = services.GetRequiredService<DbContextOptions<TestDbContext>>();

        options.GetService<IClock>().Should().BeOfType<Clock>();
        options.GetService<IDisposable>().Should().BeNull();
        new DbContextOptionsBuilder<TestDbContext>().Options.GetService<IClock>().Should().BeNull();
    }

    public void Dispose() => _database.Dispose();
}
