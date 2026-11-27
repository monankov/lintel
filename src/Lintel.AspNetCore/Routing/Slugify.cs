using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.AspNetCore.Routing;

/// <summary>
/// Writes route values in kebab-case: <c>OrderItems</c> → <c>Order-Items</c>
/// (combine with <c>RouteOptions.LowercaseUrls</c> for <c>order-items</c>).
/// </summary>
public sealed partial class SlugifyParameterTransformer : IOutboundParameterTransformer
{
    [GeneratedRegex("([a-z])([A-Z])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WordBoundary();

    /// <inheritdoc />
    public string? TransformOutbound(object? value)
        => value?.ToString() is { } text ? WordBoundary().Replace(text, "$1-$2") : null;
}

/// <summary>
/// Registration of <see cref="SlugifyParameterTransformer"/>.
/// </summary>
public static class SlugifyRoutingExtensions
{
    /// <summary>
    /// Applies <see cref="SlugifyParameterTransformer"/> to the <c>[controller]</c> and <c>[action]</c> tokens of attribute routes.
    /// </summary>
    /// <param name="options">The MVC options.</param>
    /// <returns>The same options.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> is <see langword="null"/>.</exception>
    public static MvcOptions AddSlugifyRouting(this MvcOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Conventions.Add(new RouteTokenTransformerConvention(new SlugifyParameterTransformer()));
        return options;
    }

    /// <summary>
    /// Registers <see cref="SlugifyParameterTransformer"/> for conventional routes: <c>{controller:slugify}/{action:slugify}</c>.
    /// </summary>
    /// <param name="options">The route options.</param>
    /// <param name="constraintKey">The name used in route templates.</param>
    /// <returns>The same options.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="constraintKey"/> is empty.</exception>
    public static RouteOptions AddSlugifyRouting(this RouteOptions options, string constraintKey = "slugify")
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(constraintKey);

        options.ConstraintMap[constraintKey] = typeof(SlugifyParameterTransformer);
        return options;
    }
}
