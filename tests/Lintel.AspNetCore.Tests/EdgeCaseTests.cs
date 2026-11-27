using System.ComponentModel.DataAnnotations;
using Lintel.AspNetCore.Rewrite;
using Lintel.AspNetCore.TagHelpers;
using Lintel.AspNetCore.Tests.TestUtils;
using Lintel.AspNetCore.Validation;
using Lintel.Collections;
using Lintel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using static Lintel.AspNetCore.Tests.TestUtils.TagHelperRunner;

namespace Lintel.AspNetCore.Tests;

public class EdgeCaseTests
{
    [Theory]
    [InlineData("lowercase", "/Orders", "https://shop.test/orders")]
    [InlineData("slash", "/orders", "https://shop.test/orders/")]
    [InlineData("both", "/Orders", "https://shop.test/orders/")]
    public void Default_rewrite_rules_apply_to_every_request_with_301(string rule, string path, string expected)
    {
        var options = rule switch
        {
            "lowercase" => new RewriteOptions().AddRedirectToLowercase(),
            "slash" => new RewriteOptions().AddRedirectToTrailingSlash(),
            _ => new RewriteOptions().AddRedirectToLowercaseTrailingSlash(),
        };
        var httpContext = TestContextFactory.HttpContext(path);
        httpContext.Request.Method = "GET";
        var context = new RewriteContext { HttpContext = httpContext };

        options.Rules.Single().ApplyRule(context);

        context.HttpContext.Response.StatusCode.Should().Be(301);
        context.HttpContext.Response.Headers.Location.ToString().Should().Be(expected);
    }

    [Fact]
    public void PageInfo_is_suppressed_without_page()
    {
        var output = Output("page-info");

        new PageInfoTagHelper().Process(Context("page-info"), output);

        Render(output).Should().BeEmpty();
    }

    [Fact]
    public void Pagination_renders_delimiters_and_plain_items_without_transform()
    {
        var helper = new PaginationTagHelper(new FakeUrlHelperFactory())
        {
            Options = new PaginationOptions
            {
                PagedEnumerable = new PagedList<int>([1], totalItems: 3, pageNumber: 1, pageSize: 1),
                MaximumPageNumbersToDisplay = null,
                DelimiterBetweenPageNumbers = "|",
                FunctionToTransformEachPageLink = null,
                FunctionToDisplayEachPageNumber = page => $"#{page}",
            },
            ViewContext = TestContextFactory.ViewContext(TestContextFactory.HttpContext("/orders"), TestContextFactory.Mvc()),
        };
        var output = Output("pagination");

        helper.Process(Context("pagination"), output);
        var html = Render(output);

        html.Should().Contain("<li class=\"page-item\">|</li>");
        html.Should().Contain("""<li class="page-item"><a href="/Orders/Index?page=2">#2</a></li>""");
        html.Should().NotContain("page-link");
    }

    [Fact]
    public void RemoveClass_removes_only_non_empty_classes()
    {
        var output = Output("li");
        output.AddClass("active");
        output.AddClass("item");

        output.RemoveClass("active");
        output.RemoveClass(" ");

        Render(output).Should().Be("""<li class="item"></li>""");
        var withoutOutput = () => ((Microsoft.AspNetCore.Razor.TagHelpers.TagHelperOutput)null!).RemoveClass("x");
        withoutOutput.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddClass_and_RemoveClass_accept_several_classes_separated_by_spaces()
    {
        var output = Output("div");

        output.AddClass("alert  alert-danger\tshow");
        output.AddClass("alert");
        output.RemoveClass("show alert");

        Render(output).Should().Be("""<div class="alert-danger"></div>""");
    }

    public sealed class Order
    {
        public Customer? Customer { get; set; }

        public string? Status { get; set; }

        [RequiredIf(nameof(Status), null)]
        [Display(Name = "Comment")]
        public string? Comment { get; set; }

        [RequiredIf("Customer.Name", "VIP", ErrorMessage = "VipNoteRequired")]
        public string? VipNote { get; set; }

        [RequiredIf("Customer.Missing", "x")]
        public string? Broken { get; set; }
    }

    public sealed class Customer
    {
        [Display(Name = "Customer name")]
        public string? Name { get; set; }
    }

    private static readonly IServiceProvider _services = new ServiceCollection()
        .AddLogging()
        .AddControllersWithViews().Services
        .AddAspNetCoreValidation()
        .BuildServiceProvider();

    private static Dictionary<string, string> ClientAttributes(string property, IStringLocalizer? localizer = null)
    {
        var metadataProvider = _services.GetRequiredService<IModelMetadataProvider>();
        var metadata = metadataProvider.GetMetadataForProperty(typeof(Order), property);
        var attribute = typeof(Order).GetProperty(property)!.GetCustomAttributes(true).OfType<ModelAwareValidationAttribute>().Single();
        var attributes = new Dictionary<string, string>();
        var context = new ClientModelValidationContext(
            new ActionContext(TestContextFactory.HttpContext(services: _services), new RouteData(), new ActionDescriptor()),
            metadata, metadataProvider, attributes);

        new ModelAwareValidationAttributeAdapterProvider().GetAttributeAdapter(attribute, localizer)!.AddValidation(context);
        return attributes;
    }

    [Fact]
    public void Null_parameter_is_rendered_as_empty_value()
        => ClientAttributes(nameof(Order.Comment)).Should().Contain("data-val-requiredif-relatedpropertyvalue", "");

    [Fact]
    public void Nested_related_property_uses_its_display_name()
        => ClientAttributes(nameof(Order.VipNote))["data-val-requiredif"].Should().Be("VipNoteRequired");

    [Fact]
    public void Custom_message_is_localized_with_arguments()
    {
        var localizer = Substitute.For<IStringLocalizer>();
        localizer["VipNoteRequired"].Returns(new LocalizedString("VipNoteRequired", "{0} нужна, если {1} = {2}"));

        ClientAttributes(nameof(Order.VipNote), localizer)["data-val-requiredif"]
            .Should().Be("VipNote нужна, если Customer name = VIP");
    }

    [Fact]
    public void Missing_nested_related_property_falls_back_to_its_path()
        => ClientAttributes(nameof(Order.Broken))["data-val-requiredif"]
            .Should().Be("Broken is required due to Customer.Missing being equal to x.");
}
