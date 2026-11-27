using System.Text.Json;
using System.Text.Json.Nodes;
using Lintel.Settings.Providers;
using Lintel.Settings.Tests.TestUtils;
using Microsoft.AspNetCore.DataProtection;

namespace Lintel.Settings.Tests;

public sealed class JsonSettingsProviderTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private (JsonSettingsProvider<TestSettings> Provider, string Path) Provider(string json, bool cache = true)
    {
        var path = _temp.File("settings.json", json);
        return (new JsonSettingsProvider<TestSettings>(path, new SettingsProviderOptions { Cache = cache }), path);
    }

    [Fact]
    public void Ctor_validates_arguments()
    {
        var withoutPath = () => new JsonSettingsProvider<TestSettings>("", new SettingsProviderOptions());
        var withoutOptions = () => new JsonSettingsProvider<TestSettings>("a.json", null!);

        withoutPath.Should().Throw<ArgumentException>().WithParameterName("path");
        withoutOptions.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void Reads_values_case_insensitively_with_comments_and_trailing_commas()
    {
        var (provider, _) = Provider("""
            {
              // a comment
              "fileName": "readme.txt",
              "Enabled": true,
              "Mode": "On",
              "Smtp": { "Host": "smtp.test", "Port": 587 },
              "Tags": [ "a", "b", ],
            }
            """);

        var settings = provider.GetSettings();

        settings.FileName.Should().Be("readme.txt");
        settings.Enabled.Should().BeTrue();
        settings.Mode.Should().Be(Mode.On);
        settings.Smtp.Should().BeEquivalentTo(new SmtpSettings { Host = "smtp.test", Port = 587 });
        settings.Tags.Should().Equal("a", "b");
    }

    [Fact]
    public void Reads_enum_numbers_too()
        => Provider("""{ "Mode": 1 }""").Provider.GetSettings().Mode.Should().Be(Mode.On);

    [Fact]
    public void Alias_is_used_as_name()
        => Provider("""{ "SomeOwnerNameAlias": "Owner name" }""").Provider.GetSettings().OwnerName.Should().Be("Owner name");

    [Fact]
    public void Default_value_is_used_only_when_property_is_missing()
    {
        Provider("""{ "SomeOtherProperty": "123" }""").Provider.GetSettings()
            .Should().Match<TestSettings>(s => s.HelpUrl == "help.example.com" && s.Timeout == 25);
        Provider("""{ "HelpUrl": "support.example.com", "Timeout": 60 }""").Provider.GetSettings()
            .Should().Match<TestSettings>(s => s.HelpUrl == "support.example.com" && s.Timeout == 60);
    }

    [Fact]
    public void Private_setters_are_read_and_get_only_properties_ignored()
    {
        var (provider, path) = Provider("""{ "InternalNote": "note", "Computed": "ignored" }""");

        provider.GetSettings().InternalNote.Should().Be("note");

        provider.SetSettings(provider.GetSettings());
        JsonNode.Parse(File.ReadAllText(path))!["InternalNote"]!.GetValue<string>().Should().Be("note");
    }

    [Fact]
    public void Encrypted_property_is_compatible_with_Structr_files()
    {
        var encryptedByStructr = StructrEncryptor.Encrypt("secret-key", "abcdef");
        var (provider, path) = Provider($$"""{ "ApiKey": "{{encryptedByStructr}}" }""");

        provider.GetSettings().ApiKey.Should().Be("secret-key");

        provider.SetSettings(new TestSettings { ApiKey = "new-key" });
        var stored = JsonNode.Parse(File.ReadAllText(path))!["ApiKey"]!.GetValue<string>();

        stored.Should().NotBe("new-key");
        StructrEncryptor.Decrypt(stored, "abcdef").Should().Be("new-key");
    }

    [Fact]
    public void Empty_encrypted_value_is_stored_as_null()
    {
        var (provider, path) = Provider("{}");

        provider.SetSettings(new TestSettings { ApiKey = "" });

        JsonNode.Parse(File.ReadAllText(path))!["ApiKey"].Should().BeNull();
    }

    [Fact]
    public void Value_that_cannot_be_decrypted_gives_clear_error()
    {
        var (provider, _) = Provider("""{ "ApiKey": "plain text" }""");

        var act = () => provider.GetSettings();

        act.Should().Throw<JsonException>().WithMessage("The value of TestSettings.ApiKey cannot be decrypted.");
    }

    [Fact]
    public void Protected_property_uses_data_protection()
    {
        var path = _temp.File("protected.json", "{}");
        var protector = new EphemeralDataProtectionProvider().CreateProtector("tests");
        var provider = new JsonSettingsProvider<ProtectedSettings>(path, new SettingsProviderOptions { DataProtector = protector });

        provider.SetSettings(new ProtectedSettings { Password = "p@ss" });

        var stored = JsonNode.Parse(File.ReadAllText(path))!["Password"]!.GetValue<string>();
        stored.Should().NotContain("p@ss");
        protector.Unprotect(stored).Should().Be("p@ss");
        new JsonSettingsProvider<ProtectedSettings>(path, new SettingsProviderOptions { DataProtector = protector })
            .GetSettings().Password.Should().Be("p@ss");
    }

    [Fact]
    public void Protected_property_without_protector_gives_clear_error()
    {
        var path = _temp.File("protected.json", """{ "Password": "x" }""");

        var act = () => new JsonSettingsProvider<ProtectedSettings>(path, new SettingsProviderOptions()).GetSettings();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Protected = true*DataProtector is not set*");
    }

    [Fact]
    public void Cache_keeps_instance_and_reloads_external_changes_into_it()
    {
        var (provider, path) = Provider("""{ "FileName": "readme.txt" }""");
        var settings = provider.GetSettings();

        TempDirectory.Rewrite(path, """{ "FileName": "changed.txt" }""");

        provider.GetSettings().Should().BeSameAs(settings);
        settings.FileName.Should().Be("changed.txt");
    }

    [Fact]
    public void Without_cache_every_access_loads_a_new_instance()
    {
        var (provider, path) = Provider("""{ "FileName": "readme.txt" }""", cache: false);
        var first = provider.GetSettings();

        TempDirectory.Rewrite(path, """{ "FileName": "changed.txt" }""");
        var second = provider.GetSettings();

        second.Should().NotBeSameAs(first);
        first.FileName.Should().Be("readme.txt");
        second.FileName.Should().Be("changed.txt");
    }

    [Fact]
    public void SetSettings_writes_indented_json_and_updates_cache()
    {
        var (provider, path) = Provider("""{ "FileName": "readme.txt" }""");
        var cached = provider.GetSettings();

        provider.SetSettings(new TestSettings { FileName = "new.txt", Mode = Mode.On, OwnerName = "Owner" });

        cached.FileName.Should().Be("new.txt");
        var json = File.ReadAllText(path);
        json.Should().Contain(Environment.NewLine).And.Contain("\"Mode\": \"On\"").And.Contain("\"SomeOwnerNameAlias\": \"Owner\"");
        Directory.GetFiles(_temp.Path).Should().ContainSingle("the temporary file is moved over the original");
    }

    [Fact]
    public void Missing_file_throws_and_empty_file_gives_defaults()
    {
        var missing = new JsonSettingsProvider<TestSettings>(Path.Combine(_temp.Path, "missing.json"), new SettingsProviderOptions());
        var empty = Provider("").Provider;

        var act = () => missing.GetSettings();

        act.Should().Throw<FileNotFoundException>();
        empty.GetSettings().FileName.Should().BeNull();
    }

    [Fact]
    public void Concurrent_reads_and_writes_are_safe()
    {
        var (provider, _) = Provider("""{ "FileName": "0" }""");

        var act = () => Parallel.For(0, 50, i =>
        {
            if (i % 5 == 0)
            {
                provider.SetSettings(new TestSettings { FileName = i.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            _ = provider.GetSettings().FileName;
        });

        act.Should().NotThrow();
    }

    public void Dispose() => _temp.Dispose();
}
