using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Lintel.AspNetCore.ModelBinding;

/// <summary>
/// Binds <see cref="DateTime"/>, <see cref="DateOnly"/> and their nullable versions from the given formats,
/// independently of the request culture. ISO dates (<c>yyyy-MM-dd</c>, and <c>yyyy-MM-ddTHH:mm[:ss]</c> for
/// <see cref="DateTime"/>) are always accepted, because <c>&lt;input type="date"&gt;</c> and
/// <c>&lt;input type="datetime-local"&gt;</c> send them.
/// </summary>
/// <remarks>
/// An empty value binds <see langword="null"/> to a nullable property and leaves a non-nullable one unbound (validation
/// then reports it as required). A value in another format adds the standard error of
/// <c>ModelBindingMessageProvider.AttemptedValueIsInvalidAccessor</c>, so it is localized like other binding
/// errors. <see cref="DateTime"/> values have <see cref="DateTimeKind.Unspecified"/>.
/// </remarks>
public class DateModelBinder : IModelBinder
{
    private static readonly string[] _isoDateFormats = ["yyyy-MM-dd"];
    private static readonly string[] _isoDateTimeFormats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.FFFFFFF"];

    private readonly string[] _dateFormats;
    private readonly string[] _dateTimeFormats;

    /// <summary>
    /// Initializes the binder.
    /// </summary>
    /// <param name="formats">The accepted formats, for example <c>dd.MM.yyyy</c>, tried in order before ISO.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="formats"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If a format is empty.</exception>
    public DateModelBinder(IEnumerable<string> formats)
    {
        ArgumentNullException.ThrowIfNull(formats);

        var list = formats.ToList();
        if (list.Exists(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("A date format cannot be empty.", nameof(formats));
        }

        _dateFormats = [.. list, .. _isoDateFormats];
        _dateTimeFormats = [.. list, .. _isoDateTimeFormats];
    }

    /// <inheritdoc />
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var modelName = bindingContext.ModelName;
        var valueResult = bindingContext.ValueProvider.GetValue(modelName);
        if (valueResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(modelName, valueResult);
        var metadata = bindingContext.ModelMetadata;
        var text = valueResult.FirstValue?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            if (metadata.IsReferenceOrNullableType)
            {
                bindingContext.Result = ModelBindingResult.Success(null);
            }
            return Task.CompletedTask;
        }

        if (TryParse(text, metadata.UnderlyingOrModelType, out var value))
        {
            bindingContext.Result = ModelBindingResult.Success(value);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(modelName,
                metadata.ModelBindingMessageProvider.AttemptedValueIsInvalidAccessor(valueResult.FirstValue!, metadata.GetDisplayName()));
        }
        return Task.CompletedTask;
    }

    private bool TryParse(string text, Type type, out object? value)
    {
        const DateTimeStyles styles = DateTimeStyles.AllowWhiteSpaces;
        if (type == typeof(DateOnly))
        {
            var parsed = DateOnly.TryParseExact(text, _dateFormats, CultureInfo.InvariantCulture, styles, out var date);
            value = date;
            return parsed;
        }

        var result = DateTime.TryParseExact(text, _dateTimeFormats, CultureInfo.InvariantCulture, styles, out var dateTime);
        value = dateTime;
        return result;
    }
}

/// <summary>
/// Provides <see cref="DateModelBinder"/> for <see cref="DateTime"/>, <see cref="DateOnly"/> and their nullable versions.
/// </summary>
/// <param name="formats">The accepted formats.</param>
public class DateModelBinderProvider(IEnumerable<string> formats) : IModelBinderProvider
{
    private readonly DateModelBinder _binder = new(formats);

    /// <inheritdoc />
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var type = context.Metadata.UnderlyingOrModelType;
        return type == typeof(DateTime) || type == typeof(DateOnly) ? _binder : null;
    }
}

/// <summary>
/// Enabling <see cref="DateModelBinder"/>: <c>services.AddControllersWithViews(o =&gt; o.UseDateModelBinding("dd.MM.yyyy"))</c>.
/// </summary>
public static class DateModelBindingMvcOptionsExtensions
{
    /// <summary>
    /// Binds dates from <paramref name="formats"/> (and ISO) before the standard binders.
    /// </summary>
    /// <param name="options">The MVC options.</param>
    /// <param name="formats">The accepted formats, for example <c>dd.MM.yyyy</c>.</param>
    /// <returns>The same options.</returns>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If a format is empty.</exception>
    public static MvcOptions UseDateModelBinding(this MvcOptions options, params string[] formats)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.ModelBinderProviders.Insert(0, new DateModelBinderProvider(formats));
        return options;
    }
}
