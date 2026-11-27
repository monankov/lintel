using Lintel.Settings.Providers;
using Lintel.Settings.Tests.TestUtils;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.Settings.Tests;

public sealed class ConfigurationTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void AddJson_registers_configuration_and_configurator()
    {
        var path = _temp.File("settings.json", """{ "FileName": "readme.txt" }""");
        var services = new ServiceCollection();
        services.AddConfiguration().AddJson<TestSettings>(path);
        var provider = services.BuildServiceProvider();

        var configuration = provider.GetRequiredService<IConfiguration<TestSettings>>();
        configuration.Settings.FileName.Should().Be("readme.txt");

        provider.GetRequiredService<IConfigurator<TestSettings>>().Configure(settings => settings.FileName = "changed.txt");

        configuration.Settings.FileName.Should().Be("changed.txt");
        File.ReadAllText(path).Should().Contain("changed.txt");
    }

    [Fact]
    public void AddXml_registers_configuration_and_configurator()
    {
        var path = _temp.File("settings.xml", "<settings><FileName>readme.txt</FileName><Enabled>true</Enabled></settings>");
        var services = new ServiceCollection();
        services.AddConfiguration().AddXml<TestSettings>(path, options => options.Cache = false);
        var provider = services.BuildServiceProvider();

        var configuration = provider.GetRequiredService<IConfiguration<TestSettings>>();
        configuration.Settings.Should().Match<TestSettings>(s => s.FileName == "readme.txt" && s.Enabled);

        provider.GetRequiredService<IConfigurator<TestSettings>>().Configure(settings => settings.FileName = "changed.txt");

        configuration.Settings.FileName.Should().Be("changed.txt");
        System.Xml.Linq.XDocument.Load(path).Root!.Element("FileName")!.Value.Should().Be("changed.txt");
    }

    [Fact]
    public void AddJson_with_service_provider_can_set_data_protector()
    {
        var path = _temp.File("protected.json", "{}");
        var services = new ServiceCollection().AddDataProtection().Services;
        services.AddConfiguration().AddJson<ProtectedSettings>(path, (sp, options) =>
            options.DataProtector = sp.GetRequiredService<IDataProtectionProvider>().CreateProtector("settings"));
        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IConfigurator<ProtectedSettings>>().Configure(s => s.Password = "p@ss");

        File.ReadAllText(path).Should().NotContain("p@ss");
        provider.GetRequiredService<IConfiguration<ProtectedSettings>>().Settings.Password.Should().Be("p@ss");
    }

    [Fact]
    public void First_registration_of_a_settings_type_wins()
    {
        var services = new ServiceCollection();
        services.AddConfiguration()
            .AddInMemory(new TestSettings { FileName = "first" })
            .AddInMemory(new TestSettings { FileName = "second" });

        services.BuildServiceProvider().GetRequiredService<IConfiguration<TestSettings>>().Settings.FileName.Should().Be("first");
    }

    [Fact]
    public void InMemory_provider_keeps_changes()
    {
        var services = new ServiceCollection();
        services.AddConfiguration().AddInMemory(new TestSettings { FileName = "a" });
        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IConfigurator<TestSettings>>().Configure(s => s.SetNote("note"));

        provider.GetRequiredService<IConfiguration<TestSettings>>().Settings.InternalNote.Should().Be("note");
    }

    [Fact]
    public void Custom_provider_is_supported()
    {
        var custom = new InMemorySettingsProvider<TestSettings>(new TestSettings { FileName = "custom" }, new SettingsProviderOptions());
        var services = new ServiceCollection();
        services.AddConfiguration().AddProvider(custom);

        services.BuildServiceProvider().GetRequiredService<IConfiguration<TestSettings>>().Settings.FileName.Should().Be("custom");
    }

    [Fact]
    public void Arguments_are_validated()
    {
        var configurator = new Configurator<TestSettings>(new ConfigurationOptions<TestSettings>(
            new InMemorySettingsProvider<TestSettings>(new TestSettings(), new SettingsProviderOptions())));

        var withoutChanges = () => configurator.Configure(null!);
        var withoutProvider = () => new ConfigurationOptions<TestSettings>(null!);
        var withoutServices = () => ((IServiceCollection)null!).AddConfiguration();

        withoutChanges.Should().Throw<ArgumentNullException>();
        withoutProvider.Should().Throw<ArgumentNullException>();
        withoutServices.Should().Throw<ArgumentNullException>();
    }

    public void Dispose() => _temp.Dispose();
}
