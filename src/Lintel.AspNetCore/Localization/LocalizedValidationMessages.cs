using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Lintel.AspNetCore.Localization;

/// <summary>
/// Sets the default message of standard DataAnnotations attributes that have no message of their own
/// (<c>ErrorMessage</c>, <c>ErrorMessageResourceName</c>) to the English format of <see cref="ValidationMessageCatalog"/>,
/// so that <see cref="ValidationMessageStringLocalizer"/> can translate it.
/// </summary>
/// <param name="catalog">The catalog.</param>
public class ValidationMessageMetadataProvider(ValidationMessageCatalog catalog) : IValidationMetadataProvider
{
    private readonly ValidationMessageCatalog _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

    /// <inheritdoc />
    public void CreateValidationMetadata(ValidationMetadataProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var attribute in context.ValidationMetadata.ValidatorMetadata.OfType<ValidationAttribute>())
        {
            if (attribute.ErrorMessage is null && attribute.ErrorMessageResourceName is null
                && GetKey(attribute) is { } key && _catalog.Get(key, System.Globalization.CultureInfo.InvariantCulture) is { } format)
            {
                attribute.ErrorMessage = format;
            }
        }
    }

    private static readonly Dictionary<Type, string> _keys = new()
    {
        [typeof(RequiredAttribute)] = ValidationMessageKeys.Required,
        [typeof(StringLengthAttribute)] = ValidationMessageKeys.StringLength,
        [typeof(RangeAttribute)] = ValidationMessageKeys.Range,
        [typeof(MinLengthAttribute)] = ValidationMessageKeys.MinLength,
        [typeof(MaxLengthAttribute)] = ValidationMessageKeys.MaxLength,
        [typeof(LengthAttribute)] = ValidationMessageKeys.Length,
        [typeof(RegularExpressionAttribute)] = ValidationMessageKeys.RegularExpression,
        [typeof(System.ComponentModel.DataAnnotations.CompareAttribute)] = ValidationMessageKeys.Compare,
        [typeof(EmailAddressAttribute)] = ValidationMessageKeys.EmailAddress,
        [typeof(PhoneAttribute)] = ValidationMessageKeys.Phone,
        [typeof(UrlAttribute)] = ValidationMessageKeys.Url,
        [typeof(CreditCardAttribute)] = ValidationMessageKeys.CreditCard,
        [typeof(FileExtensionsAttribute)] = ValidationMessageKeys.FileExtensions,
    };

    // Derived attributes use the message of the nearest standard base attribute.
    private static string? GetKey(ValidationAttribute attribute)
    {
        if (attribute is StringLengthAttribute { MinimumLength: > 0 })
        {
            return ValidationMessageKeys.StringLengthIncludingMinimum;
        }
        for (var type = attribute.GetType(); type is not null; type = type.BaseType)
        {
            if (_keys.TryGetValue(type, out var key))
            {
                return key;
            }
        }
        return null;
    }
}

/// <summary>
/// Translates English formats of <see cref="ValidationMessageCatalog"/> to the current UI culture. Other strings go to
/// <paramref name="inner"/> (the localizer of the application, if any) and are returned unchanged when it has no
/// translation either.
/// </summary>
/// <param name="catalog">The catalog.</param>
/// <param name="inner">The localizer of the application.</param>
public class ValidationMessageStringLocalizer(ValidationMessageCatalog catalog, IStringLocalizer? inner = null) : IStringLocalizer
{
    private readonly ValidationMessageCatalog _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

    /// <inheritdoc />
    public LocalizedString this[string name]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(name);

            if (inner?[name] is { ResourceNotFound: false } own)
            {
                return own;
            }
            return _catalog.FindKey(name) is { } key && _catalog.Get(key) is { } format
                ? new LocalizedString(name, format, resourceNotFound: false)
                : new LocalizedString(name, name, resourceNotFound: true);
        }
    }

    /// <inheritdoc />
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var format = this[name];
            return new LocalizedString(name, string.Format(System.Globalization.CultureInfo.CurrentCulture, format.Value, arguments),
                format.ResourceNotFound);
        }
    }

    /// <inheritdoc />
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => inner?.GetAllStrings(includeParentCultures) ?? [];
}

