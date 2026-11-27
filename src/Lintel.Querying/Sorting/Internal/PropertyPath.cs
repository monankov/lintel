using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace Lintel.Querying.Internal;

internal static class PropertyPath
{
    public const string TrimmingMessage = "Sorting by property name uses reflection to find properties; they may be trimmed.";
    public const string DynamicCodeMessage = "Sorting a query by property name creates generic methods at runtime.";

    /// <summary>
    /// Builds <c>x => x.A.B.C</c> for <paramref name="propertyName"/> <c>"A.B.C"</c>.
    /// Each part is matched exactly first, then case-insensitively.
    /// </summary>
    [RequiresUnreferencedCode(TrimmingMessage)]
    public static (ParameterExpression Parameter, Expression Body) Build<T>(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression body = parameter;
        var type = typeof(T);

        foreach (var part in propertyName.Split('.'))
        {
            var property = FindProperty(type, part)
                ?? throw new InvalidOperationException($"Nested property with name {propertyName} for type {typeof(T).Name} was not found.");

            body = Expression.Property(body, property);
            type = property.PropertyType;
        }

        return (parameter, body);
    }

    [RequiresUnreferencedCode(TrimmingMessage)]
    private static PropertyInfo? FindProperty(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

        return type.GetProperty(name, flags)
            ?? type.GetProperty(name, flags | BindingFlags.IgnoreCase);
    }
}
