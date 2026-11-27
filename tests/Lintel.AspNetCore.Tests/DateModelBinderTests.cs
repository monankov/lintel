using System.Globalization;
using Lintel.AspNetCore.ModelBinding;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.Primitives;

namespace Lintel.AspNetCore.Tests;

public class DateModelBinderTests
{
    public sealed class Form
    {
        public DateTime When { get; set; }

        public DateTime? WhenOrNull { get; set; }

        public DateOnly Day { get; set; }

        public DateOnly? DayOrNull { get; set; }

        public string? Name { get; set; }
    }

    private static readonly EmptyModelMetadataProvider _metadata = new();

    private static async Task<DefaultModelBindingContext> BindAsync(string property, string? value, params string[] formats)
    {
        var query = value is null ? new QueryCollection() : new QueryCollection(new Dictionary<string, StringValues> { [property] = value });
        var context = new DefaultModelBindingContext
        {
            ModelMetadata = _metadata.GetMetadataForProperty(typeof(Form), property),
            ModelName = property,
            ModelState = new ModelStateDictionary(),
            ValueProvider = new QueryStringValueProvider(BindingSource.Query, query, CultureInfo.InvariantCulture),
        };
        await new DateModelBinder(formats.Length > 0 ? formats : ["dd.MM.yyyy"]).BindModelAsync(context);
        return context;
    }

    [Theory]
    [InlineData(nameof(Form.When), "05.06.2026", "2026-06-05T00:00:00")]
    [InlineData(nameof(Form.When), " 05.06.2026 ", "2026-06-05T00:00:00")]
    [InlineData(nameof(Form.When), "2026-06-05", "2026-06-05T00:00:00")]
    [InlineData(nameof(Form.When), "2026-06-05T14:30", "2026-06-05T14:30:00")]
    [InlineData(nameof(Form.WhenOrNull), "2026-06-05T14:30:15", "2026-06-05T14:30:15")]
    public async Task Binds_date_time_from_format_or_iso(string property, string value, string expected)
    {
        var context = await BindAsync(property, value);

        context.Result.IsModelSet.Should().BeTrue();
        context.Result.Model.Should().Be(DateTime.Parse(expected, CultureInfo.InvariantCulture));
        ((DateTime)context.Result.Model!).Kind.Should().Be(DateTimeKind.Unspecified);
        context.ModelState[property]!.AttemptedValue.Should().Be(value);
    }

    [Theory]
    [InlineData(nameof(Form.Day), "05.06.2026")]
    [InlineData(nameof(Form.DayOrNull), "2026-06-05")]
    public async Task Binds_date_only(string property, string value)
        => (await BindAsync(property, value)).Result.Model.Should().Be(new DateOnly(2026, 6, 5));

    [Fact]
    public async Task Several_formats_are_tried_in_order()
    {
        (await BindAsync(nameof(Form.Day), "06/05/2026", "dd.MM.yyyy", "MM/dd/yyyy")).Result.Model.Should().Be(new DateOnly(2026, 6, 5));
        (await BindAsync(nameof(Form.Day), "05.06.26", "dd.MM.yyyy", "dd.MM.yy")).Result.Model.Should().Be(new DateOnly(2026, 6, 5));
    }

    [Theory]
    [InlineData(nameof(Form.When), "06/05/2026")]
    [InlineData(nameof(Form.Day), "05.06.2026 10:00")]
    [InlineData(nameof(Form.DayOrNull), "31.02.2026")]
    public async Task Other_formats_add_the_standard_error(string property, string value)
    {
        var context = await BindAsync(property, value);

        context.Result.IsModelSet.Should().BeFalse();
        context.ModelState[property]!.Errors.Single().ErrorMessage.Should().Be($"The value '{value}' is not valid for {property}.");
    }

    [Fact]
    public async Task Empty_value_is_null_for_nullable_and_unbound_otherwise()
    {
        var nullable = await BindAsync(nameof(Form.DayOrNull), " ");
        var required = await BindAsync(nameof(Form.Day), "");
        var missing = await BindAsync(nameof(Form.Day), null);

        nullable.Result.Should().Be(ModelBindingResult.Success(null));
        required.Result.IsModelSet.Should().BeFalse();
        required.ModelState[nameof(Form.Day)]!.Errors.Should().BeEmpty();
        missing.ModelState.Should().BeEmpty();
    }

    [Fact]
    public void Provider_handles_date_types_only_and_is_inserted_first()
    {
        var provider = new DateModelBinderProvider(["dd.MM.yyyy"]);

        foreach (var property in new[] { nameof(Form.When), nameof(Form.WhenOrNull), nameof(Form.Day), nameof(Form.DayOrNull) })
        {
            provider.GetBinder(new TestProviderContext(_metadata.GetMetadataForProperty(typeof(Form), property))).Should().BeOfType<DateModelBinder>();
        }
        provider.GetBinder(new TestProviderContext(_metadata.GetMetadataForProperty(typeof(Form), nameof(Form.Name)))).Should().BeNull();

        var options = new MvcOptions();
        options.ModelBinderProviders.Add(new SimpleTypeModelBinderProvider());
        options.UseDateModelBinding("dd.MM.yyyy").ModelBinderProviders[0].Should().BeOfType<DateModelBinderProvider>();
    }

    [Fact]
    public async Task Arguments_are_checked()
    {
        var withoutFormats = () => new DateModelBinder(null!);
        var emptyFormat = () => new DateModelBinder(["dd.MM.yyyy", " "]);
        var withoutOptions = () => ((MvcOptions)null!).UseDateModelBinding("dd.MM.yyyy");
        var withoutContext = () => new DateModelBinderProvider([]).GetBinder(null!);
        var withoutBindingContext = () => new DateModelBinder([]).BindModelAsync(null!);

        withoutFormats.Should().Throw<ArgumentNullException>();
        emptyFormat.Should().Throw<ArgumentException>();
        withoutOptions.Should().Throw<ArgumentNullException>();
        withoutContext.Should().Throw<ArgumentNullException>();
        await withoutBindingContext.Should().ThrowAsync<ArgumentNullException>();
    }

    private sealed class TestProviderContext(ModelMetadata metadata) : ModelBinderProviderContext
    {
        public override BindingInfo BindingInfo { get; } = new();

        public override ModelMetadata Metadata { get; } = metadata;

        public override IModelMetadataProvider MetadataProvider => _metadata;

        public override IModelBinder CreateBinder(ModelMetadata metadata) => throw new NotSupportedException();
    }
}
