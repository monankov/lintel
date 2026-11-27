using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Resources;

namespace Lintel.Navigation;

/// <summary>Loads the source navigation tree (from a file, a database...).</summary>
/// <typeparam name="TNavigationItem">The item type.</typeparam>
public interface INavigationProvider<TNavigationItem>
    where TNavigationItem : NavigationItem<TNavigationItem>, new()
{
    /// <summary>Creates the top-level items with their children.</summary>
    /// <returns>The items.</returns>
    IEnumerable<TNavigationItem> CreateNavigation();
}

/// <summary>Caches source navigation trees.</summary>
public interface INavigationCache
{
    /// <summary>Returns the cached tree of <typeparamref name="TNavigationItem"/> or creates it.</summary>
    /// <typeparam name="TNavigationItem">The item type.</typeparam>
    /// <param name="factory">Creates the tree.</param>
    /// <returns>The tree.</returns>
    IEnumerable<TNavigationItem> GetOrAdd<TNavigationItem>(Func<IEnumerable<TNavigationItem>> factory)
        where TNavigationItem : NavigationItem<TNavigationItem>, new();
}

/// <summary>Builds the navigation of the current request: filtered, localized, with the active item marked.</summary>
/// <typeparam name="TNavigationItem">The item type.</typeparam>
public interface INavigationBuilder<TNavigationItem>
    where TNavigationItem : NavigationItem<TNavigationItem>, new()
{
    /// <summary>Builds a new tree; the source tree is not modified.</summary>
    /// <returns>The top-level items.</returns>
    IEnumerable<TNavigationItem> BuildNavigation();
}

/// <summary>The navigation of the current request (a menu).</summary>
/// <typeparam name="TNavigationItem">The item type.</typeparam>
public interface INavigation<out TNavigationItem> : IEnumerable<TNavigationItem>
    where TNavigationItem : NavigationItem<TNavigationItem>, new()
{
    /// <summary>The active item, or <see langword="null"/>.</summary>
    TNavigationItem? Active { get; }
}

/// <summary>The path from the top level to the active item of the current request.</summary>
/// <typeparam name="TNavigationItem">The item type.</typeparam>
public interface IBreadcrumbNavigation<out TNavigationItem> : IEnumerable<TNavigationItem>
    where TNavigationItem : NavigationItem<TNavigationItem>, new()
{
    /// <summary>The active item, or <see langword="null"/>.</summary>
    TNavigationItem? Active { get; }
}

/// <summary>Options of the navigation of <typeparamref name="TNavigationItem"/>.</summary>
/// <typeparam name="TNavigationItem">The item type.</typeparam>
public class NavigationOptions<TNavigationItem>
    where TNavigationItem : NavigationItem<TNavigationItem>, new()
{
    /// <summary>The resource class with localized titles (the generated class of a <c>.resx</c>; internal classes work).</summary>
    public Type? ResourceType { get; set; }

    /// <summary>Which items are shown (for example by user roles); hidden items hide their children. All by default.</summary>
    public Func<TNavigationItem, bool>? ItemFilter { get; set; } = static _ => true;

    /// <summary>Which item is the current page; the first match wins. None by default.</summary>
    public Func<TNavigationItem, bool>? ItemActivator { get; set; } = static _ => false;

    /// <summary>Whether the source tree is cached for the application lifetime. Defaults to <see langword="true"/>.</summary>
    public bool EnableCaching { get; set; } = true;
}

