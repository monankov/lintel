namespace Lintel.Primitives;

/// <summary>
/// Base class for nodes of an in-memory tree that keeps parent, children, ancestors and descendants in sync.
/// </summary>
/// <remarks>
/// <typeparamref name="T"/> must be the deriving type itself: <c>class Category : TreeNode&lt;Category&gt;</c>.
/// Moving a node with <see cref="AddChild"/> detaches it from its previous parent. Cycles are rejected.
/// </remarks>
/// <typeparam name="T">The deriving node type.</typeparam>
public abstract class TreeNode<T> where T : TreeNode<T>
{
    private readonly HashSet<T> _ancestors = [];
    private readonly HashSet<T> _children = [];
    private readonly HashSet<T> _descendants = [];

    /// <summary>The parent node, or <see langword="null"/> for a root.</summary>
    public virtual T? Parent { get; private set; }

    /// <summary>The direct children.</summary>
    public virtual IEnumerable<T> Children => _children;

    /// <summary>All nodes above this one, up to the root.</summary>
    public virtual IEnumerable<T> Ancestors => _ancestors;

    /// <summary>All nodes below this one.</summary>
    public virtual IEnumerable<T> Descendants => _descendants;

    /// <summary>This node as <typeparamref name="T"/>.</summary>
    protected T This => (T)this;

    /// <summary>Whether the node has children.</summary>
    public bool HasChildren => _children.Count > 0;

    /// <summary>Whether the node has a parent.</summary>
    public bool HasParent => Parent is not null;

    /// <summary>
    /// Makes <paramref name="child"/> a child of this node, detaching it from its previous parent.
    /// </summary>
    /// <param name="child">The node to add.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="child"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">If <paramref name="child"/> is this node or one of its ancestors.</exception>
    public virtual void AddChild(T child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (ReferenceEquals(child, this) || _ancestors.Contains(child))
        {
            throw new InvalidOperationException("A node cannot become a child of itself or of its descendant.");
        }
        if (ReferenceEquals(child.Parent, this))
        {
            return;
        }

        child.ClearParent();

        _children.Add(child);
        child.Parent = This;
        ChangeAncestorDescendantRelation(This, child, addRelation: true);
    }

    /// <summary>
    /// Detaches this node (with its subtree) from its parent.
    /// </summary>
    public virtual void ClearParent()
    {
        if (Parent is null)
        {
            return;
        }

        ChangeAncestorDescendantRelation(Parent, This, addRelation: false);
        Parent._children.Remove(This);
        Parent = null;
    }

    // Links (or unlinks) every ancestor of 'ancestor' (inclusive) with every node of the subtree of 'descendant' (inclusive).
    private static void ChangeAncestorDescendantRelation(T ancestor, T descendant, bool addRelation)
    {
        if (ancestor.Parent is not null)
        {
            ChangeAncestorDescendantRelation(ancestor.Parent, descendant, addRelation);
        }

        foreach (var grandDescendant in descendant._children)
        {
            ChangeAncestorDescendantRelation(ancestor, grandDescendant, addRelation);
        }

        if (addRelation)
        {
            ancestor._descendants.Add(descendant);
            descendant._ancestors.Add(ancestor);
        }
        else
        {
            ancestor._descendants.Remove(descendant);
            descendant._ancestors.Remove(ancestor);
        }
    }
}
