using System.Linq.Expressions;

namespace Lintel.Specifications;

/// <summary>
/// A specification satisfied by any object.
/// </summary>
/// <typeparam name="T">The type of objects the rule applies to.</typeparam>
public class AnySpecification<T> : Specification<T>
{
    /// <inheritdoc />
    public override Expression<Func<T, bool>> ToExpression() => x => true;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is not null && GetType() == obj.GetType();

    /// <inheritdoc />
    public override int GetHashCode() => GetType().GetHashCode();
}
