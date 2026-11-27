using Lintel.Domain.Tests.TestUtils;

namespace Lintel.Domain.Tests;

public class CompositeEntityTests
{
    private static readonly User _user = new(1, "Ivanov I.I.");
    private static readonly Role _admin = new(RoleId.Admin, "Admin");
    private static readonly Role _userRole = new(RoleId.User, "User");

    public static TheoryData<UserRole, UserRole?, bool> EqualityData()
    {
        var userRole = new UserRole(_user, _admin);
        return new()
        {
            { userRole, userRole, true },
            { userRole, new UserRole(_user, _admin), true },
            { userRole, new UserRole(_user, _userRole), false },
            { userRole, new UserRole(new User(2, "Petrov P.P."), _admin), false },
            { userRole, null, false },
        };
    }

    [Theory]
    [MemberData(nameof(EqualityData))]
    public void Equals_compares_composite_identifiers(UserRole left, UserRole? right, bool expected)
    {
        left.Equals(right).Should().Be(expected);
        (left == right).Should().Be(expected);
    }

    [Fact]
    public void Hash_code_depends_on_composite_identifier()
    {
        var userRole = new UserRole(_user, _admin);

        userRole.GetHashCode().Should().Be(new UserRole(_user, _admin).GetHashCode());
        userRole.GetHashCode().Should().NotBe(new UserRole(_user, _userRole).GetHashCode());
    }

    [Fact]
    public void Null_composite_identifier_is_equal_only_to_itself()
    {
        var entity = new NullKeyEntity();

        entity.Equals(entity).Should().BeTrue();
        entity.Equals(new NullKeyEntity()).Should().BeFalse();
        entity.GetHashCode().Should().Be(0);
        entity.ToString().Should().Be("NullKeyEntity");
    }

    [Fact]
    public void Is_never_transient()
        => new UserRole(_user, _admin).IsTransient().Should().BeFalse();

    [Fact]
    public void ToString_contains_type_and_composite_identifier()
        => new UserRole(_user, _admin).ToString().Should().Be("UserRole { UserId = 1, RoleId = Admin }");
}
