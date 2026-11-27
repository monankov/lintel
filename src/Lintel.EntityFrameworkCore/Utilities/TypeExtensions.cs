namespace Lintel.EntityFrameworkCore.Utilities;

internal static class TypeExtensions
{
    /// <summary>
    /// Whether <paramref name="type"/> derives from or implements the open generic <paramref name="genericType"/>.
    /// </summary>
    public static bool IsAssignableFromGenericType(this Type genericType, Type type)
    {
        if (genericType.IsInterface && type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericType))
        {
            return true;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == genericType)
            {
                return true;
            }
        }

        return false;
    }
}
