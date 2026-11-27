using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Lintel.Navigation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.AspNetCore.Navigation;

/// <summary>
/// Maps the current route value to the value of a link: <c>{ "From": "(.+)Invoices", "To": "{0}Orders" }</c>
/// turns the controller <c>SalesInvoices</c> into <c>SalesOrders</c>. <see cref="From"/> is a regular expression
/// that must match the whole value; its groups are the arguments of <see cref="To"/> (a format string).
/// </summary>
public class RouteValueMap
{
    /// <summary>The pattern of the current value.</summary>
    public string From { get; set; } = "";

    /// <summary>The format of the link value; <c>{0}</c> is the first group of <see cref="From"/>.</summary>
    public string To { get; set; } = "";
}

/// <summary>
/// A breadcrumb that is active on the pages of its route and links to it. See <see cref="RouteBreadcrumb{TBreadcrumb}"/>.
/// </summary>
public interface IRouteBreadcrumb
{
    /// <summary>The identifier.</summary>
    string? Id { get; }

    /// <summary>The text.</summary>
    string? Title { get; }

    /// <summary>Whether this is the breadcrumb of the current page.</summary>
    bool IsActive { get; }

    /// <summary>The area: a name or a pattern; empty means "no area".</summary>
    string? Area { get; }

    /// <summary>Maps the current area to the area of the link.</summary>
    RouteValueMap? AreaMap { get; }

    /// <summary>The controller: a name or a pattern; empty matches any controller.</summary>
    string? Controller { get; }

    /// <summary>Maps the current controller to the controller of the link.</summary>
    RouteValueMap? ControllerMap { get; }

    /// <summary>The action: a name or a pattern.</summary>
    string? Action { get; }

    /// <summary>Maps the current action to the action of the link.</summary>
    RouteValueMap? ActionMap { get; }

    /// <summary>The Razor page (<c>/Orders/Edit</c>), used instead of controller and action.</summary>
    string? Page { get; }

    /// <summary>Values that the route or query of the current request must have; they are also added to the link.</summary>
    IDictionary<string, string>? RouteValues { get; }

    /// <summary>Values of the current route that the link keeps (for example <c>id</c>); other current values are cleared.</summary>
    IList<string>? PreservedRouteValues { get; }

    /// <summary>Values added to the link when it has no value for them.</summary>
    IDictionary<string, string>? DefaultRouteValues { get; }
}

/// <summary>
/// A breadcrumb bound to a route. Load the tree with <c>Lintel.Navigation</c> (for example from JSON), activate it with
/// <see cref="RouteBreadcrumbNavigationOptionsExtensions.ActivateByRoute{TBreadcrumb}"/> and render it with
/// <c>&lt;breadcrumbs&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// A name that contains characters other than letters and digits is a regular expression that must match the whole
/// route value, case-insensitively: <c>"Controller": "(.+)Orders"</c>. Plain names are compared case-insensitively.
/// </para>
/// <para>
/// The link uses the name of the breadcrumb, or, for a pattern, the current value (mapped by <c>*Map</c> when set).
/// </para>
/// </remarks>
/// <typeparam name="TBreadcrumb">The breadcrumb type itself.</typeparam>
public abstract class RouteBreadcrumb<TBreadcrumb> : NavigationItem<TBreadcrumb>, IRouteBreadcrumb
    where TBreadcrumb : RouteBreadcrumb<TBreadcrumb>, new()
{
    /// <inheritdoc />
    public string? Area { get; set; }

    /// <inheritdoc />
    public RouteValueMap? AreaMap { get; set; }

    /// <inheritdoc />
    public string? Controller { get; set; }

    /// <inheritdoc />
    public RouteValueMap? ControllerMap { get; set; }

    /// <inheritdoc />
    public string? Action { get; set; }

    /// <inheritdoc />
    public RouteValueMap? ActionMap { get; set; }

    /// <inheritdoc />
    public string? Page { get; set; }

    /// <inheritdoc />
    public IDictionary<string, string>? RouteValues { get; set; }

    /// <inheritdoc />
    public IList<string>? PreservedRouteValues { get; set; }

    /// <inheritdoc />
    public IDictionary<string, string>? DefaultRouteValues { get; set; }
}

/// <summary>
/// A breadcrumb bound to a route, for trees that need no extra properties.
/// </summary>
public class RouteBreadcrumb : RouteBreadcrumb<RouteBreadcrumb>;

/// <summary>
/// Matching breadcrumbs with the current request and building their links.
/// </summary>
public static class RouteBreadcrumbMatcher
{
    private static readonly ConcurrentDictionary<string, Regex> _patterns = new(StringComparer.Ordinal);

    /// <summary>
    /// Whether <paramref name="breadcrumb"/> is the breadcrumb of the current request: area, controller and action
    /// (or page) match, and the request has all <see cref="IRouteBreadcrumb.RouteValues"/> (in the route or the query).
    /// </summary>
    /// <param name="breadcrumb">The breadcrumb.</param>
    /// <param name="context">The current request.</param>
    /// <returns><see langword="true"/> if it matches.</returns>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public static bool IsMatch(IRouteBreadcrumb breadcrumb, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(breadcrumb);
        ArgumentNullException.ThrowIfNull(context);

        var route = context.GetRouteData().Values;
        var target = !string.IsNullOrEmpty(breadcrumb.Page)
            ? IsValueMatch(breadcrumb.Page, Value(route, "page"))
            : !string.IsNullOrEmpty(breadcrumb.Action)
                && IsValueMatch(breadcrumb.Action, Value(route, "action"))
                && (string.IsNullOrEmpty(breadcrumb.Controller) || IsValueMatch(breadcrumb.Controller, Value(route, "controller")));