/// <summary>
/// Enabling localized validation messages.
/// </summary>
public static class LocalizedValidationMessagesExtensions
{
    /// <summary>
    /// Takes the model binding messages of MVC (for example "The value '…' is not valid for …") from
    /// <paramref name="catalog"/> in the current UI culture.
    /// </summary>
    /// <param name="options">The MVC options.</param>
    /// <param name="catalog">The catalog; by default <see cref="ValidationMessageCatalog.Default"/>.</param>
    /// <returns>The same options.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> is <see langword="null"/>.</exception>
    public static MvcOptions UseLocalizedModelBindingMessages(this MvcOptions options, ValidationMessageCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var messages = catalog ?? ValidationMessageCatalog.Default;
        var provider = options.ModelBindingMessageProvider;
        provider.SetMissingBindRequiredValueAccessor(x => messages.Format(ValidationMessageKeys.MissingBindRequiredValue, x));
        provider.SetMissingKeyOrValueAccessor(() => messages.Format(ValidationMessageKeys.MissingKeyOrValue));
        provider.SetMissingRequestBodyRequiredValueAccessor(() => messages.Format(ValidationMessageKeys.MissingRequestBodyRequiredValue));
        provider.SetValueMustNotBeNullAccessor(x => messages.Format(ValidationMessageKeys.ValueMustNotBeNull, x));
        provider.SetAttemptedValueIsInvalidAccessor((x, y) => messages.Format(ValidationMessageKeys.AttemptedValueIsInvalid, x, y));
        provider.SetNonPropertyAttemptedValueIsInvalidAccessor(x => messages.Format(ValidationMessageKeys.NonPropertyAttemptedValueIsInvalid, x));
        provider.SetUnknownValueIsInvalidAccessor(x => messages.Format(ValidationMessageKeys.UnknownValueIsInvalid, x));
        provider.SetNonPropertyUnknownValueIsInvalidAccessor(() => messages.Format(ValidationMessageKeys.NonPropertyUnknownValueIsInvalid));
        provider.SetValueIsInvalidAccessor(x => messages.Format(ValidationMessageKeys.ValueIsInvalid, x));
        provider.SetValueMustBeANumberAccessor(x => messages.Format(ValidationMessageKeys.ValueMustBeANumber, x));
        provider.SetNonPropertyValueMustBeANumberAccessor(() => messages.Format(ValidationMessageKeys.NonPropertyValueMustBeANumber));
        return options;
    }

    /// <summary>
    /// Localizes validation messages by the current UI culture: model binding messages, and default messages of
    /// standard DataAnnotations attributes (<c>[Required]</c>, <c>[StringLength]</c>, <c>[Range]</c>, ...) on the server
    /// and in client-side validation. English and Russian are included (<see cref="ValidationMessageCatalog"/>).
    /// </summary>
    /// <remarks>
    /// Attributes with their own <c>ErrorMessage</c> keep it; it still goes through the DataAnnotations localizer of the
    /// application, if one is configured with <c>AddDataAnnotationsLocalization</c>, which also takes precedence for
    /// the standard formats. Enable request localization (<c>app.UseRequestLocalization(...)</c>) to choose the culture.
    /// </remarks>
    /// <param name="builder">The MVC builder.</param>
    /// <param name="catalog">The catalog; by default <see cref="ValidationMessageCatalog.Default"/>.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IMvcBuilder AddLocalizedValidationMessages(this IMvcBuilder builder, ValidationMessageCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var messages = catalog ?? ValidationMessageCatalog.Default;
        builder.Services.AddLocalization();
        builder.Services.Configure<MvcOptions>(options =>
        {
            options.UseLocalizedModelBindingMessages(messages);
            options.ModelMetadataDetailsProviders.Add(new ValidationMessageMetadataProvider(messages));
        });
        builder.Services.PostConfigure<MvcDataAnnotationsLocalizationOptions>(options =>
        {
            var inner = options.DataAnnotationLocalizerProvider;
            options.DataAnnotationLocalizerProvider = (type, factory)
                => new ValidationMessageStringLocalizer(messages, inner?.Invoke(type, factory));
        });
        return builder;
    }
}
