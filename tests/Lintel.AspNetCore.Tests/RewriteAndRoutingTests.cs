using Lintel.AspNetCore.Rewrite;
using Lintel.AspNetCore.Routing;
using Lintel.AspNetCore.Tests.TestUtils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.Routing;

namespace Lintel.AspNetCore.Tests;

public class RewriteAndRoutingTests
{
    private static (RuleResult Result, string? Location, int Status) Apply(IRule rule, string path, string method = "GET", string? pathBase = null)
    {
        var httpContext = TestContextFactory.HttpContext(path, "?Id=1");
        httpContext.Request.Method = method;
        if (pathBase is not null)
        {
            httpContext.Request.PathBase = pathBase;
        }

        var context = new RewriteContext { HttpContext = httpContext };
        rule.ApplyRule(context);
        return (context.Result, httpContext.Response.Headers.Location.ToString() is { Length: > 0 } location ? location : null, httpContext.Response.StatusCode);
    }

    private static readonly Func<HttpRequest, bool> _all = _ => true;

    [Theory]
    [InlineData("/Orders/Edit", "https://shop.test/orders/edit?Id=1")]
    [InlineData("/orders/edit", null)]
    public void Lowercase_rule(string path, string? expected)
    {
        var (result, location, status) = Apply(new RedirectToLowercaseRule(_all, 301), path);

        location.Should().Be(expected);
        result.Should().Be(expected is null ? RuleResult.ContinueRules : RuleResult.EndResponse);
        if (expected is not null)
        {
            status.Should().Be(301);
        }
    }

    [Theory]
    [InlineData("/orders", "https://shop.test/orders/?Id=1")]
    [InlineData("/orders/", null)]
    [InlineData("/css/site.css", null)]
    [InlineData("/", null)]
    public void Trailing_slash_rule(string path, string? expected)
        => Apply(new RedirectToTrailingSlashRule(_all, 301), path).Location.Should().Be(expected);

    [Theory]
    [InlineData("/Orders", "https://shop.test/orders/?Id=1")]
    [InlineData("/Orders/", "https://shop.test/orders/?Id=1")]
    [InlineData("/orders", "https://shop.test/orders/?Id=1")]
    [InlineData("/orders/", null)]
    public void Lowercase_trailing_slash_rule(string path, string? expected)
        => Apply(new RedirectToLowercaseTrailingSlashRule(_all, 308), path).Location.Should().Be(expected);

    [Fact]
    public void Rules_apply_to_get_and_head_only_and_respect_filter()
    {
        Apply(new RedirectToLowercaseRule(_all, 301), "/Orders", "HEAD").Result.Should().Be(RuleResult.EndResponse);
        Apply(new RedirectToLowercaseRule(_all, 301), "/Orders", "POST").Result.Should().Be(RuleResult.ContinueRules);
        Apply(new RedirectToLowercaseRule(_ => false, 301), "/Orders").Result.Should().Be(RuleResult.ContinueRules);
    }

    [Fact]
    public void Path_base_is_kept_and_lowercased()
        => Apply(new RedirectToLowercaseRule(_all, 301), "/Orders", pathBase: "/Shop").Location
            .Should().Be("https://shop.test/shop/orders?Id=1");

    [Fact]
    public void Rewrite_options_extensions_add_rules()
    {
        var options = new RewriteOptions()
            .AddRedirectToLowercase()
            .AddRedirectToTrailingSlash()
            .AddRedirectToLowercaseTrailingSlash(_all, 302);

        options.Rules.Select(r => r.GetType()).Should().Equal(
            typeof(RedirectToLowercaseRule), typeof(RedirectToTrailingSlashRule), typeof(RedirectToLowercaseTrailingSlashRule));
    }

    [Theory]
    [InlineData("OrderItems", "Order-Items")]
    [InlineData("orderItems", "order-Items")]
    [InlineData("HTMLParser", "HTMLParser")]
    [InlineData("index", "index")]
    [InlineData(null, null)]
    public void Slugify_transformer(string? value, string? expected)
        => new SlugifyParameterTransformer().TransformOutbound(value).Should().Be(expected);

    [Fact]
    public void AddSlugifyRouting_registers_convention_and_constraint()
    {
        var mvc = new MvcOptions().AddSlugifyRouting();
        var route = new RouteOptions().AddSlugifyRouting().AddSlugifyRouting();

        mvc.Conventions.Should().ContainSingle();
        route.ConstraintMap["slugify"].Should().Be<SlugifyParameterTransformer>();
    }
}
