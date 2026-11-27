using System.Net;
using System.Text.Json;
using Lintel.AspNetCore.IntegrationTests.TestUtils;

namespace Lintel.AspNetCore.IntegrationTests;

/// <summary>Validation, alerts, referrer, rewrite rules and routing through the real ASP.NET Core pipeline.</summary>
public sealed class MvcIntegrationTests(TestApp app) : IClassFixture<TestApp>
{
    private CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Form_inputs_have_client_validation_attributes_with_display_names()
    {
        var page = await app.Browser().GetDocumentAsync("/bookings/create");

        var to = page.QuerySelector("#To")!;
        to.GetAttribute("data-val-is").Should().Be("Departure date must be greater than Arrival date.");
        to.GetAttribute("data-val-is-relatedproperty").Should().Be("From");
        to.GetAttribute("data-val-is-operator").Should().Be("GreaterThan");

        var petName = page.QuerySelector("#PetName")!;
        petName.GetAttribute("data-val-requiredif").Should().Be("Pet name is required due to Has pets being equal to True.");
        petName.GetAttribute("data-val-requiredif-relatedpropertyvalue").Should().Be("true");

        page.QuerySelector("#Name")!.GetAttribute("data-val-required").Should().NotBeNullOrEmpty("standard adapters still work");
    }

    [Fact]
    public async Task Server_validation_reports_errors_with_display_names()
    {
        var browser = app.Browser();
        var form = await browser.GetDocumentAsync("/bookings/create");

        using var response = await browser.SubmitAsync(form, "#booking", new Dictionary<string, string>
        {
            ["Name"] = "Ivanov",
            ["From"] = "2026-05-10",
            ["To"] = "2026-05-01",
            ["HasPets"] = "true",
            ["PetName"] = "",
        });
        var page = await HttpExtensions.ParseAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        page.QuerySelectorAll("#errors li").Select(li => li.TextContent).Should().BeEquivalentTo(
            "Departure date must be greater than Arrival date.",
            "Pet name is required due to Has pets being equal to True.");
    }

    [Fact]
    public async Task Alert_added_to_redirect_is_shown_once_on_the_next_page()
    {
        var browser = app.Browser();
        var form = await browser.GetDocumentAsync("/bookings/create");

        using var saved = await browser.SubmitAsync(form, "#booking", new Dictionary<string, string> { ["Name"] = "Petrov" });
        saved.StatusCode.Should().Be(HttpStatusCode.Redirect);
        saved.Headers.Location!.OriginalString.Should().Be("/bookings");

        var next = await browser.GetDocumentAsync("/bookings");
        var afterThat = await browser.GetDocumentAsync("/bookings");

        next.QuerySelectorAll("#alerts .alert-success").Select(a => a.TextContent).Should().Equal("Booking for Petrov saved");
        afterThat.QuerySelectorAll("#alerts .alert").Should().BeEmpty("TempData is removed after it has been shown");
    }

    [Fact]
    public async Task Back_link_uses_local_referer_and_post_back_returns_there()
    {
        var browser = app.Browser();
        var page = await browser.GetDocumentAsync("/bookings/edit/7",
            request => request.Headers.Referrer = new Uri("https://shop.test/products?page=2"));

        page.QuerySelector("#back")!.GetAttribute("href").Should().Be("https://shop.test/products?page=2");

        using var response = await browser.SubmitAsync(page, "#edit");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("https://shop.test/products?page=2");
    }

    [Theory]
    [InlineData("https://evil.test/phishing")]
    [InlineData("//evil.test")]
    [InlineData("javascript:alert(1)")]
    public async Task Referrer_cannot_redirect_outside_the_application(string referrer)
    {
        var browser = app.Browser();
        var page = await browser.GetDocumentAsync("/bookings/edit/7",
            request => request.Headers.TryAddWithoutValidation("Referer", "https://evil.test/"));

        page.QuerySelector("#back")!.GetAttribute("href").Should().Be("/bookings");

        using var response = await browser.SubmitAsync(page, "#edit", tamper: fields => fields["__Referrer"] = referrer);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/bookings");
    }

    [Theory]
    [InlineData("/Legacy/Old-Page", "https://shop.test/legacy/old-page/")]
    [InlineData("/legacy/page", "https://shop.test/legacy/page/")]
    public async Task Legacy_urls_are_redirected_to_lower_case_with_trailing_slash(string url, string expected)
    {
        using var response = await app.Browser().GetAsync(url, Token);

        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.ToString().Should().Be(expected);
    }

