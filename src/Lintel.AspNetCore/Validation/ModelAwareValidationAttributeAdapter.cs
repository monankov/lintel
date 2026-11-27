using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Lintel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Localization;

namespace Lintel.AspNetCore.Validation;

/// <summary>
/// Emits unobtrusive client validation attributes for <see cref="ModelAwareValidationAttribute"/>s:
/// <c>data-val-{rule}="message"</c> and <c>data-val-{rule}-{parameter}="value"</c>.
/// </summary>
/// <remarks>
/// The display name of the related property comes from ASP.NET Core model metadata, so <c>[Display]</c>,
/// <c>[ModelMetadataType]</c> and display metadata providers are respected. The attribute itself is not modified.
/// </remarks>
public class ModelAwareValidationAttributeAdapter : AttributeAdapterBase<ModelAwareValidationAttribute>
{
    private readonly IStringLocalizer? _stringLocalizer;

    /// <summary>
    /// Initializes the adapter.
    /// </summary>
    /// <param name="attribute">The attribute.</param>
    /// <param name="stringLocalizer">The localizer of error messages, if data annotations localization is enabled.</param>
    public ModelAwareValidationAttributeAdapter(ModelAwareValidationAttribute attribute, IStringLocalizer? stringLocalizer)
        : base(attribute, stringLocalizer)
    {
        _stringLocalizer = stringLocalizer;
    }

    /// <inheritdoc />
    public override void AddValidation(ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rule = Attribute.ClientTypeName.ToLowerInvariant();

        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, $"data-val-{rule}", GetErrorMessage(context));

        foreach (var (name, value) in Attribute.ClientValidationParameters)
        {
            MergeAttribute(context.Attributes, $"data-val-{rule}-{name}", value switch
            {
                null => "",
                string text => text,
                _ => JsonSerializer.Serialize(value, value.GetType()),
            });
        }
    }

    /// <inheritdoc />
    public override string GetErrorMessage(ModelValidationContextBase validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        var metadata = validationContext.ModelMetadata;
        var name = metadata.GetDisplayName();
        var relatedDisplayName = Attribute is ContingentValidationAttribute contingent
            ? GetRelatedPropertyDisplayName(validationContext.MetadataProvider, metadata.ContainerType, contingent.RelatedProperty)
            : null;

        if (_stringLocalizer is not null
            && !string.IsNullOrEmpty(Attribute.ErrorMessage)
            && string.IsNullOrEmpty(Attribute.ErrorMessageResourceName)
            && Attribute.ErrorMessageResourceType is null)
        {
            var template = _stringLocalizer[Attribute.ErrorMessage].Value;
            return string.Format(CultureInfo.CurrentCulture, template, Attribute.GetErrorMessageArguments(name, relatedDisplayName));
        }

        return Attribute.FormatErrorMessage(name, relatedDisplayName);
    }

    private static string? GetRelatedPropertyDisplayName(IModelMetadataProvider provider, Type? containerType, string relatedProperty)
    {
        var type = containerType;
        ModelMetadata? metadata = null;
        foreach (var part in relatedProperty.Split('.'))
        {
            if (type is null)
            {
                return null;
            }
            metadata = provider.GetMetadataForType(type).Properties[part];
            if (metadata is null)
            {
                return null;
            }
            type = metadata.ModelType;
        }
        return metadata?.DisplayName;
    }
}

/// <summary>
/// Adds <see cref="ModelAwareValidationAttributeAdapter"/> to the standard adapters of ASP.NET Core.
/// </summary>
public class ModelAwareValidationAttributeAdapterProvider : IValidationAttributeAdapterProvider
{
    private readonly ValidationAttributeAdapterProvider _standard = new();

    /// <inheritdoc />
    public IAttributeAdapter? GetAttributeAdapter(ValidationAttribute attribute, IStringLocalizer? stringLocalizer)
        => attribute is ModelAwareValidationAttribute modelAware
            ? new ModelAwareValidationAttributeAdapter(modelAware, stringLocalizer)
            : _standard.GetAttributeAdapter(attribute, stringLocalizer);
}
