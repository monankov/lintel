using Lintel.EntityFrameworkCore.Tests.TestUtils;
using Microsoft.EntityFrameworkCore;

namespace Lintel.EntityFrameworkCore.Tests;

public sealed class AuditTests : IDisposable
{
    private static readonly DateTimeOffset _created = new(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);

    private readonly SqliteDatabase _database = new();
    private readonly ManualTimeProvider _clock = new(_created);
    private string? _user = "peter@parker.name";

    private TestDbContext CreateContext() => _database.CreateContext(new AuditSaveChangesInterceptor(_clock, () => _user));

    private CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void Audit_extension_sets_creation_modification_and_deletion()
    {
        using var context = _database.CreateContext();
        var customer = new Customer("Foo 1");
        context.Customers.Add(customer);

        var dateCreated = new DateTime(2026, 1, 1, 10, 0, 0);
        context.Audit(() => dateCreated, () => "creator");

        customer.DateCreated.Should().Be(dateCreated);
        customer.CreatedBy.Should().Be("creator");
        customer.DateModified.Should().Be(dateCreated);
        customer.ModifiedBy.Should().Be("creator");
        context.SaveChanges();

        customer.Rename("Foo 2");
        var dateModified = dateCreated.AddHours(1);
        context.Audit(() => dateModified, () => "editor");

        customer.DateModified.Should().Be(dateModified);
        customer.ModifiedBy.Should().Be("editor");
        customer.CreatedBy.Should().Be("creator");

        context.Customers.Remove(customer);
        var dateDeleted = dateCreated.AddHours(2);
        context.Audit(() => dateDeleted, () => "remover");

        customer.DateDeleted.Should().Be(dateDeleted);
        customer.DeletedBy.Should().Be("remover");
        context.Entry(customer).State.Should().Be(EntityState.Modified);
    }

