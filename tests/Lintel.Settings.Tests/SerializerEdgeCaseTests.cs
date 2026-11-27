using System.Text.Json;
using Lintel.Settings.Providers;
using Lintel.Settings.Tests.TestUtils;

namespace Lintel.Settings.Tests;

public sealed class SerializerEdgeCaseTests : IDisposable
{
    public enum Level
    {
        Low,
        High,
    }

    public class EnumDefaults
    {
        [Option(DefaultValue = "high")]
        public Level ByName { get; set; }

        [Option(DefaultValue = 1)]
        public Level ByNumber { get; set; }

        [Option(DefaultValue = 30)]
        public int? Seconds { get; set; }
    }

    public class EncryptedNumber
    {
        [Option(EncryptionPassphrase = "x")]
        public int Pin { get; set; }
    }

    public class BothEncryptions
    {
        [Option(EncryptionPassphrase = "x", Protected = true)]
        public string? Secret { get; set; }
    }

    public class Flags
    {
        public bool Enabled { get; set; }
    }

    private readonly TempDirectory _temp = new();

    private T Load<T>(string name, string content) where T : class, new()
    {
        var path = _temp.File(name, content);
        return name.EndsWith(".xml", StringComparison.Ordinal)
            ? new XmlSettingsProvider<T>(path, new SettingsProviderOptions()).GetSettings()
            : new JsonSettingsProvider<T>(path, new SettingsProviderOptions()).GetSettings();
    }

    [Fact]
    public void Default_values_are_converted_to_property_types()
    {
        var settings = Load<EnumDefaults>("defaults.json", "{}");

        settings.ByName.Should().Be(Level.High);
        settings.ByNumber.Should().Be(Level.High);
        settings.Seconds.Should().Be(30);
    }

    [Fact]
    public void Only_string_properties_can_be_encrypted()
    {
        var act = () => Load<EncryptedNumber>("number.json", "{}");

        act.Should().Throw<InvalidOperationException>().WithMessage("Only string properties can be encrypted: EncryptedNumber.Pin.");
    }

    [Fact]
    public void Protected_and_passphrase_cannot_be_combined()
    {
        var act = () => Load<BothEncryptions>("both.json", "{}");

        act.Should().Throw<InvalidOperationException>().WithMessage("Use either Protected or EncryptionPassphrase: BothEncryptions.Secret.");
    }

    [Theory]
    [InlineData("""{ "ApiKey": "" }""", "")]
    [InlineData("""{ "ApiKey": null }""", null)]
    public void Empty_encrypted_values_are_read_as_is(string json, string? expected)
        => Load<TestSettings>("empty.json", json).ApiKey.Should().Be(expected);

    [Fact]
    public void Value_that_is_not_base64_gives_clear_error()
    {
        var act = () => Load<TestSettings>("bad.json", """{ "ApiKey": "%%%" }""");

        act.Should().Throw<JsonException>().WithMessage("The value of TestSettings.ApiKey cannot be decrypted.")
            .WithInnerException<FormatException>();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData(" TRUE ", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    public void Xml_booleans_are_read_from_text(string text, bool expected)
        => Load<Flags>("flags.xml", $"<flags><Enabled>{text}</Enabled></flags>").Enabled.Should().Be(expected);

    [Fact]
    public void Invalid_xml_boolean_gives_clear_error()
    {
        var act = () => Load<Flags>("flags.xml", "<flags><Enabled>yes</Enabled></flags>");

        act.Should().Throw<JsonException>().WithMessage("'yes' is not a boolean value.");
    }

    [Fact]
    public void Empty_xml_element_for_boolean_gives_clear_error()
    {
        var act = () => Load<Flags>("flags.xml", "<flags><Enabled /></flags>");

        act.Should().Throw<JsonException>().WithMessage("A boolean value was expected.");
    }

    [Fact]
    public void Xml_file_without_root_is_an_error()
    {
        var act = () => Load<Flags>("empty.xml", "");

        act.Should().Throw<System.Xml.XmlException>();
    }

    public void Dispose() => _temp.Dispose();
}
