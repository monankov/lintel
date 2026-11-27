using Lintel.Domain;
using Lintel.EntityFrameworkCore.Tests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Lintel.EntityFrameworkCore.Tests;

public sealed class ModelBuilderExtensionsTests : IDisposable
{
    private readonly SqliteDatabase _database = new();
    private readonly TestDbContext _context;

    public ModelBuilderExtensionsTests() => _context = _database.CreateContext();

    private IEntityType EntityType<T>() => _context.Model.FindEntityType(typeof(T))!;

    [Fact]
    public void ApplyEntityConfiguration_uses_Id_as_key_of_root_types()
    {
        EntityType<Customer>().FindPrimaryKey()!.Properties.Select(p => p.Name).Should().Equal("Id");
        EntityType<Animal>().FindPrimaryKey()!.Properties.Select(p => p.Name).Should().Equal("Id");
        EntityType<Dog>().BaseType!.ClrType.Should().Be<Animal>();
        EntityType<Dog>().GetDeclaredKeys().Should().BeEmpty();
    }

    [Fact]
    public void ApplyEntityConfiguration_invokes_configure_for_every_entity_type()
    {
        var builder = new ModelBuilder(Microsoft.EntityFrameworkCore.Metadata.Conventions.SqliteConventionSetBuilder.Build());
        builder.Entity<Tag>();
        builder.Entity<Animal>();
        builder.Entity<Dog>();
        var configured = new List<string>();

        builder.ApplyEntityConfiguration(options => options.Configure = (entityType, _) => configured.Add(entityType.ClrType.Name));

        configured.Should().BeEquivalentTo([nameof(Tag), nameof(Animal), nameof(Dog)]);
    }

    [Fact]
    public void ApplyValueObjectsAsComplexTypes_maps_value_objects_with_nested_ones()
    {
        var address = EntityType<Customer>().FindComplexProperty(nameof(Customer.Address));

        address.Should().NotBeNull();
        address!.ComplexType.FindComplexProperty(nameof(Address.Location)).Should().NotBeNull();
        _context.Model.FindEntityType(typeof(Address)).Should().BeNull();
        _context.Model.FindEntityType(typeof(Location)).Should().BeNull();
    }

    [Fact]
    public void ApplyValueObjectsAsComplexTypes_leaves_owned_value_objects()
    {
        EntityType<Order>().FindComplexProperty(nameof(Order.Shipping)).Should().BeNull();
        EntityType<Order>().FindNavigation(nameof(Order.Shipping))!.TargetEntityType.IsOwned().Should().BeTrue();
    }

    [Fact]
    public void ApplyValueObjectConfiguration_configures_owned_value_objects()
    {
        var shipping = EntityType<Order>().FindNavigation(nameof(Order.Shipping))!.TargetEntityType;
        var table = StoreObjectIdentifier.Table(shipping.GetTableName()!, shipping.GetSchema());

        shipping.GetProperties().Where(p => !p.IsShadowProperty())
            .Select(p => p.GetColumnName(table))
            .Should().OnlyContain(name => name!.StartsWith("shipping_"));
    }

    [Fact]
    public void ApplyAuditableConfiguration_configures_audit_properties()
    {
        var customer = EntityType<Customer>();

        customer.FindProperty(AuditableProperties.DateCreated)!.IsNullable.Should().BeFalse();
        customer.FindProperty(AuditableProperties.DateModified)!.IsNullable.Should().BeFalse();
        customer.FindProperty(AuditableProperties.DateDeleted)!.IsNullable.Should().BeTrue();
        foreach (var name in new[] { AuditableProperties.CreatedBy, AuditableProperties.ModifiedBy, AuditableProperties.DeletedBy })
        {
            var property = customer.FindProperty(name)!;
            property.IsNullable.Should().BeTrue();
            property.GetMaxLength().Should().Be(100);
        }
    }

    [Fact]
    public void ApplyAuditableConfiguration_adds_named_filter_to_root_types_only()
    {
        EntityType<Customer>().FindDeclaredQueryFilter(SoftDeleteQueryFilter.Name).Should().NotBeNull();
        EntityType<Customer>().FindDeclaredQueryFilter("NotBlocked").Should().NotBeNull();
        EntityType<Animal>().FindDeclaredQueryFilter(SoftDeleteQueryFilter.Name).Should().NotBeNull();
        EntityType<Dog>().GetDeclaredQueryFilters().Should().BeEmpty();
        EntityType<Tag>().GetDeclaredQueryFilters().Should().BeEmpty();
    }

    [Fact]
    public void ApplyAuditableConfiguration_can_skip_filter()
    {
        var builder = new ModelBuilder();
        builder.Entity<Customer>();

        builder.ApplyAuditableConfiguration(options => options.AddSoftDeleteQueryFilter = false);

        builder.Model.FindEntityType(typeof(Customer))!.GetDeclaredQueryFilters().Should().BeEmpty();
    }

    [Fact]
    public void GetEntityTypes_finds_types_by_open_generic_and_interface()
    {
        var builder = new ModelBuilder();
        builder.Entity<Order>().OwnsOne(order => order.Shipping);
        builder.Entity<Tag>();

        builder.GetEntityTypes(typeof(Entity<>)).Select(t => t.ClrType).Should().BeEquivalentTo([typeof(Order), typeof(Tag)]);
        builder.GetEntityTypes(typeof(ValueObject<>)).Select(t => t.ClrType).Should().Equal(typeof(Shipping));
        builder.GetEntityTypes(typeof(ISoftDeletable)).Select(t => t.ClrType).Should().Equal(typeof(Order));
        builder.GetEntityTypes(typeof(Entity<>), t => t.ClrType == typeof(Tag)).Should().ContainSingle();
    }

    [Fact]
    public void GetEntityTypes_validates_arguments()
    {
        var withoutBuilder = () => ((ModelBuilder)null!).GetEntityTypes(typeof(Entity<>));
        var withoutType = () => new ModelBuilder().GetEntityTypes(null!);

        withoutBuilder.Should().ThrowExactly<ArgumentNullException>();
        withoutType.Should().ThrowExactly<ArgumentNullException>();
    }

    public void Dispose()
    {
        _context.Dispose();
        _database.Dispose();
    }
}
