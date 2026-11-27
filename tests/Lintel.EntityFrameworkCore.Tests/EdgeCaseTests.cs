using Lintel.EntityFrameworkCore.Utilities;
using Lintel.EntityFrameworkCore.Tests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Lintel.EntityFrameworkCore.Tests;

public sealed class EdgeCaseTests : IDisposable
{
    private readonly SqliteDatabase _database = new();

    [Fact]
    public async Task Interceptor_with_custom_delegates_audits_sync_and_async_saves()
    {
        var timestamp = new DateTime(2025, 12, 31, 23, 59, 0);
        var interceptor = new AuditSaveChangesInterceptor(() => timestamp, () => "robot");

        await using (var context = _database.CreateContext(interceptor))
        {
            context.Customers.Add(new Customer("sync"));
            context.SaveChanges();
            context.Customers.Add(new Customer("async"));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = _database.CreateContext())
        {
            context.Customers.Should().HaveCount(2).And.OnlyContain(c => c.DateCreated == timestamp && c.CreatedBy == "robot");
        }
    }

    [Fact]
    public void Interceptor_without_providers_uses_local_time_and_no_user()
    {
        using var context = _database.CreateContext(new AuditSaveChangesInterceptor());
        var before = DateTime.Now;

        context.Customers.Add(new Customer("default"));
        context.SaveChanges();

        context.Customers.Single().Should().Match<Customer>(c => c.DateCreated >= before && c.CreatedBy == null);
    }

    [Fact]
    public void Interceptor_ignores_events_without_context()
    {
        var interceptor = new AuditSaveChangesInterceptor();
        var result = InterceptionResult<int>.SuppressWithResult(1);

        interceptor.SavingChanges(new DbContextEventData(null!, (_, _) => "", null), result).Should().Be(result);
    }

    [Fact]
    public void TimeProvider_is_required()
    {
        var act = () => new AuditSaveChangesInterceptor((TimeProvider)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private interface IHandler<T>;

    private sealed class StringHandler : IHandler<string>;

    private class Base<T>;

    private sealed class Derived : Base<int>;

    [Fact]
    public void IsAssignableFromGenericType_supports_interfaces_and_base_classes()
    {
        typeof(IHandler<>).IsAssignableFromGenericType(typeof(StringHandler)).Should().BeTrue();
        typeof(Base<>).IsAssignableFromGenericType(typeof(Derived)).Should().BeTrue();
        typeof(Base<>).IsAssignableFromGenericType(typeof(Base<string>)).Should().BeTrue();
        typeof(IHandler<>).IsAssignableFromGenericType(typeof(Derived)).Should().BeFalse();
        typeof(Base<>).IsAssignableFromGenericType(typeof(StringHandler)).Should().BeFalse();
    }

    public void Dispose() => _database.Dispose();
}
