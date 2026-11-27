using Lintel.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Lintel.EntityFrameworkCore.Tests.TestUtils;

public class Location : ValueObject<Location>
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class Address : ValueObject<Address>
{
    public string City { get; set; } = "";
    public string Street { get; set; } = "";
    public Location Location { get; set; } = new();
}

public class Shipping : ValueObject<Shipping>
{
    public string Carrier { get; set; } = "";
    public string TrackingNumber { get; set; } = "";
}

/// <summary>Signed auditable, soft-deletable, with a value object mapped as a complex type.</summary>
public class Customer : SignedAuditableEntity<Customer, int>, ISignedSoftDeletable
{
    public string Name { get; private set; } = "";
    public bool IsBlocked { get; set; }
    public Address Address { get; set; } = new();
    public DateTime? DateDeleted { get; private set; }
    public string? DeletedBy { get; private set; }

    private Customer()
    {
    }

    public Customer(string name) => Name = name;

    public void Rename(string name) => Name = name;
}

/// <summary>Auditable, soft-deletable, with a value object mapped as an owned type.</summary>
public class Order : AuditableEntity<Order, int>, ISoftDeletable
{
    public string Number { get; private set; } = "";
    public Shipping Shipping { get; set; } = new();
    public DateTime? DateDeleted { get; private set; }

    private Order()
    {
    }

    public Order(string number) => Number = number;
}

/// <summary>Never deleted, without soft-delete columns.</summary>
public class Archive : Entity<Archive, int>, IUndeletable
{
    public string Title { get; set; } = "";
}

/// <summary>Not audited at all.</summary>
public class Tag : Entity<Tag, int>
{
    public string Name { get; set; } = "";
}

/// <summary>Root of a TPH hierarchy.</summary>
public class Animal : AuditableEntity<Animal, int>, ISoftDeletable
{
    public string Name { get; set; } = "";
    public DateTime? DateDeleted { get; private set; }
}

public class Dog : Animal
{
    public string Breed { get; set; } = "";
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Archive> Archives => Set<Archive>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Animal> Animals => Set<Animal>();
    public DbSet<Dog> Dogs => Set<Dog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>().OwnsOne(order => order.Shipping);
        modelBuilder.Entity<Customer>().HasQueryFilter("NotBlocked", customer => !customer.IsBlocked);

        modelBuilder.ApplyEntityConfiguration();

        modelBuilder.ApplyValueObjectsAsComplexTypes();

        modelBuilder.ApplyValueObjectConfiguration(options => options.Configure = (entityType, _, _) =>
        {
            foreach (var property in entityType.GetProperties().Where(p => !p.IsShadowProperty()))
            {
                property.SetColumnName("shipping_" + property.Name);
            }
        });

        modelBuilder.ApplyAuditableConfiguration(options =>
        {
            options.SignedColumnIsRequired = false;
            options.SignedColumnMaxLength = 100;
        });
    }
}

/// <summary>A fresh SQLite in-memory database per test.</summary>
public sealed class SqliteDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqliteDatabase()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public TestDbContext CreateContext(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors)
        => new(new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptors)
            .Options);

    public void Dispose() => _connection.Dispose();
}

public sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