        return target
            && (string.IsNullOrEmpty(breadcrumb.Area) ? string.IsNullOrEmpty(Value(route, "area")) : IsValueMatch(breadcrumb.Area, Value(route, "area")))
            && (breadcrumb.RouteValues ?? new Dictionary<string, string>()).All(expected =>
                string.Equals(Value(route, expected.Key) ?? context.Request.Query[expected.Key].FirstOrDefault(), expected.Value,
                    StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Builds the route values of the link of <paramref name="breadcrumb"/>: area, controller and action (or page),
    /// <see cref="IRouteBreadcrumb.RouteValues"/>, preserved values of the current route and defaults. Other values of
    /// the current route are set to <see langword="null"/>, so that the link does not take them over.
    /// </summary>
    /// <param name="breadcrumb">The breadcrumb.</param>
    /// <param name="context">The current request.</param>
    /// <returns>The route values.</returns>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public static RouteValueDictionary GetLinkValues(IRouteBreadcrumb breadcrumb, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(breadcrumb);
        ArgumentNullException.ThrowIfNull(context);

        var route = context.GetRouteData().Values;
        var values = new RouteValueDictionary();
        foreach (var (key, value) in route)
        {
            values[key] = breadcrumb.PreservedRouteValues?.Contains(key, StringComparer.OrdinalIgnoreCase) == true ? value : null;
        }
        foreach (var (key, value) in breadcrumb.RouteValues ?? new Dictionary<string, string>())
        {
            values[key] = value;
        }
        foreach (var (key, value) in breadcrumb.DefaultRouteValues ?? new Dictionary<string, string>())
        {
            if (string.IsNullOrEmpty(values[key]?.ToString()))
            {
                values[key] = value;
            }
        }

        // An empty area means "no area" (unlike an empty controller, which means "any controller").
        values["area"] = string.IsNullOrEmpty(breadcrumb.Area) && breadcrumb.AreaMap is null
            ? ""
            : Resolve(breadcrumb.Area, breadcrumb.AreaMap, Value(route, "area")) ?? "";
        if (!string.IsNullOrEmpty(breadcrumb.Page))
        {
            values["page"] = breadcrumb.Page;
            values.Remove("controller");
            values.Remove("action");
        }
        else
        {
            // "page" is kept: for a controller link it is an ordinary value (for example the page number of a list).
            values["controller"] = Resolve(breadcrumb.Controller, breadcrumb.ControllerMap, Value(route, "controller"));
            values["action"] = Resolve(breadcrumb.Action, breadcrumb.ActionMap, Value(route, "action"));
        }
        return values;
    }

    /// <summary>Whether a breadcrumb has a link target (an action or a page).</summary>
    /// <param name="breadcrumb">The breadcrumb.</param>
    /// <returns><see langword="true"/> if a link can be built.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="breadcrumb"/> is <see langword="null"/>.</exception>
    public static bool HasLink(IRouteBreadcrumb breadcrumb)
    {
        ArgumentNullException.ThrowIfNull(breadcrumb);

        return !string.IsNullOrEmpty(breadcrumb.Action) || !string.IsNullOrEmpty(breadcrumb.Page);
    }

    private static string? Resolve(string? name, RouteValueMap? map, string? current)
    {
        if (map is not null && current is not null && Pattern(map.From).Match(current) is { Success: true } match)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, map.To,
                match.Groups.Values.Skip(1).Select(group => (object)group.Value).ToArray());
        }
        return string.IsNullOrEmpty(name) || IsPattern(name) ? current : name;
    }

    private static bool IsValueMatch(string expected, string? actual)
        => actual is not null && (IsPattern(expected)
            ? Pattern(expected).IsMatch(actual)
            : string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase));

    // Page names start with "/" and may contain more of them: a slash alone does not make a pattern.
    private static bool IsPattern(string value) => value.Any(ch => !char.IsLetterOrDigit(ch) && ch != '/');

    private static Regex Pattern(string pattern)
        => _patterns.GetOrAdd(pattern, static p =>
            new Regex($"^(?:{p})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)));

    private static string? Value(RouteValueDictionary route, string key) => route.TryGetValue(key, out var value) ? value?.ToString() : null;
}

/// <summary>
/// Activating route breadcrumbs: <c>.AddJson&lt;RouteBreadcrumb&gt;(path, (services, options) =&gt; options.ActivateByRoute(services))</c>.
/// </summary>
public static class RouteBreadcrumbNavigationOptionsExtensions
{
    /// <summary>
    /// Marks the breadcrumb of the current request as active (see <see cref="RouteBreadcrumbMatcher.IsMatch"/>).
    /// Requires <see cref="IHttpContextAccessor"/> (<c>services.AddHttpContextAccessor()</c>).
    /// </summary>
    /// <typeparam name="TBreadcrumb">The breadcrumb type.</typeparam>
    /// <param name="options">The navigation options.</param>
    /// <param name="services">The service provider of the request.</param>
    /// <returns>The same options.</returns>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public static NavigationOptions<TBreadcrumb> ActivateByRoute<TBreadcrumb>(this NavigationOptions<TBreadcrumb> options, IServiceProvider services)
        where TBreadcrumb : RouteBreadcrumb<TBreadcrumb>, new()
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(services);

        var accessor = services.GetRequiredService<IHttpContextAccessor>();
        options.ItemActivator = breadcrumb => accessor.HttpContext is { } context && RouteBreadcrumbMatcher.IsMatch(breadcrumb, context);
        return options;
    }
}
