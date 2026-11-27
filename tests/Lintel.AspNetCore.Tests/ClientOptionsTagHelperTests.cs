using Lintel.AspNetCore.Client.Options;
using Lintel.AspNetCore.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Routing;
using static Lintel.AspNetCore.Tests.TestUtils.TagHelperRunner;

namespace Lintel.AspNetCore.Tests;

public class ClientOptionsTagHelperTests
{
    private sealed class DictionaryOptionProvider : IClientOptionProvider
    {
        private readonly Dictionary<string, IReadOnlyDictionary<string, object?>> _options = [];

        public void AddClientOptions(string key, Dictionary<string, object?> options) => _options[key] = options;

        public IReadOnlyDictionary<string, object?> GetClientOptions(string key) => _options[key];

        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> GetAllClientOptions() => _options;

        public string BuildClientOptionsKey(RouteData routeData) => "key";
    }

    private static string Render(IClientOptionProvider provider)
    {
        var output = new TagHelperOutput("client-options", [new TagHelperAttribute("id", "client-options")],
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        new ClientOptionsTagHelper(provider).Process(Context("client-options"), output);
        return TestUtils.TagHelperRunner.Render(output);
    }

    [Fact]
    public void Renders_options_by_key_as_json_script()
    {
        var provider = new DictionaryOptionProvider();
        provider.AddClientOptions("orders.index", new() { ["pageSize"] = 20, ["filter"] = new { ShowArchived = true } });
        provider.AddClientOptions("layout", new() { ["Theme"] = "dark", ["empty"] = null });

        Render(provider).Should().Be(
            """<script id="client-options" type="application/json">"""
            + """{"orders.index":{"pageSize":20,"filter":{"showArchived":true}},"layout":{"Theme":"dark","empty":null}}"""
            + "</script>");
    }

    [Fact]
    public void Values_cannot_close_the_script_element()
    {
        var provider = new DictionaryOptionProvider();
        provider.AddClientOptions("page", new() { ["title"] = "</script><script>alert(1)</script>" });

        Render(provider).Should().NotContain("</script><script>").And.Contain("\\u003C/script\\u003E");
    }

    [Fact]
    public void Renders_nothing_without_options()
        => Render(new DictionaryOptionProvider()).Should().BeEmpty();

    [Fact]
    public void Arguments_are_checked()
    {
        var withoutProvider = () => new ClientOptionsTagHelper(null!);
        var withoutOutput = () => new ClientOptionsTagHelper(new DictionaryOptionProvider()).Process(Context(), null!);

        withoutProvider.Should().Throw<ArgumentNullException>();
        withoutOutput.Should().Throw<ArgumentNullException>();
    }
}
