using System.ComponentModel.DataAnnotations;
using Lintel.AspNetCore.Tests.TestUtils;
using Lintel.AspNetCore.Validation;
using Lintel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.AspNetCore.Tests;

public class ValidationAdapterTests
{
    public sealed class Booking
    {
        [Display(Name = "Arrival date")]
        public DateTime From { get; set; }

        [Display(Name = "Departure date")]
        [GreaterThan(nameof(From), PassOnNull = true)]
        public DateTime To { get; set; }

        public string? Country { get; set; }

        [RequiredIf(nameof(Country), "RU")]
        public string? Region { get; set; }

        [RegularExpressionIf(@"^\d{6}$", nameof(Country), "RU")]
        public string? PostCode { get; set; }

        [Required]
        public string? Name { get; set; }
    }

    private static readonly IServiceProvider _services = new ServiceCollection()
        .AddLogging()
        .AddControllersWithViews().Services
        .AddAspNetCoreValidation()
        .BuildServiceProvider();

    private static Dictionary<string, string> ClientAttributes(string property)
    {
        var metadataProvider = _services.GetRequiredService<IModelMetadataProvider>();
        var metadata = metadataProvider.GetMetadataForProperty(typeof(Booking), property);
        var adapterProvider = _services.GetRequiredService<IValidationAttributeAdapterProvider>();
        var actionContext = new ActionContext(TestContextFactory.HttpContext(services: _services), new RouteData(), new ActionDescriptor());

        var attributes = new Dictionary<string, string>();
        var context = new ClientModelValidationContext(actionContext, metadata, metadataProvider, attributes);
        foreach (var attribute in typeof(Booking).GetProperty(property)!.GetCustomAttributes(true).OfType<ValidationAttribute>())
        {
            adapterProvider.GetAttributeAdapter(attribute, stringLocalizer: null)!.AddValidation(context);
        }
        return attributes;
    }

    [Fact]
    public void Is_attribute_emits_rule_message_and_parameters_with_display_names()
        => ClientAttributes(nameof(Booking.To)).Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["data-val"] = "true",
            ["data-val-is"] = "Departure date must be greater than Arrival date.",
            ["data-val-is-relatedproperty"] = "From",
            ["data-val-is-operator"] = "GreaterThan",
            ["data-val-is-passonnull"] = "true",
        });

    [Fact]
    public void RequiredIf_emits_expected_value()
        => ClientAttributes(nameof(Booking.Region)).Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["data-val"] = "true",
            ["data-val-requiredif"] = "Region is required due to Country being equal to RU.",
            ["data-val-requiredif-relatedproperty"] = "Country",
            ["data-val-requiredif-operator"] = "EqualTo",
            ["data-val-requiredif-relatedpropertyvalue"] = "RU",
        });

    [Fact]
    public void RegularExpressionIf_keeps_requiredif_rule_with_pattern()
        => ClientAttributes(nameof(Booking.PostCode)).Should().Contain("data-val-requiredif-pattern", @"^\d{6}$");

    [Fact]
    public void Standard_attributes_keep_standard_adapters()
        => ClientAttributes(nameof(Booking.Name)).Should().ContainKey("data-val-required");

    [Fact]
    public void Adapter_does_not_modify_the_attribute()
    {
        var attribute = (GreaterThanAttribute)typeof(Booking).GetProperty(nameof(Booking.To))!.GetCustomAttributes(typeof(GreaterThanAttribute), true)[0];

        ClientAttributes(nameof(Booking.To));

        attribute.RelatedPropertyDisplayName.Should().BeNull();
        attribute.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void AddAspNetCoreValidation_replaces_adapter_provider_registered_by_mvc()
    {
        var services = new ServiceCollection().AddLogging().AddControllersWithViews().Services.AddAspNetCoreValidation();

        services.Where(d => d.ServiceType == typeof(IValidationAttributeAdapterProvider))
            .Should().ContainSingle().Which.ImplementationType.Should().Be<ModelAwareValidationAttributeAdapterProvider>();
    }
}
