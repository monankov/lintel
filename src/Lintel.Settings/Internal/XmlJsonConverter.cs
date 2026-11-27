using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Lintel.Settings.Internal;

/// <summary>
/// Converts between XML and JSON the way Structr did with Newtonsoft (<c>SerializeXNode</c>/<c>DeserializeXNode</c>):
/// child elements are properties, repeated elements are arrays, an empty element is <see langword="null"/>,
/// the root element name is not significant. Attributes are ignored.
/// </summary>
internal static class XmlJsonConverter
{
    public static JsonObject ToJson(XElement root) => ToObject(root);

    public static XDocument ToXml(JsonNode? json, string rootName)
    {
        var root = new XElement(XmlConvert(rootName));
        if (json is JsonObject obj)
        {
            AppendProperties(root, obj);
        }
        return new XDocument(root);
    }

    private static JsonObject ToObject(XElement element)
    {
        var result = new JsonObject();
        foreach (var group in element.Elements().GroupBy(child => child.Name.LocalName))
        {
            var children = group.ToList();
            result[group.Key] = children.Count == 1
                ? ToNode(children[0])
                : new JsonArray(children.Select(ToNode).ToArray());
        }
        return result;
    }

    private static JsonNode? ToNode(XElement element)
        => element.HasElements ? ToObject(element)
            : element.IsEmpty || element.Value.Length == 0 ? null
            : JsonValue.Create(element.Value);

    private static void AppendProperties(XElement parent, JsonObject obj)
    {
        foreach (var (name, value) in obj)
        {
            if (value is JsonArray array)
            {
                foreach (var item in array)
                {
                    parent.Add(ToElement(name, item));
                }
            }
            else
            {
                parent.Add(ToElement(name, value));
            }
        }
    }

    private static XElement ToElement(string name, JsonNode? value)
    {
        var element = new XElement(XmlConvert(name));
        switch (value)
        {
            case null:
                break;
            case JsonObject obj:
                AppendProperties(element, obj);
                break;
            case JsonValue scalar:
                element.Value = scalar.GetValueKind() switch
                {
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.String => scalar.GetValue<string>(),
                    _ => scalar.ToJsonString(),
                };
                break;
            default:
                element.Value = value.ToJsonString();
                break;
        }
        return element;
    }

    private static string XmlConvert(string name) => System.Xml.XmlConvert.EncodeLocalName(name)!;
}