    [Fact]
    public async Task Rewrite_rule_filter_leaves_other_urls_alone()
    {
        using var response = await app.Browser().GetAsync("/Products", Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Slugified_routes_are_matched_and_generated()
    {
        using var response = await app.Browser().GetAsync("/order-items/recent-list", Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Token)).Should().Be("/order-items/recent-list");
    }

    [Fact]
    public async Task Partial_view_is_rendered_into_json()
    {
        using var response = await app.Browser().GetAsync("/products/row/3", Token);

        var html = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token)).RootElement.GetProperty("html").GetString();
        html.Should().Contain("<tr data-id=\"3\"><td>Product 03</td><td>23</td></tr>");
    }

    [Fact]
    public async Task Ajax_action_is_selected_only_for_ajax_requests()
    {
        var browser = app.Browser();

        using var plain = await browser.GetAsync("/products/count", Token);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/products/count");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        using var ajax = await browser.SendAsync(request, Token);

        plain.StatusCode.Should().Be(HttpStatusCode.NotFound);
        ajax.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ajax.Content.ReadAsStringAsync(Token)).Should().Be("25");
    }

    [Theory]
    [InlineData("Taken", "This name is taken.", true)]
    [InlineData("Crash", "The form could not be saved. Please try again.", false)]
    public async Task Exception_of_form_action_renders_the_form_again_with_entered_values(string name, string error, bool fieldError)
    {
        var browser = app.Browser();
        var form = await browser.GetDocumentAsync("/bookings/create");

        using var response = await browser.SubmitAsync(form, "#booking", new Dictionary<string, string> { ["Name"] = name, ["PetName"] = "Rex" });
        var page = await HttpExtensions.ParseAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        page.QuerySelector("#Name")!.GetAttribute("value").Should().Be(name);
        page.QuerySelector("#PetName")!.GetAttribute("value").Should().Be("Rex");
        page.QuerySelectorAll("#summary-all li").Select(li => li.TextContent).Should().Equal(error);
        page.QuerySelector("#Name")!.ClassList.Contains("is-invalid").Should().Be(fieldError);
        (page.QuerySelector("#summary") is not null).Should().Be(!fieldError, "a general error belongs to the form");
    }

    [Theory]
    [InlineData("01.05.2026", "02.05.2026", null)]
    [InlineData("2026-05-01", "2026-05-02", null)]
    [InlineData("2026/05/01", "02.05.2026", "The value '2026/05/01' is not valid for Arrival date.")]
    public async Task Dates_are_bound_from_configured_format_and_iso(string from, string to, string? error)
    {
        var browser = app.Browser();
        var form = await browser.GetDocumentAsync("/bookings/create");

        using var response = await browser.SubmitAsync(form, "#booking", new Dictionary<string, string> { ["Name"] = "Petrov", ["From"] = from, ["To"] = to });

        if (error is null)
        {
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            return;
        }
        var page = await HttpExtensions.ParseAsync(response);
        page.QuerySelectorAll("#summary-all li").Select(li => li.TextContent).Should().Contain(error);
    }

    [Fact]
    public async Task Validation_messages_follow_the_request_culture()
    {
        var browser = app.Browser();
        browser.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU");
        var form = await browser.GetDocumentAsync("/bookings/create");

        form.QuerySelector("#Name")!.GetAttribute("data-val-required").Should().Be("Поле «Guest name» обязательно для заполнения.");

        using var response = await browser.SubmitAsync(form, "#booking", new Dictionary<string, string> { ["Name"] = "", ["From"] = "2026/05/01" });
        var page = await HttpExtensions.ParseAsync(response);

        page.QuerySelectorAll("#summary-all li").Select(li => li.TextContent).Should().Contain(
        [
            "Поле «Guest name» обязательно для заполнения.",
            "Значение «2026/05/01» недопустимо для поля «Arrival date».",
        ]);
    }

    [Fact]
    public async Task Validation_messages_are_english_by_default()
    {
        var form = await app.Browser().GetDocumentAsync("/bookings/create");

        form.QuerySelector("#Name")!.GetAttribute("data-val-required").Should().Be("The Guest name field is required.");
    }
}
