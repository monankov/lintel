using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Lintel.Primitives;

/// <summary>
/// A materialized path of node identifiers from the root, written as <c>/1/2/3/</c>.
/// </summary>
/// <remarks>
/// Useful for storing trees in a string column and querying subtrees with <c>LIKE '/1/2/%'</c>.
/// For SQL Server's native <c>hierarchyid</c> type use <c>Microsoft.EntityFrameworkCore.SqlServer.HierarchyId</c>.
/// </remarks>
public class HierarchyId
{
    private const char _separator = '/';

    private List<int> _nodes;

    /// <summary>
    /// Initializes a path from node identifiers, root first.
    /// </summary>
    /// <param name="nodes">The node identifiers; at least one, none negative.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="nodes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="nodes"/> is empty or contains a negative value.</exception>
    public HierarchyId(IEnumerable<int> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        _nodes = Validate(nodes.ToList(), nameof(nodes));
    }

    /// <summary>The node identifiers, root first.</summary>
    public IReadOnlyList<int> Nodes => _nodes;

    /// <summary>Returns the identifier of the last node.</summary>
    /// <returns>The node identifier.</returns>
    public int GetNode() => _nodes[^1];

    /// <summary>Returns the number of nodes in the path.</summary>
    /// <returns>The level, 1 for a root.</returns>
    public int GetLevel() => _nodes.Count;

    /// <summary>
    /// Whether <paramref name="node"/> is one of the ancestors of the last node.
    /// </summary>
    /// <param name="node">The node identifier.</param>
    /// <returns><see langword="true"/> if the node is an ancestor.</returns>
    public bool IsDescendantOf(int node) => _nodes.Take(_nodes.Count - 1).Contains(node);

    /// <summary>
    /// Whether this path starts with <paramref name="ancestor"/> and is longer than it.
    /// </summary>
    /// <param name="ancestor">The possible ancestor path.</param>
    /// <returns><see langword="true"/> if this path is inside the subtree of <paramref name="ancestor"/>.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="ancestor"/> is <see langword="null"/>.</exception>
    public bool IsDescendantOf(HierarchyId ancestor)
    {
        ArgumentNullException.ThrowIfNull(ancestor);

        return ancestor._nodes.Count < _nodes.Count
            && _nodes.Take(ancestor._nodes.Count).SequenceEqual(ancestor._nodes);
    }

    /// <summary>
    /// Returns the path of the ancestor <paramref name="n"/> levels higher.
    /// </summary>
    /// <param name="n">How many levels higher; from 1 to <see cref="GetLevel"/> − 1.</param>
    /// <returns>The ancestor path.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="n"/> is out of range.</exception>
    public HierarchyId GetAncestor(int n)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(n, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(n, _nodes.Count);

        return new HierarchyId(_nodes.Take(_nodes.Count - n));
    }

    /// <summary>
    /// Returns the path of a child node.
    /// </summary>
    /// <param name="node">The identifier of the child.</param>
    /// <returns>The child path.</returns>
    /// <exception cref="ArgumentException">If <paramref name="node"/> is negative.</exception>
    public HierarchyId GetDescendant(int node) => new([.. _nodes, node]);

    /// <summary>
    /// Moves this path from the subtree of <paramref name="sourceAncestor"/> to the subtree of <paramref name="destAncestor"/>:
    /// the prefix <paramref name="sourceAncestor"/> is replaced with <paramref name="destAncestor"/>.
    /// </summary>
    /// <param name="sourceAncestor">The current ancestor path.</param>
    /// <param name="destAncestor">The new ancestor path.</param>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">If this path is not a descendant of <paramref name="sourceAncestor"/>.</exception>
    public void Move(HierarchyId sourceAncestor, HierarchyId destAncestor)
    {
        ArgumentNullException.ThrowIfNull(sourceAncestor);
        ArgumentNullException.ThrowIfNull(destAncestor);

        if (!IsDescendantOf(sourceAncestor))
        {
            throw new InvalidOperationException($"Current hierarchyid {this} isn't descendant of {sourceAncestor}");
        }

        _nodes = [.. destAncestor._nodes, .. _nodes.Skip(sourceAncestor._nodes.Count)];
    }

    /// <summary>Returns the path as <c>/1/2/3/</c>.</summary>
    /// <returns>The string form.</returns>
    public override string ToString() => _separator + string.Join(_separator, _nodes) + _separator;

    /// <summary>
    /// Parses a path written as <c>/1/2/3/</c> (leading and trailing separators are optional).
    /// </summary>
    /// <param name="value">The string form.</param>
    /// <returns>The path.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">If <paramref name="value"/> is not a valid path.</exception>
    public static HierarchyId Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return TryParse(value, out var result)
            ? result
            : throw new InvalidOperationException($"Invalid HierarchyId string value {value}.");
    }

    /// <summary>
    /// Tries to parse a path written as <c>/1/2/3/</c>.
    /// </summary>
    /// <param name="value">The string form.</param>
    /// <param name="result">The path, if parsed.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is a valid path.</returns>
    public static bool TryParse([NotNullWhen(true)] string? value, [NotNullWhen(true)] out HierarchyId? result)
    {
        result = null;
        if (value is null)
        {
            return false;
        }

        var parts = value.Trim().Trim(_separator).Split(_separator);
        var nodes = new List<int>(parts.Length);
        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var node))
            {
                return false;
            }
            nodes.Add(node);
        }

        result = new HierarchyId(nodes);
        return true;
    }

    private static List<int> Validate(List<int> nodes, string parameterName)
    {
        if (nodes.Count == 0)
        {
            throw new ArgumentException("HierarchyId must contain at least one node.", parameterName);
        }
        if (nodes.Exists(node => node < 0))
        {
            throw new ArgumentException("HierarchyId nodes must not be negative.", parameterName);
        }
        return nodes;
    }
}
