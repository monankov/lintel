using Lintel.AspNetCore.Client.Alerts;
using Lintel.AspNetCore.TagHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static Lintel.AspNetCore.Tests.TestUtils.TagHelperRunner;

namespace Lintel.AspNetCore.Tests;

public class ClientAlertsTagHelperTests
{
    private sealed class ListAlertProvider(params ClientAlert[] alerts) : IClientAlertProvider
    {
        public List<ClientAlert> Alerts { get; } = [.. alerts];

        public void AddClientAlert(ClientAlert alert) => Alerts.Add(alert);

        public IEnumerable<ClientAlert> GetAllClientAlerts() => Alerts;
    }

    private static string Render(ClientAlertsTagHelper helper, params TagHelperAttribute[] attributes)
    {
        var output = new TagHelperOutput("client-alerts", [.. attributes],
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        helper.Process(Context("client-alerts"), output);
        return TestUtils.TagHelperRunner.Render(output);
    }

    private static ClientAlertsTagHelper Helper(ClientAlertOptions? options = null, params ClientAlert[] alerts)
        => new(new ListAlertProvider(alerts), Options.Create(options ?? new ClientAlertOptions()));

    [Fact]
    public void Renders_bootstrap_alerts_with_close_button_and_keeps_attributes()
    {
        var html = Render(Helper(null, new ClientAlert(ClientAlertTypes.Success, "Saved"), new ClientAlert(ClientAlertTypes.Error, "Failed")),
            new TagHelperAttribute("class", "mt-3"));

        html.Should().Be(
            """<div class="mt-3">"""
            + """<div class="alert alert-success alert-dismissible fade show" role="alert">Saved<button aria-label="Close" class="btn-close" data-bs-dismiss="alert" type="button"></button></div>"""
            + """<div class="alert alert-danger alert-dismissible fade show" role="alert">Failed<button aria-label="Close" class="btn-close" data-bs-dismiss="alert" type="button"></button></div>"""
            + "</div>");
    }

    [Fact]
    public void Renders_nothing_without_alerts()
        => Render(Helper()).Should().BeEmpty();

    [Fact]
    public void Message_is_html_encoded()
        => Render(Helper(null, new ClientAlert("info", "<script>alert('x')</script>")))
            .Should().Contain("&lt;script&gt;alert(&#x27;x&#x27;)&lt;/script&gt;").And.NotContain("<script>");

    [Fact]
    public void Not_dismissible_alert_has_no_close_button()
    {
        var helper = Helper(null, new ClientAlert("warning", "Check"));
        helper.Dismissible = false;

        Render(helper).Should().Be("""<div><div class="alert alert-warning" role="alert">Check</div></div>""");
    }

    [Fact]
    public void Options_change_classes_and_close_label()
    {
        var options = new ClientAlertOptions { CssClass = "notice", DismissibleCssClass = "", CloseButtonLabel = "Dismiss" };
        options.TypeCssClasses["success"] = "notice-ok";

        var html = Render(Helper(options, new ClientAlert("Success", "Saved"), new ClientAlert("Custom", "Other")));

        html.Should().Contain("""<div class="notice notice-ok" role="alert">Saved<button aria-label="Dismiss" """);
        html.Should().Contain("""<div class="notice alert-custom" role="alert">Other""");
    }

    [Fact]
    public void Json_format_renders_escaped_script_data()
    {
        var helper = Helper(null, new ClientAlert("success", "Saved"), new ClientAlert("error", "</script><b>&"));
        helper.Format = ClientAlertsFormat.Json;

        var html = Render(helper, new TagHelperAttribute("id", "alerts"));

        html.Should().Be("""<script id="alerts" type="application/json">[{"type":"success","message":"Saved"},{"type":"error","message":"\u003C/script\u003E\u003Cb\u003E\u0026"}]</script>""");
    }

    [Fact]
    public async Task Shortcut_methods_add_alerts_of_standard_types()
    {
        var provider = new ListAlertProvider();
        var services = new ServiceCollection().AddSingleton<IClientAlertProvider>(provider).BuildServiceProvider();
        var context = new ActionContext { HttpContext = TestUtils.TestContextFactory.HttpContext(services: services) };
        var inner = new EmptyResult();

        foreach (var result in new[] { inner.Success("a"), inner.Info("b"), inner.Warning("c"), inner.Error("d") })
        {
            await result.ExecuteResultAsync(context);
        }

        provider.Alerts.Select(a => (a.Type, a.Message)).Should().Equal(("success", "a"), ("info", "b"), ("warning", "c"), ("error", "d"));
    }

    [Fact]
    public void AddClientAlerts_applies_configuration()
    {
        var options = new ServiceCollection()
            .AddClientAlerts(o => o.CloseButtonLabel = "Закрыть")
            .BuildServiceProvider()
            .GetRequiredService<IOptions<ClientAlertOptions>>().Value;

        options.CloseButtonLabel.Should().Be("Закрыть");
        options.GetTypeCssClass(ClientAlertTypes.Error).Should().Be("alert-danger");
    }

    [Fact]
    public void Constructor_and_arguments_are_checked()
    {
        var withoutProvider = () => new ClientAlertsTagHelper(null!, Options.Create(new ClientAlertOptions()));
        var withoutOptions = () => new ClientAlertsTagHelper(new ListAlertProvider(), null!);
        var withoutType = () => new ClientAlertOptions().GetTypeCssClass(null!);
        var withoutOutput = () => Helper().Process(Context(), null!);

        withoutProvider.Should().Throw<ArgumentNullException>();
        withoutOptions.Should().Throw<ArgumentNullException>();
        withoutType.Should().Throw<ArgumentNullException>();
        withoutOutput.Should().Throw<ArgumentNullException>();
    }
}
