using System.Collections.Concurrent;
using System.Globalization;

namespace Lintel.AspNetCore.Localization;

/// <summary>
/// Keys of the messages in <see cref="ValidationMessageCatalog"/>: model binding messages of MVC and default messages of
/// DataAnnotations attributes.
/// </summary>
public static class ValidationMessageKeys
{
#pragma warning disable CS1591 // The names repeat the MVC accessors and attribute names.
    public const string MissingBindRequiredValue = nameof(MissingBindRequiredValue);
    public const string MissingKeyOrValue = nameof(MissingKeyOrValue);
    public const string MissingRequestBodyRequiredValue = nameof(MissingRequestBodyRequiredValue);
    public const string ValueMustNotBeNull = nameof(ValueMustNotBeNull);
    public const string AttemptedValueIsInvalid = nameof(AttemptedValueIsInvalid);
    public const string NonPropertyAttemptedValueIsInvalid = nameof(NonPropertyAttemptedValueIsInvalid);
    public const string UnknownValueIsInvalid = nameof(UnknownValueIsInvalid);
    public const string NonPropertyUnknownValueIsInvalid = nameof(NonPropertyUnknownValueIsInvalid);
    public const string ValueIsInvalid = nameof(ValueIsInvalid);
    public const string ValueMustBeANumber = nameof(ValueMustBeANumber);
    public const string NonPropertyValueMustBeANumber = nameof(NonPropertyValueMustBeANumber);

    public const string Required = nameof(Required);
    public const string StringLength = nameof(StringLength);
    public const string StringLengthIncludingMinimum = nameof(StringLengthIncludingMinimum);
    public const string Range = nameof(Range);
    public const string MinLength = nameof(MinLength);
    public const string MaxLength = nameof(MaxLength);
    public const string Length = nameof(Length);
    public const string RegularExpression = nameof(RegularExpression);
    public const string Compare = nameof(Compare);
    public const string EmailAddress = nameof(EmailAddress);
    public const string Phone = nameof(Phone);
    public const string Url = nameof(Url);
    public const string CreditCard = nameof(CreditCard);
    public const string FileExtensions = nameof(FileExtensions);
#pragma warning restore CS1591
}

/// <summary>
/// Message formats by key and culture. English (the invariant culture) has the texts of ASP.NET Core and
/// DataAnnotations; Russian (<c>ru</c>) is included. Add languages or replace texts at startup:
/// <c>ValidationMessageCatalog.Default.Set("de", ValidationMessageKeys.Required, "Das Feld {0} ist erforderlich.")</c>.
/// </summary>
/// <remarks>
/// A message is looked up for the culture, then for its parents (<c>ru-RU</c> → <c>ru</c>), then in English.
/// </remarks>
public class ValidationMessageCatalog
{
    // Declared before Default: static fields are initialized in order.
    private static readonly (string Key, string English, string Russian)[] _builtIn =
    [
        (ValidationMessageKeys.MissingBindRequiredValue, "A value for the '{0}' parameter or property was not provided.", "Не указано значение для «{0}»."),
        (ValidationMessageKeys.MissingKeyOrValue, "A value is required.", "Требуется значение."),
        (ValidationMessageKeys.MissingRequestBodyRequiredValue, "A non-empty request body is required.", "Требуется непустое тело запроса."),
        (ValidationMessageKeys.ValueMustNotBeNull, "The value '{0}' is invalid.", "Значение «{0}» недопустимо."),
        (ValidationMessageKeys.AttemptedValueIsInvalid, "The value '{0}' is not valid for {1}.", "Значение «{0}» недопустимо для поля «{1}»."),
        (ValidationMessageKeys.NonPropertyAttemptedValueIsInvalid, "The value '{0}' is not valid.", "Значение «{0}» недопустимо."),
        (ValidationMessageKeys.UnknownValueIsInvalid, "The supplied value is invalid for {0}.", "Указано недопустимое значение для поля «{0}»."),
        (ValidationMessageKeys.NonPropertyUnknownValueIsInvalid, "The supplied value is invalid.", "Указано недопустимое значение."),
        (ValidationMessageKeys.ValueIsInvalid, "The value '{0}' is invalid.", "Значение «{0}» недопустимо."),
        (ValidationMessageKeys.ValueMustBeANumber, "The field {0} must be a number.", "Поле «{0}» должно быть числом."),
        (ValidationMessageKeys.NonPropertyValueMustBeANumber, "The field must be a number.", "Поле должно быть числом."),

        (ValidationMessageKeys.Required, "The {0} field is required.", "Поле «{0}» обязательно для заполнения."),
        (ValidationMessageKeys.StringLength, "The field {0} must be a string with a maximum length of {1}.", "Длина поля «{0}» не должна превышать {1}."),
        (ValidationMessageKeys.StringLengthIncludingMinimum, "The field {0} must be a string with a minimum length of {2} and a maximum length of {1}.", "Длина поля «{0}» должна быть от {2} до {1}."),
        (ValidationMessageKeys.Range, "The field {0} must be between {1} and {2}.", "Значение поля «{0}» должно быть от {1} до {2}."),
        (ValidationMessageKeys.MinLength, "The field {0} must be a string or array type with a minimum length of '{1}'.", "Длина поля «{0}» должна быть не меньше {1}."),
        (ValidationMessageKeys.MaxLength, "The field {0} must be a string or array type with a maximum length of '{1}'.", "Длина поля «{0}» не должна превышать {1}."),
        (ValidationMessageKeys.Length, "The field {0} must be a string or collection type with a minimum length of '{1}' and maximum length of '{2}'.", "Длина поля «{0}» должна быть от {1} до {2}."),
        (ValidationMessageKeys.RegularExpression, "The field {0} must match the regular expression '{1}'.", "Поле «{0}» имеет неверный формат."),
        (ValidationMessageKeys.Compare, "'{0}' and '{1}' do not match.", "Поля «{0}» и «{1}» не совпадают."),
        (ValidationMessageKeys.EmailAddress, "The {0} field is not a valid e-mail address.", "Поле «{0}» должно содержать адрес электронной почты."),
        (ValidationMessageKeys.Phone, "The {0} field is not a valid phone number.", "Поле «{0}» должно содержать номер телефона."),
        (ValidationMessageKeys.Url, "The {0} field is not a valid fully-qualified http, https, or ftp URL.", "Поле «{0}» должно содержать адрес http, https или ftp."),
        (ValidationMessageKeys.CreditCard, "The {0} field is not a valid credit card number.", "Поле «{0}» должно содержать номер банковской карты."),
        (ValidationMessageKeys.FileExtensions, "The {0} field only accepts files with the following extensions: {1}", "Поле «{0}» принимает только файлы с расширениями: {1}."),
    ];

