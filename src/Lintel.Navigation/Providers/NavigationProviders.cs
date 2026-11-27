using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Xml.Linq;

namespace Lintel.Navigation.Providers;

/// <summary>
/// Loads navigation from a JSON file: an array of items, each with its properties and a <c>Children</c> array.
/// </summary>
/// <example>
/// <code>
/// [
///   { "Id": "orders", "Title": "Orders", "Url": "/orders", "Children": [ { "Id": "new-order", "Title": "New", "Url": "/orders/new" } ] }
/// ]
/// </code>
/// </example>
/// <typeparam name="TNavigationItem">The item type.</typeparam>
public class JsonNavigationProvider<TNavigationItem> : INavigationProvider<TNavigationItem>
    where TNavigationItem : NavigationItem<TNavigationItem>, new()
{
    private static readonly JsonSerializerOptions _options = CreateOptions();

    private readonly string _path;

    /// <summary>
    /// Initializes the provider.
    /// </summary>
    /// <param name="path">The path of the JSON file.</param>
    /// <exception cref="ArgumentException">If <paramref name="path"/> is empty.</exception>
    public JsonNavigationProvider(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _path = path;
    }

    /// <inheritdoc />
    /// <exception cref="FileNotFoundException">If the file does not exist.</exception>
    public IEnumerable<TNavigationItem> CreateNavigation()
    {
        if (!File.Exists(_path))
        {
            throw new FileNotFoundException("Navigation file not found.", _path);
        }

        var content = File.ReadAllText(_path);
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var root = JsonNode.Parse(content, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })
            as JsonArray ?? throw new InvalidOperationException("The navigation JSON must be an array of items.");

        return [.. root.OfType<JsonObject>().Select(Parse)];
    }

    private static TNavigationItem Parse(JsonObject json)
    {
        var item = json.Deserialize<TNavigationItem>(_options) ?? new TNavigationItem();

        var children = json.FirstOrDefault(p => string.Equals(p.Key, nameof(NavigationItem<TNavigationItem>.Children), StringComparison.OrdinalIgnoreCase)).Value;
        if (children is JsonArray array)
        {
            foreach (var child in array.OfType<JsonObject>())
            {
                item.AddChild(Parse(child));
            }
        }

        return item;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
            {
                return;
            }
            foreach (var property in typeInfo.Properties)
            {
                // Private and protected setters are written, as with Structr's Newtonsoft contract resolver.
                if (property.Set is null && property.AttributeProvider is PropertyInfo { } info
                    && info.DeclaringType != typeof(NavigationItem<TNavigationItem>)
                    && info.GetSetMethod(nonPublic: true) is { } setter)
                {
                    property.Set = (target, value) => setter.Invoke(target, [value]);
                }
            }
        });

        return new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            // Enums by name or number, as Newtonsoft read them in Structr.
            Converters = { new JsonStringEnumConverter(allowIntegerValues: true) },
        };
    }
}

/// <summary>
/// Loads navigation from an XML file: child elements of the root are items, their attributes are properties
/// (case-insensitive), nested elements are children. Element names are not significant.
/// </summary>
/// <example>
/// <code>
/// &lt;navigation&gt;
///   &lt;item id="orders" title="Orders" url="/orders"&gt;
///     &lt;item id="new-order" title="New" url="/orders/new" /&gt;
///   &lt;/item&gt;
/// &lt;/navigation&gt;
/// </code>
/// </example>
/// <typeparam name="TNavigationItem">The item type.</typeparam>
public class XmlNavigationProvider<TNavigationItem> : INavigationProvider<TNavigationItem>
    where TNavigationItem : NavigationItem<TNavigationItem>, new()
{
    private static readonly Dictionary<string, PropertyInfo> _properties = typeof(TNavigationItem)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(property => property.GetSetMethod(nonPublic: true) is not null && property.GetIndexParameters().Length == 0)
        .Where(property => property.DeclaringType != typeof(NavigationItem<TNavigationItem>) || property.GetSetMethod() is not null)
        .GroupBy(property => property.Name, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    private readonly string _path;

    /// <summary>
    /// Initializes the provider.
    /// </summary>
    /// <param name="path">The path of the XML file.</param>
    /// <exception cref="ArgumentException">If <paramref name="path"/> is empty.</exception>
    public XmlNavigationProvider(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _path = path;
    }

    /// <inheritdoc />
    /// <exception cref="FileNotFoundException">If the file does not exist.</exception>
    public IEnumerable<TNavigationItem> CreateNavigation()
    {
        if (!File.Exists(_path))
        {
            throw new FileNotFoundException("Navigation file not found.", _path);
        }

        var root = XDocument.Load(_path).Root ?? throw new InvalidOperationException("Invalid navigation xml file.");
        return [.. root.Elements().Select(Parse)];
    }

    private static TNavigationItem Parse(XElement element)
    {
        var item = new TNavigationItem();

        foreach (var attribute in element.Attributes())
        {
            if (_properties.TryGetValue(attribute.Name.LocalName, out var property))
            {
                property.SetValue(item, ConvertValue(attribute.Value, property));
            }
        }

        foreach (var child in element.Elements())
        {
            item.AddChild(Parse(child));
        }

        return item;
    }

    private static object? ConvertValue(string value, PropertyInfo property)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (type == typeof(string))
        {
            return value;
        }
        if (value.Length == 0 && type != property.PropertyType)
        {
            return null;
        }

        try
        {
            return type.IsEnum ? Enum.Parse(type, value, ignoreCase: true)
                : type == typeof(Guid) ? Guid.Parse(value)
                : Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or ArgumentException or OverflowException)
        {
            throw new InvalidOperationException($"Cannot convert '{value}' to {type.Name} for {typeof(TNavigationItem).Name}.{property.Name}.", ex);
        }
    }
}
