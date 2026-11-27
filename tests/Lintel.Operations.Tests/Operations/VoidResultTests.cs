namespace Lintel.Operations.Tests.Operations;

public class VoidResultTests
{
    [Fact]
    public async Task Uniqueness()
    {
        var voidResult = new VoidResult();

        voidResult.Should().Be(VoidResult.Value);
        (await Task.FromResult(voidResult)).Should().Be(await VoidResult.TaskValue);
        default(VoidResult).Should().Be(VoidResult.Value);
    }

    [Fact]
    public void Equality()
    {
        var voidResult = new VoidResult();
        var otherVoidResult = new VoidResult();

        voidResult.Equals(otherVoidResult).Should().BeTrue();
        voidResult.Equals("someNotVoidResultObject").Should().BeFalse();
        voidResult.Equals((object)otherVoidResult).Should().BeTrue();
        (voidResult == otherVoidResult).Should().BeTrue();
        (voidResult != otherVoidResult).Should().BeFalse();
    }

    [Fact]
    public void Comparing()
    {
        var voidResult = new VoidResult();
        var otherVoidResult = new VoidResult();

        voidResult.CompareTo(otherVoidResult).Should().Be(0);
        voidResult.CompareTo((object)otherVoidResult).Should().Be(0);
        (voidResult < otherVoidResult).Should().BeFalse();
        (voidResult <= otherVoidResult).Should().BeTrue();
        (voidResult > otherVoidResult).Should().BeFalse();
        (voidResult >= otherVoidResult).Should().BeTrue();
    }

    [Fact]
    public void Comparing_throws_for_another_type()
    {
        var voidResult = new VoidResult();

        var act = () => voidResult.CompareTo("someNotVoidResultObject");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void HashCode_is_zero()
        => new VoidResult().GetHashCode().Should().Be(0);

    [Fact]
    public void ToString_is_empty()
        => new VoidResult().ToString().Should().BeEmpty();
}
