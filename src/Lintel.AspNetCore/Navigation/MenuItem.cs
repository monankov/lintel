using Lintel.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.AspNetCore.Navigation;

/// <summary>
/// An item of a menu: a link (<see cref="Url"/>) or a group of items. Load the tree with <c>Lintel.Navigation</c>
/// (for example from JSON), activate it with <see cref="MenuNavigationOptionsExtensions.ActivateMenu{TMenuItem}"/>,
/// filter it with <see cref="MenuNavigationOptionsExtensions.FilterMenuByPolicy{TMenuItem}"/> and render it in a view.
/// </summary>
/// <typeparam name="TMenuItem">The menu item type itself.</typeparam>
public abstract class MenuItem<TMenuItem> : NavigationItem<TMenuItem>
    where TMenuItem : MenuItem<TMenuItem>, new()
{
    /// <summary>The address of the link; a group has none.</summary>
    public string? Url { get; set; }

    /// <summary>The icon, usually CSS classes.</summary>
    public string? Icon { get; set; }

    /// <summary>The authorization policy the user must pass to see the item; <see langword="null"/> means everyone.</summary>
    public string? Policy { get; set; }

    /// <summary>Whether the item has a link.</summary>
    public bool HasUrl => !string.IsNullOrWhiteSpace(Url);

    /// <summary>Whether the item has an icon.</summary>
    public bool HasIcon => !string.IsNullOrWhiteSpace(Icon);

    /// <summary>Whether a descendant has a link (a group worth showing).</summary>
    public bool HasDescendantWithUrl => Descendants.Any(item => item.HasUrl);
}

/// <summary>
/// A menu item, for menus that need no extra properties.
/// </summary>
public class MenuItem : MenuItem<MenuItem>;

/// <summary>
/// Sets the active menu item of a controller, an action or a Razor page by its identifier:
/// <c>[Menu("orders")] public class OrdersController : Controller</c>. An attribute on an action takes precedence
/// over the one on its controller.
/// </summary>
/// <param name="id">The identifier of the menu item.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class MenuAttribute(string id) : Attribute
{
    /// <summary>The identifier of the menu item.</summary>
    public string Id { get; } = !string.IsNullOrWhiteSpace(id) ? id : throw new ArgumentException("The menu item identifier is required.", nameof(id));
}

/// <summary>
/// Activation and filtering of menus:
/// <c>.AddJson&lt;MenuItem&gt;(path, (services, o) =&gt; o.ActivateMenu(services).FilterMenuByPolicy(services))</c>.
/// </summary>
public static class MenuNavigationOptionsExtensions
{
    /// <summary>
    /// Marks the menu item of the current request as active: the item named by <see cref="MenuAttribute"/> of the
    /// endpoint; without the attribute (and when <paramref name="matchUrl"/> is set) the item whose <c>Url</c> is the
    /// longest prefix of the request path, by whole segments and case-insensitively. <c>/</c> matches only the root.
    /// </summary>
    /// <remarks>
    /// An endpoint with <see cref="MenuAttribute"/> is never matched by URL, even if no item has its identifier.
    /// Absolute URLs (<c>https://…</c>) are not matched. Requires <see cref="IHttpContextAccessor"/>.
    /// </remarks>
    /// <typeparam name="TMenuItem">The menu item type.</typeparam>
    /// <param name="options">The navigation options.</param>
    /// <param name="services">The service provider of the request.</param>
    /// <param name="matchUrl">Whether items are matched by URL when the endpoint has no <see cref="MenuAttribute"/>.</param>
    /// <returns>The same options.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> or <paramref name="services"/> is <see langword="null"/>.</exception>
    public static NavigationOptions<TMenuItem> ActivateMenu<TMenuItem>(this NavigationOptions<TMenuItem> options, IServiceProvider services,
        bool matchUrl = true)
        where TMenuItem : MenuItem<TMenuItem>, new()
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(services);

        var accessor = services.GetRequiredService<IHttpContextAccessor>();
        string? bestUrl = null;
        var urlComputed = false;

