using System.Linq.Expressions;

namespace Lintel.Specifications.Internal;

internal sealed class AndSpecification<T> : Specification<T>
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;

    public AndSpecification(Specification<T> left, Specification<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> ToExpression()
        => _left.ToExpression().Compose(_right.ToExpression(), Expression.AndAlso);

    public override bool Equals(object? obj)
        => obj is AndSpecification<T> other && _left.Equals(other._left) && _right.Equals(other._right);

    public override int GetHashCode() => HashCode.Combine(typeof(AndSpecification<T>), _left, _right);
}

internal sealed class OrSpecification<T> : Specification<T>
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;

    public OrSpecification(Specification<T> left, Specification<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> ToExpression()
        => _left.ToExpression().Compose(_right.ToExpression(), Expression.OrElse);

    public override bool Equals(object? obj)
        => obj is OrSpecification<T> other && _left.Equals(other._left) && _right.Equals(other._right);

    public override int GetHashCode() => HashCode.Combine(typeof(OrSpecification<T>), _left, _right);
}

internal sealed class NotSpecification<T> : Specification<T>
{
    private readonly Specification<T> _inner;

    public NotSpecification(Specification<T> inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var expression = _inner.ToExpression();
        return Expression.Lambda<Func<T, bool>>(Expression.Not(expression.Body), expression.Parameters);
    }

    public override bool Equals(object? obj) => obj is NotSpecification<T> other && _inner.Equals(other._inner);

    public override int GetHashCode() => HashCode.Combine(typeof(NotSpecification<T>), _inner);
}

internal static class ExpressionComposition
{
    /// <summary>
    /// Merges the bodies of two lambdas, rebinding the parameters of <paramref name="second"/>
    /// to the parameters of <paramref name="first"/> so the result stays translatable by LINQ providers.
    /// </summary>
    public static Expression<Func<T, bool>> Compose<T>(this Expression<Func<T, bool>> first,
        Expression<Func<T, bool>> second,
        Func<Expression, Expression, BinaryExpression> merge)
    {
        var secondBody = new ParameterRebinder(second.Parameters[0], first.Parameters[0]).Visit(second.Body);
        return Expression.Lambda<Func<T, bool>>(merge(first.Body, secondBody), first.Parameters);
    }

    private sealed class ParameterRebinder(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == from ? to : base.VisitParameter(node);
    }
}