    [Fact]
    public void Audit_without_providers_uses_local_time()
    {
        using var context = _database.CreateContext();
        var customer = new Customer("Foo");
        context.Customers.Add(customer);

        var before = DateTime.Now;
        context.Audit();

        customer.DateCreated.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.Now);
        customer.CreatedBy.Should().BeNull();
    }

    [Fact]
    public async Task Interceptor_audits_on_save_with_utc_time()
    {
        await using (var context = CreateContext())
        {
            context.Customers.Add(new Customer("Foo"));
            await context.SaveChangesAsync(Token);
        }

        await using (var context = CreateContext())
        {
            var customer = await context.Customers.SingleAsync(Token);
            customer.DateCreated.Should().Be(_created.UtcDateTime);
            customer.CreatedBy.Should().Be("peter@parker.name");
            customer.DateModified.Should().Be(_created.UtcDateTime);
        }
    }

    [Fact]
    public void Interceptor_updates_modification_and_protects_creation()
    {
        using (var context = CreateContext())
        {
            context.Customers.Add(new Customer("Foo"));
            context.SaveChanges();
        }

        _clock.Now = _created.AddDays(1);
        _user = "editor";
        using (var context = CreateContext())
        {
            var customer = context.Customers.Single();
            customer.Rename("Bar");
            context.Entry(customer).Property(nameof(Customer.DateCreated)).CurrentValue = DateTime.MinValue;
            context.SaveChanges();
        }

        using (var context = CreateContext())
        {
            var customer = context.Customers.Single();
            customer.Name.Should().Be("Bar");
            customer.DateCreated.Should().Be(_created.UtcDateTime);
            customer.CreatedBy.Should().Be("peter@parker.name");
            customer.DateModified.Should().Be(_created.AddDays(1).UtcDateTime);
            customer.ModifiedBy.Should().Be("editor");
        }
    }

    [Fact]
    public void Changing_complex_value_object_marks_entity_modified()
    {
        using (var context = CreateContext())
        {
            context.Customers.Add(new Customer("Foo") { Address = new Address { City = "Omsk" } });
            context.SaveChanges();
        }

        _clock.Now = _created.AddHours(5);
        using (var context = CreateContext())
        {
            var customer = context.Customers.Single();
            customer.Address = new Address { City = "Kazan", Street = customer.Address.Street, Location = customer.Address.Location };
            context.SaveChanges();
        }

        using (var context = CreateContext())
        {
            var customer = context.Customers.Single();
            customer.Address.City.Should().Be("Kazan");
            customer.DateModified.Should().Be(_created.AddHours(5).UtcDateTime);
        }
    }

    [Fact]
    public void Soft_deleted_entity_is_kept_and_hidden_by_named_filter()
    {
        using (var context = CreateContext())
        {
            context.Customers.Add(new Customer("Foo"));
            context.SaveChanges();
        }

        _clock.Now = _created.AddDays(2);
        _user = "remover";
        using (var context = CreateContext())
        {
            context.Customers.Remove(context.Customers.Single());
            context.SaveChanges();
        }

        using (var context = CreateContext())
        {
            context.Customers.Should().BeEmpty();

            var deleted = context.Customers.IncludeSoftDeleted().Single();
            deleted.DateDeleted.Should().Be(_created.AddDays(2).UtcDateTime);
            deleted.DeletedBy.Should().Be("remover");
        }
    }

    [Fact]
    public void IncludeSoftDeleted_keeps_other_named_filters()
    {
        using (var context = CreateContext())
        {
            context.Customers.Add(new Customer("Visible"));
            context.Customers.Add(new Customer("Blocked") { IsBlocked = true });
            context.SaveChanges();
            context.Customers.Remove(context.Customers.Single(c => c.Name == "Visible"));
            context.SaveChanges();
        }

        using (var context = CreateContext())
        {
            context.Customers.IncludeSoftDeleted().Select(c => c.Name).Should().Equal("Visible");
            context.Customers.IgnoreQueryFilters().Should().HaveCount(2);
        }
    }

    [Fact]
    public void Soft_deleted_entity_keeps_owned_value_objects()
    {
        using (var context = CreateContext())
        {
            context.Orders.Add(new Order("A-1") { Shipping = new Shipping { Carrier = "DHL", TrackingNumber = "123" } });
            context.SaveChanges();
        }

        using (var context = CreateContext())
        {
            context.Orders.Remove(context.Orders.Single());
            context.SaveChanges();
        }

        using (var context = CreateContext())
        {
            var order = context.Orders.IncludeSoftDeleted().Single();
            order.DateDeleted.Should().NotBeNull();
            order.Shipping.Should().Be(new Shipping { Carrier = "DHL", TrackingNumber = "123" });
        }
    }

    [Fact]
    public void Undeletable_entity_is_not_deleted()
    {
        using (var context = CreateContext())
        {
            context.Archives.Add(new Archive { Title = "Keep" });
            context.SaveChanges();
            context.Archives.Remove(context.Archives.Single());
            context.SaveChanges();
        }

        using (var context = CreateContext())
        {
            context.Archives.Single().Title.Should().Be("Keep");
        }
    }

    [Fact]
    public void Non_auditable_entity_is_deleted()
    {
        using (var context = CreateContext())
        {
            context.Tags.Add(new Tag { Name = "x" });
            context.SaveChanges();
            context.Tags.Remove(context.Tags.Single());
            context.SaveChanges();
        }

        using (var context = CreateContext())
        {
            context.Tags.IgnoreQueryFilters().Should().BeEmpty();
        }
    }

    [Fact]
    public void Soft_delete_works_for_derived_types_of_hierarchy()
    {
        using (var context = CreateContext())
        {
            context.Dogs.Add(new Dog { Name = "Rex", Breed = "Husky" });
            context.Animals.Add(new Animal { Name = "Generic" });
            context.SaveChanges();
            context.Dogs.Remove(context.Dogs.Single());
            context.SaveChanges();
        }

        using (var context = CreateContext())
        {
            context.Animals.Select(a => a.Name).Should().Equal("Generic");
            context.Dogs.IncludeSoftDeleted().Single().DateDeleted.Should().NotBeNull();
        }
    }

    public void Dispose() => _database.Dispose();
}