        options.ItemActivator = item =>
        {
            if (accessor.HttpContext is not { } context)
            {
                return false;
            }
            if (context.GetEndpoint()?.Metadata.GetMetadata<MenuAttribute>() is { } menu)
            {
                return string.Equals(item.Id, menu.Id, StringComparison.OrdinalIgnoreCase);
            }
            if (!matchUrl || !item.HasUrl)
            {
                return false;
            }

            // The options are created per request, so the best URL is computed once per request.
            if (!urlComputed)
            {
                bestUrl = FindBestUrl(Root(item), context.Request.Path);
                urlComputed = true;
            }
            return bestUrl is not null && string.Equals(Normalize(item.Url), bestUrl, StringComparison.OrdinalIgnoreCase);
        };
        return options;

        IEnumerable<TMenuItem> Root(TMenuItem item)
        {
            var source = services.GetService<INavigationProvider<TMenuItem>>();
            return source is null
                ? item.Ancestors.LastOrDefault() is { } top ? [top] : [item]
                : options.EnableCaching
                    ? services.GetRequiredService<INavigationCache>().GetOrAdd(() => source.CreateNavigation())
                    : source.CreateNavigation();
        }
    }

    /// <summary>
    /// Hides items whose <c>Policy</c> the current user does not pass (<see cref="IAuthorizationService"/>), and groups
    /// (items without <c>Url</c>) that have no visible item with a link left. The filter set before is applied too.
    /// </summary>
    /// <remarks>
    /// Policies are evaluated once per request each. The navigation API is synchronous, so the evaluation waits for
    /// <see cref="IAuthorizationService.AuthorizeAsync(System.Security.Claims.ClaimsPrincipal, object?, string)"/>;
    /// keep menu policies to checks of claims and roles. Requires <c>services.AddAuthorization()</c> when items have policies.
    /// </remarks>
    /// <typeparam name="TMenuItem">The menu item type.</typeparam>
    /// <param name="options">The navigation options.</param>
    /// <param name="services">The service provider of the request.</param>
    /// <returns>The same options.</returns>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public static NavigationOptions<TMenuItem> FilterMenuByPolicy<TMenuItem>(this NavigationOptions<TMenuItem> options, IServiceProvider services)
        where TMenuItem : MenuItem<TMenuItem>, new()
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(services);

        var accessor = services.GetRequiredService<IHttpContextAccessor>();
        var previous = options.ItemFilter;
        var policies = new Dictionary<string, bool>(StringComparer.Ordinal);
        var visible = new Dictionary<TMenuItem, bool>(ReferenceEqualityComparer.Instance);

        options.ItemFilter = IsVisible;
        return options;

        bool IsVisible(TMenuItem item)
        {
            if (visible.TryGetValue(item, out var known))
            {
                return known;
            }

            var result = (previous?.Invoke(item) ?? true)
                && (item.Policy is not { Length: > 0 } policy || IsAuthorized(policy))
                && (item.HasUrl || item.Children.Any(IsVisible)); // a visible child has a link or visible links below it
            visible[item] = result;
            return result;
        }

        bool IsAuthorized(string policy)
        {
            if (!policies.TryGetValue(policy, out var authorized))
            {
                var authorization = services.GetService<IAuthorizationService>()
                    ?? throw new InvalidOperationException($"Menu item policy '{policy}' requires services.AddAuthorization().");
                var user = accessor.HttpContext?.User ?? new System.Security.Claims.ClaimsPrincipal();
                authorized = authorization.AuthorizeAsync(user, null, policy).GetAwaiter().GetResult().Succeeded;
                policies[policy] = authorized;
            }
            return authorized;
        }
    }

    private static string? FindBestUrl<TMenuItem>(IEnumerable<TMenuItem> roots, PathString path)
        where TMenuItem : MenuItem<TMenuItem>, new()
        => roots.SelectMany(root => root.Descendants.Prepend(root))
            .Select(item => Normalize(item.Url))
            .Where(url => url is not null && IsMatch(url, path))
            .OrderByDescending(url => url!.Length)
            .FirstOrDefault();

    private static bool IsMatch(string url, PathString path)
        => url == "/"
            ? !path.HasValue || path.Value == "/"
            : path.StartsWithSegments(url, StringComparison.OrdinalIgnoreCase);

    // A local path without query, fragment and trailing slash; null for absolute or empty URLs.
    private static string? Normalize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith('/') || url.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }
        var end = url.IndexOfAny(['?', '#']);
        var path = end < 0 ? url : url[..end];
        return path.Length > 1 ? path.TrimEnd('/') : path;
    }
}
