using Lintel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Lintel.EntityFrameworkCore.PostgreSql.Tests.TestUtils;

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

public class Customer : SignedAuditableEntity<Customer, int>, ISignedSoftDeletable
{
    public string Name { get; private set; } = "";
    public bool IsBlocked { get; set; }
    public int Rating { get; set; }
    public Address Address { get; set; } = new();
    public DateTime? DateDeleted { get; private set; }
    public string? DeletedBy { get; private set; }

    private Customer()
    {
    }

    public Customer(string name) => Name = name;

    public void Rename(string name) => Name = name;
}

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

public class Animal : AuditableEntity<Animal, int>, ISoftDeletable
{
    public string Name { get; set; } = "";
    public DateTime? DateDeleted { get; private set; }
}

public class Dog : Animal
{
    public string Breed { get; set; } = "";
}

/// <summary>Keyed by a GUID that the application generates.</summary>
public class Tag : Entity<Tag, Guid>
{
    public int Sequence { get; private set; }

    private Tag()
    {
    }

    public Tag(Guid id, int sequence)
    {
        Id = id;
        Sequence = sequence;
    }
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Animal> Animals => Set<Animal>();
    public DbSet<Dog> Dogs => Set<Dog>();
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>().OwnsOne(order => order.Shipping);
        modelBuilder.Entity<Customer>().HasQueryFilter("NotBlocked", customer => !customer.IsBlocked);
        modelBuilder.Entity<Tag>().Property(tag => tag.Id).ValueGeneratedNever();

        modelBuilder.ApplyEntityConfiguration();
        modelBuilder.ApplyValueObjectsAsComplexTypes();
        modelBuilder.ApplyAuditableConfiguration();
    }
}

public sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
