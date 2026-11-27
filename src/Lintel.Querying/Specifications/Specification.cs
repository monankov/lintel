using System.Linq.Expressions;
using Lintel.Specifications.Internal;

namespace Lintel.Specifications;

/// <summary>
/// Encapsulates a business rule as an expression that can be checked in memory
/// or translated by a LINQ provider (for example EF Core).
/// </summary>
/// <remarks>
/// Combine specifications with <see cref="SpecificationExtensions.And{T}(Specification{T}, Specification{T})"/>,
/// <see cref="SpecificationExtensions.Or{T}(Specification{T}, Specification{T})"/>,
/// <see cref="SpecificationExtensions.Not{T}(Specification{T})"/> or the <c>&amp;</c>, <c>|</c> and <c>!</c> operators.
/// </remarks>
/// <typeparam name="T">The type of objects the rule applies to.</typeparam>
public abstract class Specification<T>
{
    /// <summary>
    /// Returns the rule as an expression tree.
    /// </summary>
    /// <returns>The predicate expression.</returns>
    public abstract Expression<Func<T, bool>> ToExpression();

    /// <summary>
    /// Compiles the rule into a delegate.
    /// </summary>
    /// <remarks>Compilation is relatively expensive: when checking many objects, compile once and reuse the delegate.</remarks>
    /// <returns>The compiled predicate.</returns>
    public Func<T, bool> ToFunc() => ToExpression().Compile();

    /// <summary>
    /// Checks whether <paramref name="candidate"/> satisfies the rule.
    /// </summary>
    /// <remarks>The expression is compiled on every call; use <see cref="ToFunc"/> to check many objects.</remarks>
    /// <param name="candidate">The object to check.</param>
    /// <returns><see langword="true"/> if the rule is satisfied.</returns>
    public bool IsSatisfiedBy(T candidate) => ToFunc()(candidate);

    /// <summary>Combines two specifications with logical AND.</summary>
    /// <param name="left">The first specification.</param>
    /// <param name="right">The second specification.</param>
    /// <returns>A specification satisfied when both are satisfied.</returns>
    public static Specification<T> operator &(Specification<T> left, Specification<T> right)
        => new AndSpecification<T>(left, right);

    /// <summary>Combines two specifications with logical OR.</summary>
    /// <param name="left">The first specification.</param>
    /// <param name="right">The second specification.</param>
    /// <returns>A specification satisfied when either is satisfied.</returns>
    public static Specification<T> operator |(Specification<T> left, Specification<T> right)
        => new OrSpecification<T>(left, right);

    /// <summary>Negates a specification.</summary>
    /// <param name="specification">The specification to negate.</param>
    /// <returns>A specification satisfied when <paramref name="specification"/> is not.</returns>
    public static Specification<T> operator !(Specification<T> specification)
        => new NotSpecification<T>(specification);

    /// <summary>Combines two specifications with logical AND. Same as the <c>&amp;</c> operator.</summary>
    /// <param name="left">The first specification.</param>
    /// <param name="right">The second specification.</param>
    /// <returns>A specification satisfied when both are satisfied.</returns>
    public static Specification<T> BitwiseAnd(Specification<T> left, Specification<T> right) => left & right;

    /// <summary>Combines two specifications with logical OR. Same as the <c>|</c> operator.</summary>
    /// <param name="left">The first specification.</param>
    /// <param name="right">The second specification.</param>
    /// <returns>A specification satisfied when either is satisfied.</returns>
    public static Specification<T> BitwiseOr(Specification<T> left, Specification<T> right) => left | right;

    /// <summary>Negates a specification. Same as the <c>!</c> operator.</summary>
    /// <param name="specification">The specification to negate.</param>
    /// <returns>A specification satisfied when <paramref name="specification"/> is not.</returns>
    public static Specification<T> LogicalNot(Specification<T> specification) => !specification;
}
