using Lintel.AspNetCore.Referrer;
using Lintel.AspNetCore.Tests.TestUtils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Lintel.AspNetCore.Tests;

public class ReferrerTests
{
    private static DefaultHttpContext Posting(string? referrer, string path = "/orders/edit/1")
    {
        var context = TestContextFactory.HttpContext(path);
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(referrer is null ? [] : new Dictionary<string, StringValues> { [ReferrerDefaults.Key] = referrer });
        return context;
    }

    [Theory]
    [InlineData("/orders?page=3", "/orders?page=3")]
    [InlineData("https://shop.test/orders", "https://shop.test/orders")]
    [InlineData("https://evil.test/phish", "/fallback")]
    [InlineData("//evil.test", "/fallback")]
    [InlineData("javascript:alert(1)", "/fallback")]
    [InlineData("", "/fallback")]
    [InlineData(null, "/fallback")]
    public void GetReferrer_accepts_only_local_posted_referrers(string? posted, string expected)
        => Posting(posted).Request.GetReferrer("/fallback").Should().Be(expected);

    [Fact]
    public void RedirectToReferrer_redirects_to_local_referrer_or_fallback()
    {
        static Controller Controller(HttpContext context) => new TestController
        {
            ControllerContext = new ControllerContext(new ActionContext(context, new RouteData(), new ControllerActionDescriptor())),
        };

        Controller(Posting("/orders?page=3")).RedirectToReferrer("/orders").Url.Should().Be("/orders?page=3");
        Controller(Posting("https://evil.test")).RedirectToReferrer("/orders").Url.Should().Be("/orders");
        Controller(TestContextFactory.HttpContext()).RedirectToReferrer("/orders").Url.Should().Be("/orders");
    }

    [Fact]
    public void RedirectToReferrer_requires_fallback()
    {
        var act = () => new TestController().RedirectToReferrer("");

        act.Should().Throw<ArgumentException>();
    }

    private static string RenderLink(HttpContext context, string? fallback)
    {
        var helper = new ReferrerTagHelper { Referrer = fallback, ViewContext = TestContextFactory.ViewContext(context) };
        var output = TagHelperRunner.Output("a");
        output.Attributes.Add("href", "#");

        helper.Process(TagHelperRunner.Context("a"), output);
        return TagHelperRunner.Render(output);
    }

    [Fact]
    public void TagHelper_uses_local_referer_header_and_stores_it()
    {
        var context = TestContextFactory.HttpContext("/orders/edit/1");
        context.Request.Headers.Referer = "https://shop.test/orders?page=3";

        RenderLink(context, "/orders").Should().Be(
            """<a href="https://shop.test/orders?page=3"></a><input name="__Referrer" type="hidden" value="https://shop.test/orders?page=3">""");
    }

    [Fact]
    public void TagHelper_ignores_external_referer_and_current_page()
    {
        var external = TestContextFactory.HttpContext("/orders/edit/1");
        external.Request.Headers.Referer = "https://evil.test/";
        var self = TestContextFactory.HttpContext("/orders/edit/1");
        self.Request.Headers.Referer = "https://shop.test/orders/edit/1";

        RenderLink(external, "/orders").Should().StartWith("""<a href="/orders">""");
        RenderLink(self, "/orders").Should().StartWith("""<a href="/orders">""");
    }

    [Fact]
    public void TagHelper_prefers_posted_referrer_on_post_back()
    {
        var context = Posting("/orders?page=7");
        context.Request.Headers.Referer = "https://shop.test/orders/edit/1?x=1";

        RenderLink(context, "/orders").Should().StartWith("""<a href="/orders?page=7">""");
    }

    [Fact]
    public void TagHelper_ignores_unsafe_posted_referrer()
        => RenderLink(Posting("javascript:alert(1)"), "/orders").Should().StartWith("""<a href="/orders">""");

    private sealed class TestController : Controller;
}
