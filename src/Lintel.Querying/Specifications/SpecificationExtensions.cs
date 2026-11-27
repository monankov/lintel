using Lintel.Specifications.Internal;

namespace Lintel.Specifications;

/// <summary>
/// Composition of specifications and filtering of sequences by specifications.
/// </summary>
public static class SpecificationExtensions
{
    /// <summary>Combines two specifications with logical AND.</summary>
    /// <typeparam name="T">The type of objects the rule applies to.</typeparam>
    /// <param name="left">The first specification.</param>
    /// <param name="right">The second specification.</param>
    /// <returns>A specification satisfied when both are satisfied.</returns>
    /// <exception cref="ArgumentNullException">If any specification is <see langword="null"/>.</exception>
    public static Specification<T> And<T>(this Specification<T> left, Specification<T> right)
        => new AndSpecification<T>(left, right);

    /// <summary>Combines two specifications with logical OR.</summary>
    /// <typeparam name="T">The type of objects the rule applies to.</typeparam>
    /// <param name="left">The first specification.</param>
    /// <param name="right">The second specification.</param>
    /// <returns>A specification satisfied when either is satisfied.</returns>
    /// <exception cref="ArgumentNullException">If any specification is <see langword="null"/>.</exception>
    public static Specification<T> Or<T>(this Specification<T> left, Specification<T> right)
        => new OrSpecification<T>(left, right);

    /// <summary>Negates a specification.</summary>
    /// <typeparam name="T">The type of objects the rule applies to.</typeparam>
    /// <param name="specification">The specification to negate.</param>
    /// <returns>A specification satisfied when <paramref name="specification"/> is not.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="specification"/> is <see langword="null"/>.</exception>
    public static Specification<T> Not<T>(this Specification<T> specification)
        => new NotSpecification<T>(specification);

    /// <summary>Combines a specification with the negation of another: <c>left AND NOT right</c>.</summary>
    /// <typeparam name="T">The type of objects the rule applies to.</typeparam>
    /// <param name="left">The first specification.</param>
    /// <param name="right">The specification to negate.</param>
    /// <returns>The combined specification.</returns>
    /// <exception cref="ArgumentNullException">If any specification is <see langword="null"/>.</exception>
    public static Specification<T> AndNot<T>(this Specification<T> left, Specification<T> right)
        => new AndSpecification<T>(left, new NotSpecification<T>(right));

    /// <summary>Combines a specification with the negation of another: <c>left OR NOT right</c>.</summary>
    /// <typeparam name="T">The type of objects the rule applies to.</typeparam>
    /// <param name="left">The first specification.</param>
    /// <param name="right">The specification to negate.</param>
    /// <returns>The combined specification.</returns>
    /// <exception cref="ArgumentNullException">If any specification is <see langword="null"/>.</exception>
    public static Specification<T> OrNot<T>(this Specification<T> left, Specification<T> right)
        => new OrSpecification<T>(left, new NotSpecification<T>(right));

    /// <summary>
    /// Filters a query by a specification. The specification expression is passed to the query provider,
    /// so it is translated (for example to SQL by EF Core).
    /// </summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">The query to filter.</param>
    /// <param name="specification">The specification to satisfy.</param>
    /// <returns>A query with elements satisfying the specification.</returns>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public static IQueryable<T> Where<T>(this IQueryable<T> source, Specification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(specification);

        return source.Where(specification.ToExpression());
    }

    /// <summary>
    /// Filters a sequence by a specification. The specification is compiled once per call.
    /// </summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">The sequence to filter.</param>
    /// <param name="specification">The specification to satisfy.</param>
    /// <returns>Elements satisfying the specification.</returns>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public static IEnumerable<T> Where<T>(this IEnumerable<T> source, Specification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(specification);

        return source.Where(specification.ToFunc());
    }
}
