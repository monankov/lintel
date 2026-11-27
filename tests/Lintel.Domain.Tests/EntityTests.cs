using Lintel.Domain.Tests.TestUtils;

namespace Lintel.Domain.Tests;

public class EntityTests
{
    [Fact]
    public void Ctor_throws_when_wrong_generic_argument_is_specified()
    {
        var act = () => new Item();

        act.Should().ThrowExactly<InvalidOperationException>()
            .WithMessage("Entity \"*Item\" specifies \"User\" as generic argument, but it should be its own type.");
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void IsTransient(int id, bool expected)
        => new User(id, "Ivanov I.I.").IsTransient().Should().Be(expected);

    [Fact]
    public void IsTransient_for_reference_and_guid_keys()
    {
        new Tag(null).IsTransient().Should().BeTrue();
        new Tag("x").IsTransient().Should().BeFalse();
        new Document(Guid.Empty).IsTransient().Should().BeTrue();
        new Document(Guid.NewGuid()).IsTransient().Should().BeFalse();
    }

    public static TheoryData<User?, User?, bool> EqualityData()
    {
        var user1 = new User(1, "Ivanov I.I.");
        var transient = new User(0, "Ivanov I.I.");
        return new()
        {
            { user1, user1, true },
            { user1, new User(1, "Petrov I.I."), true },
            { user1, new User(3, "Ivanov I.I."), false },
            { user1, null, false },
            { transient, transient, true },
            { transient, new User(0, "Ivanov I.I."), false },
            { transient, new User(1, "Ivanov I.I."), false },
        };
    }

    [Theory]
    [MemberData(nameof(EqualityData))]
    public void Equals_compares_by_identity(User? left, User? right, bool expected)
    {
        left!.Equals(right).Should().Be(expected);
        left.Equals((object?)right).Should().Be(expected);
        (left == right).Should().Be(expected);
        (left != right).Should().Be(!expected);
    }

    [Fact]
    public void Operators_handle_nulls()
    {
        User? none = null;
        var user = new User(1, "Ivanov I.I.");

        (none == null).Should().BeTrue();
        (none == user).Should().BeFalse();
        (user == none).Should().BeFalse();
        (none != user).Should().BeTrue();
    }

    [Fact]
    public void Equals_with_other_types_is_false()
    {
        var user = new User(1, "Ivanov I.I.");

        user.Equals(new Role(RoleId.Admin, "Admin")).Should().BeFalse();
        user.Equals("User [Id=1]").Should().BeFalse();
    }

    [Fact]
    public void Entity_and_its_proxy_are_equal_symmetrically_with_same_hash_code()
    {
        var user = new User(5, "Ivanov I.I.");
        var proxy = new UserProxy(5);

        user.Equals(proxy).Should().BeTrue();
        proxy.Equals(user).Should().BeTrue();
        user.GetHashCode().Should().Be(proxy.GetHashCode());
    }

    [Fact]
    public void Hash_code_depends_on_identity()
    {
        var user1 = new User(1, "Ivanov I.I.");

        user1.GetHashCode().Should().Be(new User(1, "Petrov I.I.").GetHashCode());
        user1.GetHashCode().Should().NotBe(new User(3, "Ivanov I.I.").GetHashCode());
    }

    public class Admin : User
    {
        public Admin(int id) : base(id, "Admin")
        {
        }
    }

    [Fact]
    public void Unrelated_derived_types_with_same_identifier_are_not_equal()
    {
        // UserProxy and Admin both derive from User, but neither derives from the other.
        var proxy = new UserProxy(7);
        var admin = new Admin(7);

        proxy.Equals(admin).Should().BeFalse();
        admin.Equals(proxy).Should().BeFalse();
    }

    [Fact]
    public void Hash_code_is_stable_when_transient_entity_gets_identifier()
    {
        var user = new User(0, "Ivanov I.I.");
        var set = new HashSet<User> { user };

        user.AssignId(42); // for example, assigned by the database on save

        set.Contains(user).Should().BeTrue();
    }

    [Fact]
    public void ToString_contains_type_and_identifier()
        => new User(1, "Ivanov I.I.").ToString().Should().Be("User [Id=1]");
}
