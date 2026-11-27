using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.DataProtection;

namespace Lintel.Settings.Internal;

/// <summary>
/// System.Text.Json options that implement <see cref="OptionAttribute"/> and the Structr (Newtonsoft) conventions:
/// case-insensitive names, comments and trailing commas allowed, private setters are written, enums by name or number.
/// </summary>
internal static class SettingsSerializer
{
    public static JsonSerializerOptions CreateOptions(IDataProtector? dataProtector, bool forXml)
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo => ApplyOptions(typeInfo, dataProtector));

        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(allowIntegerValues: true) },
        };

        if (forXml)
        {
            // XML has no types: every value is text.
            options.NumberHandling = JsonNumberHandling.AllowReadingFromString;
            options.Converters.Add(new TextBooleanConverter());
        }

        return options;
    }

    private static void ApplyOptions(JsonTypeInfo typeInfo, IDataProtector? dataProtector)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        var defaults = new List<(PropertyInfo Property, object? Value)>();

        foreach (var property in typeInfo.Properties)
        {
            if (property.AttributeProvider is not PropertyInfo propertyInfo)
            {
                continue;
            }

            // Private and protected setters are written, as with Structr's Newtonsoft contract resolver.
            if (property.Set is null && propertyInfo.GetSetMethod(nonPublic: true) is { } setter)
            {
                property.Set = (target, value) => setter.Invoke(target, [value]);
            }

            if (propertyInfo.GetCustomAttribute<OptionAttribute>() is not { } option)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(option.Alias))
            {
                property.Name = option.Alias;
            }

            if (option.DefaultValue is not null)
            {
                defaults.Add((propertyInfo, ConvertDefault(option.DefaultValue, propertyInfo)));
            }

            if (option.Protected || !string.IsNullOrEmpty(option.EncryptionPassphrase))
            {
                if (propertyInfo.PropertyType != typeof(string))
                {
                    throw new InvalidOperationException($"Only string properties can be encrypted: {propertyInfo.DeclaringType?.Name}.{propertyInfo.Name}.");
                }
                if (option.Protected && !string.IsNullOrEmpty(option.EncryptionPassphrase))
                {
                    throw new InvalidOperationException($"Use either Protected or EncryptionPassphrase: {propertyInfo.DeclaringType?.Name}.{propertyInfo.Name}.");
                }

                property.CustomConverter = option.Protected
                    ? new EncryptedStringConverter(propertyInfo, value => Protector(dataProtector, propertyInfo).Protect(value),
                        value => Protector(dataProtector, propertyInfo).Unprotect(value))
                    : new EncryptedStringConverter(propertyInfo, value => LegacyStringEncryptor.Encrypt(value, option.EncryptionPassphrase!),
                        value => LegacyStringEncryptor.Decrypt(value, option.EncryptionPassphrase!));
            }
        }

        if (defaults.Count > 0)
        {
            // Applied before reading, so values present in the file win (Newtonsoft's DefaultValueHandling.Populate).
            var previous = typeInfo.OnDeserializing;
            typeInfo.OnDeserializing = target =>
            {
                previous?.Invoke(target);
                foreach (var (property, value) in defaults)
                {
                    property.SetValue(target, value);
                }
            };
        }
    }

    private static IDataProtector Protector(IDataProtector? dataProtector, PropertyInfo property)
        => dataProtector ?? throw new InvalidOperationException(
            $"{property.DeclaringType?.Name}.{property.Name} is [Option(Protected = true)], but SettingsProviderOptions.DataProtector is not set.");

    private static object? ConvertDefault(object value, PropertyInfo property)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (type.IsInstanceOfType(value))
        {
            return value;
        }
        if (type.IsEnum)
        {
            return value is string name ? Enum.Parse(type, name, ignoreCase: true) : Enum.ToObject(type, value);
        }
        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }

    private sealed class EncryptedStringConverter(PropertyInfo property, Func<string, string> encrypt, Func<string, string> decrypt)
        : JsonConverter<string>
    {
        public override bool HandleNull => false;

        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            try
            {
                return decrypt(value);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                throw new JsonException($"The value of {property.DeclaringType?.Name}.{property.Name} cannot be decrypted.", ex);
            }
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            if (string.IsNullOrEmpty(value))
            {
                writer.WriteNullValue();
                return;
            }
            writer.WriteStringValue(encrypt(value));
        }
    }

    // Used only for XML, where every value is a string (or null for an empty element).
    private sealed class TextBooleanConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString()?.Trim().ToLowerInvariant() switch
                {
                    "true" or "1" => true,
                    "false" or "0" => false,
                    var text => throw new JsonException($"'{text}' is not a boolean value."),
                },
                _ => throw new JsonException("A boolean value was expected."),
            };

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) => writer.WriteBooleanValue(value);
    }
}
