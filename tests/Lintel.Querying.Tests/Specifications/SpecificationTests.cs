using System.Linq.Expressions;
using Lintel.Specifications;

namespace Lintel.Querying.Tests.Specifications;

public class SpecificationTests
{
    private sealed class StringContainsSpecification(string part) : Specification<string>
    {
        public string Part { get; } = part;

        public override Expression<Func<string, bool>> ToExpression() => x => x.Contains(Part);

        public override bool Equals(object? obj) => obj is StringContainsSpecification other && other.Part == Part;

        public override int GetHashCode() => Part.GetHashCode(StringComparison.Ordinal);
    }

    private sealed class StringLengthSpecification(int length) : Specification<string>
    {
        public override Expression<Func<string, bool>> ToExpression() => s => s.Length == length;
    }

    private static readonly StringContainsSpecification _ab = new("ab");
    private static readonly StringContainsSpecification _de = new("de");

    [Theory]
    [InlineData("abc", 3, true)]
    [InlineData("abcd", 3, false)]
    public void IsSatisfiedBy_and_ToFunc(string input, int length, bool expected)
    {
        var spec = new StringLengthSpecification(length);

        spec.IsSatisfiedBy(input).Should().Be(expected);
        spec.ToFunc()(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("abc", false)]
    [InlineData("bcd", false)]
    [InlineData("abcde", true)]
    [InlineData("bcde", false)]
    public void And(string value, bool expected)
    {
        _ab.And(_de).IsSatisfiedBy(value).Should().Be(expected);
        (_ab & _de).IsSatisfiedBy(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("abc", true)]
    [InlineData("bcd", false)]
    [InlineData("abcde", true)]
    [InlineData("bcde", true)]
    public void Or(string value, bool expected)
    {
        _ab.Or(_de).IsSatisfiedBy(value).Should().Be(expected);
        (_ab | _de).IsSatisfiedBy(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("abc", false)]
    [InlineData("bcd", true)]
    [InlineData("abcde", false)]
    [InlineData("bcde", true)]
    public void Not(string value, bool expected)
    {
        _ab.Not().IsSatisfiedBy(value).Should().Be(expected);
        (!_ab).IsSatisfiedBy(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("abc", true)]
    [InlineData("bcd", false)]
    [InlineData("abcde", false)]
    [InlineData("bcde", false)]
    public void AndNot(string value, bool expected)
        => _ab.AndNot(_de).IsSatisfiedBy(value).Should().Be(expected);

    [Theory]
    [InlineData("abc", true)]
    [InlineData("bcd", true)]
    [InlineData("abcde", true)]
    [InlineData("bcde", false)]
    public void OrNot(string value, bool expected)
        => _ab.OrNot(_de).IsSatisfiedBy(value).Should().Be(expected);

    [Fact]
    public void Named_operator_alternatives_match_operators()
    {
        Specification<string>.BitwiseAnd(_ab, _de).Should().Be(_ab & _de);
        Specification<string>.BitwiseOr(_ab, _de).Should().Be(_ab | _de);
        Specification<string>.LogicalNot(_ab).Should().Be(!_ab);
    }

    [Fact]
    public void Composed_expression_uses_single_parameter_and_no_invocations()
    {
        // LINQ providers such as EF Core cannot translate Invoke nodes or foreign parameters.
        var expression = (_ab & !_de | new StringLengthSpecification(2)).ToExpression();

        var visitor = new NodeCollector();
        visitor.Visit(expression.Body);

        visitor.Parameters.Should().OnlyContain(p => p == expression.Parameters[0]);
        visitor.HasInvocation.Should().BeFalse();
    }

    [Fact]
    public void Where_filters_queryable_with_expression()
    {
        var query = new[] { "abc", "bcd", "abcde" }.AsQueryable();

        var result = query.Where(_ab);

        result.Expression.ToString().Should().Contain("Contains");
        result.Should().Equal("abc", "abcde");
    }

    [Fact]
    public void Where_filters_enumerable()
    {
        IEnumerable<string> source = ["abc", "bcd", "abcde"];

        source.Where(_de).Should().Equal("abcde");
    }

    [Fact]
    public void Any_and_None()
    {
        string[] values = ["abc", ""];

        values.Where(new AnySpecification<string>()).Should().Equal(values);
        values.Where(new NoneSpecification<string>()).Should().BeEmpty();
    }

    public static TheoryData<object, object?, bool> EqualityData => new()
    {
        { new AnySpecification<string>(), null, false },
        { new AnySpecification<string>(), new AnySpecification<string>(), true },
        { new AnySpecification<string>(), new NoneSpecification<string>(), false },
        { new NoneSpecification<string>(), new NoneSpecification<string>(), true },
        { _ab.And(_de), new StringContainsSpecification("ab").And(new StringContainsSpecification("de")), true },
        { _ab.And(_de), _de.And(_ab), false },
        { _ab.And(_de), _ab.Or(_de), false },
        { _ab.Or(_de), _ab | _de, true },
        { _ab.Not(), !new StringContainsSpecification("ab"), true },
        { _ab.Not(), _de.Not(), false },
    };

    [Theory]
    [MemberData(nameof(EqualityData))]
    public void Equality(object spec, object? other, bool expected)
    {
        spec.Equals(other).Should().Be(expected);
        if (expected)
        {
            spec.GetHashCode().Should().Be(other!.GetHashCode());
        }
    }

    [Fact]
    public void Composition_with_null_throws()
    {
        var actions = new Action[]
        {
            () => _ab.And(null!),
            () => _ab.Or(null!),
            () => ((Specification<string>)null!).Not(),
        };

        actions.Should().AllSatisfy(act => act.Should().Throw<ArgumentNullException>());
    }

    private sealed class NodeCollector : ExpressionVisitor
    {
        public HashSet<ParameterExpression> Parameters { get; } = [];
        public bool HasInvocation { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            Parameters.Add(node);
            return base.VisitParameter(node);
        }

        protected override Expression VisitInvocation(InvocationExpression node)
        {
            HasInvocation = true;
            return base.VisitInvocation(node);
        }
    }
}
