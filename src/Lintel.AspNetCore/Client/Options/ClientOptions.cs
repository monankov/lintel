using System.Collections.ObjectModel;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.AspNetCore.Client.Options;

/// <summary>
/// Settings of client options.
/// </summary>
public static class ClientOptionDefaults
{
    /// <summary>The separator of key parts: <c>area.controller.action</c>. Defaults to <c>.</c>.</summary>
    public static string Delimiter { get; set; } = ".";
}

/// <summary>
/// Collects options for client-side scripts during a request (for example to render them as JSON in the layout).
/// </summary>
public interface IClientOptionProvider
{
    /// <summary>Adds or replaces options under <paramref name="key"/>.</summary>
    /// <param name="key">The key, usually built by <see cref="BuildClientOptionsKey"/>.</param>
    /// <param name="options">The options.</param>
    void AddClientOptions(string key, Dictionary<string, object?> options);

    /// <summary>Returns the options under <paramref name="key"/>.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The options.</returns>
    IReadOnlyDictionary<string, object?> GetClientOptions(string key);

    /// <summary>Returns all options of the request by key.</summary>
    /// <returns>The options.</returns>
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> GetAllClientOptions();

    /// <summary>Builds a key such as <c>admin.order-items.edit</c> from the route.</summary>
    /// <param name="routeData">The route data.</param>
    /// <returns>The key.</returns>
    string BuildClientOptionsKey(RouteData routeData);
}

/// <summary>
/// Default <see cref="IClientOptionProvider"/> that keeps options in <see cref="HttpContext.Items"/>.
/// </summary>
public class ClientOptionProvider : IClientOptionProvider
{
    private static readonly object _key = typeof(IClientOptionProvider);

    private readonly IHttpContextAccessor _contextAccessor;

    /// <summary>
    /// Initializes the provider.
    /// </summary>
    /// <param name="contextAccessor">The HTTP context accessor.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="contextAccessor"/> is <see langword="null"/>.</exception>
    public ClientOptionProvider(IHttpContextAccessor contextAccessor)
    {
        ArgumentNullException.ThrowIfNull(contextAccessor);

        _contextAccessor = contextAccessor;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">If <paramref name="key"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="options"/> is empty.</exception>
    public void AddClientOptions(string key, Dictionary<string, object?> options)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Options cannot be empty.");
        }

        var existing = GetOptions(key);
        foreach (var (name, value) in options)
        {
            existing[name] = value;
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">If <paramref name="key"/> is empty.</exception>
    public IReadOnlyDictionary<string, object?> GetClientOptions(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return new ReadOnlyDictionary<string, object?>(GetOptions(key));
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> GetAllClientOptions()
        => GetAllOptions().ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyDictionary<string, object?>)new ReadOnlyDictionary<string, object?>(entry.Value));

    /// <inheritdoc />
    /// <remarks>
    /// MVC: <c>[area.]controller.action</c>; Razor Pages: <c>[area.]page</c> (for example <c>orders.edit</c> for <c>/Orders/Edit</c>).
    /// Parts are written in kebab-case.
    /// </remarks>
    /// <exception cref="ArgumentNullException">If <paramref name="routeData"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">If the route has neither controller/action nor page.</exception>
    public string BuildClientOptionsKey(RouteData routeData)
    {
        ArgumentNullException.ThrowIfNull(routeData);

        var values = routeData.Values;
        var parts = new List<string>();

        if (values["area"]?.ToString() is { Length: > 0 } area)
        {
            parts.Add(area);
        }

        if (values["controller"]?.ToString() is { Length: > 0 } controller && values["action"]?.ToString() is { Length: > 0 } action)
        {
            parts.Add(controller);
            parts.Add(action);
        }
        else if (values["page"]?.ToString() is { Length: > 0 } page)
        {
            parts.AddRange(page.Split('/', StringSplitOptions.RemoveEmptyEntries));
        }
        else
        {
            throw new InvalidOperationException("Cannot build a client options key: the route has neither controller and action nor page.");
        }

        return string.Join(ClientOptionDefaults.Delimiter, parts.Select(ToKebabCase));
    }

    private Dictionary<string, object?> GetOptions(string key)
    {
        var all = GetAllOptions();
        if (!all.TryGetValue(key, out var options))
        {
            options = [];
            all[key] = options;
        }
        return options;
    }

    private Dictionary<string, Dictionary<string, object?>> GetAllOptions()
    {
        var items = (_contextAccessor.HttpContext
            ?? throw new InvalidOperationException("Client options are available only during an HTTP request.")).Items;

        if (items[_key] is not Dictionary<string, Dictionary<string, object?>> all)
        {
            all = [];
            items[_key] = all;
        }
        return all;
    }

    // "OrderItems" -> "order-items", the same as in Structr.
    private static string ToKebabCase(string value)
    {
        var builder = new StringBuilder(value.Length + 4);
        for (var i = 0; i < value.Length; i++)
        {
            if (i > 0 && char.IsUpper(value[i]))
            {
                builder.Append('-');
            }
            builder.Append(char.ToLowerInvariant(value[i]));
        }
        return builder.ToString();
    }
}

/// <summary>
/// Adding client options from controllers: <c>this.AddClientOptions(new { pageSize = 20 });</c>
/// </summary>
public static class ClientOptionControllerExtensions
{
    /// <summary>Adds the public properties of <paramref name="options"/> under the key of the current action.</summary>
    /// <param name="controller">The controller.</param>
    /// <param name="options">An object (usually anonymous) whose properties become options.</param>
    public static void AddClientOptions(this Controller controller, object options)
        => controller.AddClientOptions(GetKey(controller), options);

    /// <summary>Adds the public properties of <paramref name="options"/> under <paramref name="key"/>.</summary>
    /// <param name="controller">The controller.</param>
    /// <param name="key">The key.</param>
    /// <param name="options">An object (usually anonymous) whose properties become options.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> is <see langword="null"/>.</exception>
    public static void AddClientOptions(this Controller controller, string key, object options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var dictionary = options.GetType().GetProperties()
            .Where(property => property.GetIndexParameters().Length == 0)
            .ToDictionary(property => property.Name, property => property.GetValue(options));
        controller.AddClientOptions(key, dictionary);
    }

    /// <summary>Adds options under the key of the current action.</summary>
    /// <param name="controller">The controller.</param>
    /// <param name="options">The options.</param>
    public static void AddClientOptions(this Controller controller, Dictionary<string, object?> options)
        => controller.AddClientOptions(GetKey(controller), options);

    /// <summary>Adds options under <paramref name="key"/>.</summary>
    /// <param name="controller">The controller.</param>
    /// <param name="key">The key.</param>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="controller"/> is <see langword="null"/>.</exception>
    public static void AddClientOptions(this Controller controller, string key, Dictionary<string, object?> options)
    {
        ArgumentNullException.ThrowIfNull(controller);

        GetProvider(controller).AddClientOptions(key, options);
    }

    private static string GetKey(Controller controller)
    {
        ArgumentNullException.ThrowIfNull(controller);

        return GetProvider(controller).BuildClientOptionsKey(controller.RouteData);
    }

    private static IClientOptionProvider GetProvider(Controller controller)
        => controller.HttpContext.RequestServices.GetRequiredService<IClientOptionProvider>();
}
