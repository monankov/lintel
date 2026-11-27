using System.Linq.Expressions;

namespace Lintel.Domain.Tests.TestUtils;

public enum RoleId
{
    User,
    Admin,
}

public class Role : Entity<Role, RoleId>
{
    public string Name { get; private set; } = "";

    protected Role()
    {
    }

    public Role(RoleId id, string name) : this()
    {
        Id = id;
        Name = name;
    }
}

public class Address : ValueObject<Address>
{
    public string? City { get; set; }
    public string? Street { get; set; }
    public string? House { get; set; }
}

public class User : Entity<User, int>
{
    public string FullName { get; private set; } = "";
    public Address Address { get; private set; } = new();

    protected User()
    {
    }

    public User(int id, string fullName) : this()
    {
        Id = id;
        FullName = fullName;
    }

    public void AssignId(int id) => Id = id;
}

/// <summary>Imitates an EF Core lazy-loading proxy.</summary>
public class UserProxy : User
{
    public UserProxy(int id) : base(id, "Proxy")
    {
    }
}

public class Document : Entity<Document, Guid>
{
    public Document(Guid id) => Id = id;
}

public class Tag : Entity<Tag, string?>
{
    public Tag(string? id) => Id = id;
}

public class UserRole : CompositeEntity<UserRole>
{
    private static readonly Func<UserRole, object?> _compositeId = CompositeId.Compile();

    public int UserId { get; private set; }
    public RoleId RoleId { get; private set; }

    public UserRole(User user, Role role)
    {
        UserId = user.Id;
        RoleId = role.Id;
    }

    public static Expression<Func<UserRole, object?>> CompositeId => x => new { x.UserId, x.RoleId };

    protected override object? GetCompositeId() => _compositeId(this);
}

public class NullKeyEntity : CompositeEntity<NullKeyEntity>
{
    protected override object? GetCompositeId() => null;
}

// Wrong TEntity generic argument.
public class Item : Entity<User>
{
    public override bool Equals(User? other) => throw new NotSupportedException();

    public override bool IsTransient() => throw new NotSupportedException();

    protected override int GenerateHashCode() => throw new NotSupportedException();
}
