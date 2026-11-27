using System.Xml.Linq;
using Lintel.Settings.Providers;
using Lintel.Settings.Tests.TestUtils;

namespace Lintel.Settings.Tests;

public sealed class XmlSettingsProviderTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private (XmlSettingsProvider<TestSettings> Provider, string Path) Provider(string xml)
    {
        var path = _temp.File("app-settings.xml", xml);
        return (new XmlSettingsProvider<TestSettings>(path, new SettingsProviderOptions()), path);
    }

    [Fact]
    public void Reads_Structr_format_with_typed_values()
    {
        var apiKey = StructrEncryptor.Encrypt("secret", "abcdef");
        var (provider, _) = Provider($"""
            <?xml version="1.0" encoding="utf-8"?>
            <app-settings>
              <FileName>readme.txt</FileName>
              <SomeOwnerNameAlias>Owner name</SomeOwnerNameAlias>
              <Timeout>60</Timeout>
              <ApiKey>{apiKey}</ApiKey>
              <Enabled>true</Enabled>
              <Mode>On</Mode>
              <Smtp><Host>smtp.test</Host><Port>587</Port></Smtp>
              <Tags>a</Tags>
              <Tags>b</Tags>
            </app-settings>
            """);

        var settings = provider.GetSettings();

        settings.FileName.Should().Be("readme.txt");
        settings.OwnerName.Should().Be("Owner name");
        settings.Timeout.Should().Be(60);
        settings.HelpUrl.Should().Be("help.example.com");
        settings.ApiKey.Should().Be("secret");
        settings.Enabled.Should().BeTrue();
        settings.Mode.Should().Be(Mode.On);
        settings.Smtp.Should().BeEquivalentTo(new SmtpSettings { Host = "smtp.test", Port = 587 });
        settings.Tags.Should().Equal("a", "b");
    }

    [Fact]
    public void Empty_element_is_null_and_root_name_is_not_significant()
    {
        var (provider, _) = Provider("<anything><FileName /><Enabled>1</Enabled></anything>");

        var settings = provider.GetSettings();

        settings.FileName.Should().BeNull();
        settings.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Writes_Structr_format_and_reads_it_back()
    {
        var (provider, path) = Provider("<app-settings />");

        provider.SetSettings(new TestSettings
        {
            FileName = "readme.txt",
            OwnerName = "Owner",
            ApiKey = "secret",
            Enabled = true,
            Mode = Mode.On,
            Timeout = 42,
            Smtp = new SmtpSettings { Host = "smtp.test", Port = 25 },
            Tags = ["a", "b"],
        });

        var root = XDocument.Load(path).Root!;
        root.Name.LocalName.Should().Be("app-settings");
        root.Element("FileName")!.Value.Should().Be("readme.txt");
        root.Element("SomeOwnerNameAlias")!.Value.Should().Be("Owner");
        root.Element("Enabled")!.Value.Should().Be("true");
        root.Element("Timeout")!.Value.Should().Be("42");
        root.Element("Smtp")!.Element("Port")!.Value.Should().Be("25");
        root.Elements("Tags").Select(e => e.Value).Should().Equal("a", "b");
        StructrEncryptor.Decrypt(root.Element("ApiKey")!.Value, "abcdef").Should().Be("secret");

        var reloaded = new XmlSettingsProvider<TestSettings>(path, new SettingsProviderOptions()).GetSettings();
        reloaded.Should().BeEquivalentTo(provider.GetSettings());
    }

    public void Dispose() => _temp.Dispose();
}
