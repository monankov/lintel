using System.Runtime.CompilerServices;

namespace Lintel.Navigation;

/// <summary>
/// Base class of navigation items (menu entries, breadcrumbs). Add your own properties (URL, icon, roles...).
/// </summary>
/// <remarks>
/// <typeparamref name="TNavigationItem"/> must be the deriving type itself: <c>class MenuItem : NavigationItem&lt;MenuItem&gt;</c>.
/// Items are equal when their <see cref="Id"/>s are equal (case-insensitive); items without an id are equal only to themselves.
/// </remarks>
/// <typeparam name="TNavigationItem">The deriving item type.</typeparam>
public abstract class NavigationItem<TNavigationItem> : IEquatable<TNavigationItem>
    where TNavigationItem : NavigationItem<TNavigationItem>, new()
{
    private readonly List<TNavigationItem> _children = [];

    private TNavigationItem This => (TNavigationItem)this;

    /// <summary>The identifier; also the default resource name of <see cref="Title"/>.</summary>
    public string? Id { get; set; }

    /// <summary>The title.</summary>
    public string? Title { get; set; }

    /// <summary>The name of the localized title in <see cref="NavigationOptions{TNavigationItem}.ResourceType"/>; <see cref="Id"/> by default.</summary>
    public string? ResourceName { get; set; }

    /// <summary>The direct children.</summary>
    public IEnumerable<TNavigationItem> Children => _children;

    /// <summary>All items above this one, from the top level down to the parent (the breadcrumb order).</summary>
    public IEnumerable<TNavigationItem> Ancestors
    {
        get
        {
            var ancestors = new List<TNavigationItem>();
            for (var parent = Parent; parent is not null; parent = parent.Parent)
            {
                ancestors.Add(parent);
            }
            ancestors.Reverse();
            return ancestors;
        }
    }

    /// <summary>All items below this one, depth-first in order.</summary>
    public IEnumerable<TNavigationItem> Descendants => _children.SelectMany(child => child.Descendants.Prepend(child));

    /// <summary>The parent item, or <see langword="null"/> for a top-level item.</summary>
    public TNavigationItem? Parent { get; private set; }

    /// <summary>Whether the item is the current page.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Whether the item has children.</summary>
    public bool HasChildren => _children.Count > 0;

    /// <summary>Whether a direct child is active.</summary>
    public bool HasActiveChild => _children.Exists(x => x.IsActive);

    /// <summary>Whether any descendant is active (for expanding menu sections).</summary>
    public bool HasActiveDescendant => Descendants.Any(x => x.IsActive);

    /// <summary>Whether any ancestor is active.</summary>
    public bool HasActiveAncestor => Ancestors.Any(x => x.IsActive);

    /// <summary>
    /// Adds a child, detaching it from its previous parent.
    /// </summary>
    /// <param name="child">The child.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="child"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">If <paramref name="child"/> is this item or one of its ancestors.</exception>
    public void AddChild(TNavigationItem child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (ReferenceEquals(child, this) || Ancestors.Any(ancestor => ReferenceEquals(ancestor, child)))
        {
            throw new InvalidOperationException("A navigation item cannot become a child of itself or of its descendant.");
        }

        child.Parent?.RemoveChild(child);

        _children.Add(child);
        child.Parent = This;
    }

    /// <summary>
    /// Removes a child.
    /// </summary>
    /// <param name="child">The child.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="child"/> is <see langword="null"/>.</exception>
    public void RemoveChild(TNavigationItem child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (_children.Remove(child))
        {
            child.Parent = null;
        }
    }

    /// <inheritdoc />
    public bool Equals(TNavigationItem? other)
    {
        if (other is null)
        {
            return false;
        }
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        return !string.IsNullOrWhiteSpace(Id) && string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TNavigationItem other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
        => string.IsNullOrWhiteSpace(Id) ? RuntimeHelpers.GetHashCode(this) : StringComparer.OrdinalIgnoreCase.GetHashCode(Id);

    internal void SetActive(bool isActive) => IsActive = isActive;
}