/// <summary>
/// Default <see cref="INavigationBuilder{TNavigationItem}"/>.
/// </summary>
/// <remarks>
/// Every call copies the source tree, so titles are localized in the culture of the current request and the active item
/// is computed again (in Structr a second call in the same request lost the active item, and titles were cached in the
/// culture of the first request).
/// </remarks>
/// <typeparam name="TNavigationItem">The item type.</typeparam>
public class NavigationBuilder<TNavigationItem> : INavigationBuilder<TNavigationItem>
    where TNavigationItem : NavigationItem<TNavigationItem>, new()
{
    private static readonly PropertyInfo[] _copiedProperties = typeof(TNavigationItem)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(property => property.CanWrite && property.CanRead && property.GetIndexParameters().Length == 0)
        .ToArray();

    private static readonly ConcurrentDictionary<Type, ResourceManager> _resourceManagers = new();

    private readonly INavigationProvider<TNavigationItem> _provider;
    private readonly NavigationOptions<TNavigationItem> _options;
    private readonly INavigationCache _cache;

    /// <summary>
    /// Initializes the builder.
    /// </summary>
    /// <param name="provider">The source of the tree.</param>
    /// <param name="options">The options.</param>
    /// <param name="cache">The cache of source trees.</param>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public NavigationBuilder(INavigationProvider<TNavigationItem> provider, NavigationOptions<TNavigationItem> options, INavigationCache cache)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(cache);

        _provider = provider;
        _options = options;
        _cache = cache;
    }

    /// <inheritdoc />
    public IEnumerable<TNavigationItem> BuildNavigation()
    {
        var source = _options.EnableCaching ? _cache.GetOrAdd(CreateSource) : CreateSource();
        var resources = _options.ResourceType is { } type
            ? _resourceManagers.GetOrAdd(type, static t => new ResourceManager(t.FullName!, t.Assembly))
            : null;

        var state = new BuildState(resources);
        var result = new List<TNavigationItem>();
        foreach (var item in source)
        {
            if (Copy(item, state) is { } copy)
            {
                result.Add(copy);
            }
        }
        return result;
    }

    private List<TNavigationItem> CreateSource() => _provider.CreateNavigation()?.ToList() ?? [];

    private TNavigationItem? Copy(TNavigationItem source, BuildState state)
    {
        if (_options.ItemFilter?.Invoke(source) != true)
        {
            return null;
        }

        var item = new TNavigationItem();
        foreach (var property in _copiedProperties)
        {
            property.SetValue(item, property.GetValue(source));
        }

        if (state.Resources is not null
            && (string.IsNullOrWhiteSpace(item.ResourceName) ? item.Id : item.ResourceName) is { Length: > 0 } resourceName
            && state.Resources.GetString(resourceName) is { } title)
        {
            item.Title = title;
        }

        if (!state.HasActive && _options.ItemActivator?.Invoke(source) == true)
        {
            item.SetActive(true);
            state.HasActive = true;
        }

        foreach (var child in source.Children)
        {
            if (Copy(child, state) is { } childCopy)
            {
                item.AddChild(childCopy);
            }
        }

        return item;
    }

    private sealed class BuildState(ResourceManager? resources)
    {
        public ResourceManager? Resources { get; } = resources;
        public bool HasActive { get; set; }
    }
}

/// <summary>
/// Default <see cref="INavigation{TNavigationItem}"/>.
/// </summary>
/// <typeparam name="TNavigationItem">The item type.</typeparam>
public class Navigation<TNavigationItem> : INavigation<TNavigationItem>
    where TNavigationItem : NavigationItem<TNavigationItem>, new()
{
    private readonly List<TNavigationItem> _items;

    /// <summary>
    /// Initializes the navigation.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="builder"/> is <see langword="null"/>.</exception>
    public Navigation(INavigationBuilder<TNavigationItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _items = builder.BuildNavigation().ToList();
        Active = _items.FirstOrDefault(x => x.IsActive) ?? _items.SelectMany(x => x.Descendants).FirstOrDefault(x => x.IsActive);
    }

    /// <inheritdoc />
    public TNavigationItem? Active { get; }

    /// <inheritdoc />
    public IEnumerator<TNavigationItem> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Default <see cref="IBreadcrumbNavigation{TNavigationItem}"/>.
/// </summary>
/// <typeparam name="TNavigationItem">The item type.</typeparam>
public class BreadcrumbNavigation<TNavigationItem> : IBreadcrumbNavigation<TNavigationItem>
    where TNavigationItem : NavigationItem<TNavigationItem>, new()
{
    private readonly List<TNavigationItem> _items = [];

    /// <summary>
    /// Initializes the breadcrumbs.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="builder"/> is <see langword="null"/>.</exception>
    public BreadcrumbNavigation(INavigationBuilder<TNavigationItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var items = builder.BuildNavigation().ToList();
        Active = items.FirstOrDefault(x => x.IsActive) ?? items.SelectMany(x => x.Descendants).FirstOrDefault(x => x.IsActive);
        if (Active is not null)
        {
            _items.AddRange(Active.Ancestors);
            _items.Add(Active);
        }
    }

    /// <inheritdoc />
    public TNavigationItem? Active { get; }

    /// <inheritdoc />
    public IEnumerator<TNavigationItem> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Default <see cref="INavigationCache"/>: keeps each tree for the lifetime of the application (register it as a singleton).
/// </summary>
/// <remarks>
/// A tree is created once per item type, even when several requests ask for it at the same time.
/// </remarks>
public class NavigationCache : INavigationCache
{
    private readonly ConcurrentDictionary<Type, Lazy<object>> _trees = new();

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">If <paramref name="factory"/> is <see langword="null"/>.</exception>
    public IEnumerable<TNavigationItem> GetOrAdd<TNavigationItem>(Func<IEnumerable<TNavigationItem>> factory)
        where TNavigationItem : NavigationItem<TNavigationItem>, new()
    {
        ArgumentNullException.ThrowIfNull(factory);

        return (IEnumerable<TNavigationItem>)_trees
            .GetOrAdd(typeof(TNavigationItem), _ => new Lazy<object>(() => factory().ToList()))
            .Value;
    }
}
