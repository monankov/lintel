using System.Net;
using Lintel.AspNetCore.Http;
using Lintel.AspNetCore.Mvc;
using Lintel.AspNetCore.Tests.TestUtils;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.AspNetCore.Tests;

public class HttpTests
{
    [Fact]
    public void IsAjaxRequest_checks_header()
    {
        var ajax = TestContextFactory.HttpContext();
        ajax.Request.Headers.XRequestedWith = "XMLHttpRequest";

        ajax.Request.IsAjaxRequest().Should().BeTrue();
        TestContextFactory.HttpContext().Request.IsAjaxRequest().Should().BeFalse();
    }

    [Fact]
    public void AjaxAttribute_selects_only_ajax_requests()
    {
        var ajax = TestContextFactory.HttpContext();
        ajax.Request.Headers.XRequestedWith = "XMLHttpRequest";
        var attribute = new AjaxAttribute();

        attribute.IsValidForRequest(new RouteContext(ajax), new ActionDescriptor()).Should().BeTrue();
        attribute.IsValidForRequest(new RouteContext(TestContextFactory.HttpContext()), new ActionDescriptor()).Should().BeFalse();
    }

    [Fact]
    public void GetAbsoluteUri_includes_path_base_and_query()
    {
        var context = TestContextFactory.HttpContext("/orders/список", "?page=2");
        context.Request.PathBase = "/shop";

        context.Request.GetAbsoluteUri().Should().Be("https://shop.test/shop/orders/%D1%81%D0%BF%D0%B8%D1%81%D0%BE%D0%BA?page=2");
    }

    [Theory]
    [InlineData("/orders?page=2", true)]
    [InlineData("/", true)]
    [InlineData("~/orders", true)]
    [InlineData("https://shop.test/orders", true)]
    [InlineData("HTTPS://SHOP.TEST/orders", true)]
    [InlineData("//evil.test/phish", false)]
    [InlineData("/\\evil.test", false)]
    [InlineData("~//evil.test", false)]
    [InlineData("https://evil.test/orders", false)]
    [InlineData("http://shop.test/orders", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("orders", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsLocalUrl(string? url, bool expected)
        => TestContextFactory.HttpContext().Request.IsLocalUrl(url).Should().Be(expected);

    [Fact]
    public void GetIpAddress_returns_remote_address_or_null()
    {
        var context = TestContextFactory.HttpContext();

        context.GetIpAddress().Should().BeNull();

        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.7");
        context.GetIpAddress().Should().Be("10.0.0.7");

        context.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:10.0.0.7");
        context.GetIpAddress().Should().Be("10.0.0.7", "the same client as over IPv4");
    }

    [Fact]
    public async Task Authentication_schemes()
    {
        var services = new ServiceCollection();
        services.AddAuthentication().AddCookie();
        var context = TestContextFactory.HttpContext(services: services.BuildServiceProvider());

        (await context.GetAuthenticationSchemesAsync()).Select(s => s.Name).Should().Contain(CookieAuthenticationDefaults.AuthenticationScheme);
        (await context.IsSupportedAuthenticationSchemeAsync("COOKIES")).Should().BeTrue();
        (await context.IsSupportedAuthenticationSchemeAsync("Google")).Should().BeFalse();
    }

    [Fact]
    public void ToRouteValueDictionary_copies_query()
    {
        var query = TestContextFactory.HttpContext(query: "?page=2&sort=name").Request.Query;

        var values = query.ToRouteValueDictionary();

        values.Should().HaveCount(2);
        values["page"]!.ToString().Should().Be("2");
    }

    [Fact]
    public void ToRouteValueDictionary_replaces_key_case_insensitively_or_removes_it()
    {
        var query = TestContextFactory.HttpContext(query: "?Page=2&sort=name").Request.Query;

        var replaced = query.ToRouteValueDictionary("page", 3);
        var removed = query.ToRouteValueDictionary("page", null);

        replaced["page"].Should().Be(3);
        replaced.Should().HaveCount(2);
        removed.Keys.Should().Equal("sort");
    }
}
