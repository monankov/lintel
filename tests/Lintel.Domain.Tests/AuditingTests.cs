namespace Lintel.Domain.Tests;

public class AuditingTests
{
    public class Order : SignedAuditableEntity<Order, int>, ISignedSoftDeletable
    {
        public DateTime? DateDeleted { get; private set; }
        public string? DeletedBy { get; private set; }
    }

    [Fact]
    public void Property_names_match_interfaces()
    {
        AuditableProperties.DateCreated.Should().Be(nameof(Order.DateCreated));
        AuditableProperties.CreatedBy.Should().Be(nameof(Order.CreatedBy));
        AuditableProperties.DateModified.Should().Be(nameof(Order.DateModified));
        AuditableProperties.ModifiedBy.Should().Be(nameof(Order.ModifiedBy));
        AuditableProperties.DateDeleted.Should().Be(nameof(Order.DateDeleted));
        AuditableProperties.DeletedBy.Should().Be(nameof(Order.DeletedBy));
    }

    [Fact]
    public void Signed_auditable_entity_implements_audit_contracts()
    {
        var order = new Order();

        order.Should().BeAssignableTo<ISignedCreatable>()
            .And.BeAssignableTo<ISignedModifiable>()
            .And.BeAssignableTo<ICreatable>()
            .And.BeAssignableTo<IModifiable>()
            .And.BeAssignableTo<IAuditable>();
        order.Should().BeAssignableTo<IUndeletable>();
        order.CreatedBy.Should().BeNull();
        order.DateCreated.Should().Be(default);
    }
}