    private readonly ConcurrentDictionary<(string Culture, string Key), string> _messages = new();

    /// <summary>The catalog used when none is given; it has English and Russian messages.</summary>
    public static ValidationMessageCatalog Default { get; } = CreateDefault();

    /// <summary>Creates a catalog with English and Russian messages.</summary>
    /// <returns>A new catalog.</returns>
    public static ValidationMessageCatalog CreateDefault()
    {
        var catalog = new ValidationMessageCatalog();
        foreach (var (key, english, russian) in _builtIn)
        {
            catalog.Set("", key, english);
            catalog.Set("ru", key, russian);
        }
        return catalog;
    }

    /// <summary>Sets the format of a message for a culture; <c>""</c> is English, the fallback of all cultures.</summary>
    /// <param name="culture">The culture name, for example <c>ru</c> or <c>ru-RU</c>.</param>
    /// <param name="key">The key, one of <see cref="ValidationMessageKeys"/>.</param>
    /// <param name="format">The format; the arguments are those of the MVC accessor or of the attribute.</param>
    /// <returns>The same catalog.</returns>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="key"/> or <paramref name="format"/> is empty.</exception>
    public ValidationMessageCatalog Set(string culture, string key, string format)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        _messages[(culture, key)] = format;
        return this;
    }

    /// <summary>Returns the format of a message for <paramref name="culture"/> or its fallback.</summary>
    /// <param name="key">The key.</param>
    /// <param name="culture">The culture; by default <see cref="CultureInfo.CurrentUICulture"/>.</param>
    /// <returns>The format, or <see langword="null"/> if the key is unknown.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="key"/> is <see langword="null"/>.</exception>
    public string? Get(string key, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(key);

        for (var current = culture ?? CultureInfo.CurrentUICulture; ; current = current.Parent)
        {
            if (_messages.TryGetValue((current.Name, key), out var format))
            {
                return format;
            }
            if (current.Name.Length == 0)
            {
                return null;
            }
        }
    }

    /// <summary>Formats a message for the current UI culture.</summary>
    /// <param name="key">The key.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The message.</returns>
    /// <exception cref="ArgumentException">If the key is unknown.</exception>
    public string Format(string key, params object?[] arguments)
        => string.Format(CultureInfo.CurrentCulture,
            Get(key) ?? throw new ArgumentException($"Unknown validation message '{key}'.", nameof(key)), arguments);

    /// <summary>Returns the key whose English format is <paramref name="englishFormat"/>.</summary>
    /// <param name="englishFormat">The English format, as set to <c>ValidationAttribute.ErrorMessage</c>.</param>
    /// <returns>The key, or <see langword="null"/>.</returns>
    public string? FindKey(string? englishFormat)
        => englishFormat is null ? null : _messages.FirstOrDefault(m => m.Key.Culture.Length == 0 && m.Value == englishFormat).Key.Key;
}
