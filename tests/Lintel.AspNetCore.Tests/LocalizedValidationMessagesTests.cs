using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Lintel.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Lintel.AspNetCore.Tests;

public sealed class LocalizedValidationMessagesTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }

    private static void UseCulture(string name) => CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(name);

    public sealed class Form
    {
        [Required]
        public string? Name { get; set; }

        [StringLength(10)]
        public string? Code { get; set; }

        [StringLength(10, MinimumLength = 2)]
        public string? Title { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [Required(ErrorMessage = "Enter the e-mail.")]
        [EmailAddress]
        public string? Email { get; set; }

        [MinLength(1), MaxLength(3), Length(1, 3)]
        public string[]? Tags { get; set; }

        [RegularExpression("^[0-9]+$"), Phone, Url, CreditCard, FileExtensions]
        public string? Misc { get; set; }

        [System.ComponentModel.DataAnnotations.Compare(nameof(Name))]
        public string? NameAgain { get; set; }

        [Required(ErrorMessageResourceType = typeof(Form), ErrorMessageResourceName = nameof(OwnMessage))]
        public string? FromResource { get; set; }

        public string Comment { get; set; } = "";

        public static string OwnMessage => "Own message.";
    }

    [Theory]
    [InlineData("ru-RU", "Поле «{0}» обязательно для заполнения.")]
    [InlineData("ru", "Поле «{0}» обязательно для заполнения.")]
    [InlineData("de-DE", "The {0} field is required.")]
    [InlineData("", "The {0} field is required.")]
    public void Catalog_falls_back_to_parent_culture_and_english(string culture, string expected)
        => ValidationMessageCatalog.Default.Get(ValidationMessageKeys.Required, new CultureInfo(culture)).Should().Be(expected);

    [Fact]
    public void Catalog_can_be_extended_and_reports_unknown_keys()
    {
        var catalog = ValidationMessageCatalog.CreateDefault()
            .Set("de", ValidationMessageKeys.Required, "Das Feld {0} ist erforderlich.")
            .Set("", "Custom", "Custom {0}.");
        UseCulture("de-AT");

        catalog.Format(ValidationMessageKeys.Required, "Name").Should().Be("Das Feld Name ist erforderlich.");
        catalog.Format("Custom", 1).Should().Be("Custom 1.");
        catalog.Get("Unknown").Should().BeNull();
        catalog.FindKey("The {0} field is required.").Should().Be(ValidationMessageKeys.Required);
        catalog.FindKey("Enter the e-mail.").Should().BeNull();
        catalog.FindKey(null).Should().BeNull();
        ValidationMessageCatalog.Default.Get(ValidationMessageKeys.Required, new CultureInfo("de")).Should().StartWith("The", "a new catalog does not change the default one");

        var unknown = () => catalog.Format("Unknown");
        unknown.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Model_binding_messages_follow_the_ui_culture()
    {
        var provider = new MvcOptions().UseLocalizedModelBindingMessages().ModelBindingMessageProvider;

        UseCulture("ru-RU");
        string[] russian =
        [
            provider.MissingBindRequiredValueAccessor("id"),
            provider.MissingKeyOrValueAccessor(),
            provider.MissingRequestBodyRequiredValueAccessor(),
            provider.ValueMustNotBeNullAccessor("x"),
            provider.AttemptedValueIsInvalidAccessor("abc", "Дата"),
            provider.NonPropertyAttemptedValueIsInvalidAccessor("abc"),
            provider.UnknownValueIsInvalidAccessor("Дата"),
            provider.NonPropertyUnknownValueIsInvalidAccessor(),
            provider.ValueIsInvalidAccessor("abc"),
            provider.ValueMustBeANumberAccessor("Цена"),
            provider.NonPropertyValueMustBeANumberAccessor(),
        ];
        russian.Should().Equal(
            "Не указано значение для «id».", "Требуется значение.", "Требуется непустое тело запроса.", "Значение «x» недопустимо.",
            "Значение «abc» недопустимо для поля «Дата».", "Значение «abc» недопустимо.", "Указано недопустимое значение для поля «Дата».",
            "Указано недопустимое значение.", "Значение «abc» недопустимо.", "Поле «Цена» должно быть числом.", "Поле должно быть числом.");

        UseCulture("en-US");
        provider.AttemptedValueIsInvalidAccessor("abc", "Date").Should().Be("The value 'abc' is not valid for Date.");
    }

    private static ModelMetadata Metadata(string property)
        => new ServiceCollection().AddLogging().AddMvc().AddLocalizedValidationMessages().Services.BuildServiceProvider()
            .GetRequiredService<IModelMetadataProvider>().GetMetadataForProperty(typeof(Form), property);

    private static string? Message<TAttribute>(string property) where TAttribute : ValidationAttribute
        => Metadata(property).ValidatorMetadata.OfType<TAttribute>().Single().ErrorMessage;

    [Fact]
    public void Standard_attributes_without_own_message_get_the_english_format()
    {
        Message<RequiredAttribute>(nameof(Form.Name)).Should().Be("The {0} field is required.");
        Message<StringLengthAttribute>(nameof(Form.Code)).Should().Be("The field {0} must be a string with a maximum length of {1}.");
        Message<StringLengthAttribute>(nameof(Form.Title)).Should().Contain("minimum length of {2}");
        Message<RangeAttribute>(nameof(Form.Rating)).Should().Be("The field {0} must be between {1} and {2}.");
        Message<EmailAddressAttribute>(nameof(Form.Email)).Should().Be("The {0} field is not a valid e-mail address.");
        Message<MinLengthAttribute>(nameof(Form.Tags)).Should().Contain("minimum length");
        Message<MaxLengthAttribute>(nameof(Form.Tags)).Should().Contain("maximum length");
        Message<LengthAttribute>(nameof(Form.Tags)).Should().Contain("collection type");
        Message<RegularExpressionAttribute>(nameof(Form.Misc)).Should().Contain("regular expression");
        Message<PhoneAttribute>(nameof(Form.Misc)).Should().Contain("phone number");
        Message<UrlAttribute>(nameof(Form.Misc)).Should().Contain("URL");
        Message<CreditCardAttribute>(nameof(Form.Misc)).Should().Contain("credit card");
        Message<FileExtensionsAttribute>(nameof(Form.Misc)).Should().Contain("extensions");
        Message<System.ComponentModel.DataAnnotations.CompareAttribute>(nameof(Form.NameAgain)).Should().Be("'{0}' and '{1}' do not match.");
    }

    [Fact]
    public void Own_messages_and_resources_are_kept()
    {
        Message<RequiredAttribute>(nameof(Form.Email)).Should().Be("Enter the e-mail.");
        Message<RequiredAttribute>(nameof(Form.FromResource)).Should().BeNull();
        Metadata(nameof(Form.Comment)).ValidatorMetadata.OfType<RequiredAttribute>().Single().ErrorMessage
            .Should().Be("The {0} field is required.", "the implicit [Required] of a non-nullable reference is localized too");
    }

    [Fact]
    public void Localizer_translates_catalog_formats_and_prefers_the_application_localizer()
    {
        var inner = Substitute.For<IStringLocalizer>();
        inner["The {0} field is required."].Returns(new LocalizedString("The {0} field is required.", "Own {0}.", resourceNotFound: false));
        inner[Arg.Is<string>(s => s != "The {0} field is required.")].Returns(c => new LocalizedString(c.Arg<string>(), c.Arg<string>(), resourceNotFound: true));
        inner.GetAllStrings(true).Returns([new LocalizedString("a", "b")]);
        UseCulture("ru");

        var withInner = new ValidationMessageStringLocalizer(ValidationMessageCatalog.Default, inner);
        var alone = new ValidationMessageStringLocalizer(ValidationMessageCatalog.Default);

        withInner["The {0} field is required."].Value.Should().Be("Own {0}.");
        withInner["The field {0} must be between {1} and {2}.", "Оценка", 1, 5].Value.Should().Be("Значение поля «Оценка» должно быть от 1 до 5.");
        alone["The {0} field is required."].Value.Should().Be("Поле «{0}» обязательно для заполнения.");
        var unknown = alone["Enter the e-mail."];
        unknown.Value.Should().Be("Enter the e-mail.");
        unknown.ResourceNotFound.Should().BeTrue();
        withInner.GetAllStrings(true).Should().ContainSingle();
        alone.GetAllStrings(true).Should().BeEmpty();
    }

    [Fact]
    public void Application_localizer_provider_is_wrapped()
    {
        var services = new ServiceCollection().AddLogging().AddMvc().AddDataAnnotationsLocalization().AddLocalizedValidationMessages().Services
            .BuildServiceProvider();
        var options = services.GetRequiredService<IOptions<MvcDataAnnotationsLocalizationOptions>>().Value;

        options.DataAnnotationLocalizerProvider(typeof(Form), services.GetRequiredService<IStringLocalizerFactory>())
            .Should().BeOfType<ValidationMessageStringLocalizer>();
    }

    [Fact]
    public void Arguments_are_checked()
    {
        var actions = new Action[]
        {
            () => ValidationMessageCatalog.Default.Set(null!, "k", "f"),
            () => ValidationMessageCatalog.Default.Set("", " ", "f"),
            () => ValidationMessageCatalog.Default.Set("", "k", ""),
            () => ValidationMessageCatalog.Default.Get(null!),
            () => ((MvcOptions)null!).UseLocalizedModelBindingMessages(),
            () => ((IMvcBuilder)null!).AddLocalizedValidationMessages(),
            () => _ = new ValidationMessageMetadataProvider(null!),
            () => new ValidationMessageMetadataProvider(ValidationMessageCatalog.Default).CreateValidationMetadata(null!),
            () => _ = new ValidationMessageStringLocalizer(null!),
            () => _ = new ValidationMessageStringLocalizer(ValidationMessageCatalog.Default)[null!],
        };
        foreach (var action in actions)
        {
            action.Should().Throw<ArgumentException>();
        }
    }
}
