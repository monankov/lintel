namespace Lintel.Primitives.Tests;

public class TreeNodeTests
{
    public sealed class Category(string name) : TreeNode<Category>
    {
        public string Name { get; } = name;

        public override string ToString() => Name;
    }

    // root ─┬─ a ── a1 ── a11
    //       └─ b
    private static (Category Root, Category A, Category A1, Category A11, Category B) Tree()
    {
        var root = new Category("root");
        var a = new Category("a");
        var a1 = new Category("a1");
        var a11 = new Category("a11");
        var b = new Category("b");

        a1.AddChild(a11);
        a.AddChild(a1); // attach a subtree
        root.AddChild(a);
        root.AddChild(b);
        return (root, a, a1, a11, b);
    }

    [Fact]
    public void AddChild_links_parent_children_ancestors_and_descendants()
    {
        var (root, a, a1, a11, b) = Tree();

        root.HasParent.Should().BeFalse();
        root.Children.Should().BeEquivalentTo([a, b]);
        root.Descendants.Should().BeEquivalentTo([a, a1, a11, b]);
        a11.Parent.Should().BeSameAs(a1);
        a11.Ancestors.Should().BeEquivalentTo([a1, a, root]);
        a.Descendants.Should().BeEquivalentTo([a1, a11]);
        b.HasChildren.Should().BeFalse();
        a.HasChildren.Should().BeTrue();
    }

    [Fact]
    public void ClearParent_detaches_whole_subtree()
    {
        var (root, a, a1, a11, _) = Tree();

        a.ClearParent();

        a.HasParent.Should().BeFalse();
        root.Children.Should().NotContain(a);
        root.Descendants.Should().NotContain([a, a1, a11]);
        a11.Ancestors.Should().BeEquivalentTo([a1, a]);
        a.Descendants.Should().BeEquivalentTo([a1, a11]);
    }

    [Fact]
    public void AddChild_moves_node_from_previous_parent()
    {
        var (root, a, a1, a11, b) = Tree();

        b.AddChild(a1);

        a.Children.Should().BeEmpty();
        a.Descendants.Should().BeEmpty();
        b.Children.Should().Equal(a1);
        b.Descendants.Should().BeEquivalentTo([a1, a11]);
        a11.Ancestors.Should().BeEquivalentTo([a1, b, root]);
        root.Descendants.Should().BeEquivalentTo([a, b, a1, a11]);
    }

    [Fact]
    public void AddChild_of_existing_child_does_nothing()
    {
        var (root, a, _, _, _) = Tree();

        root.AddChild(a);

        root.Children.Should().HaveCount(2);
    }

    [Fact]
    public void AddChild_rejects_cycles()
    {
        var (root, a, _, a11, _) = Tree();

        var self = () => a.AddChild(a);
        var ancestor = () => a11.AddChild(root);

        self.Should().Throw<InvalidOperationException>();
        ancestor.Should().Throw<InvalidOperationException>();
        root.HasParent.Should().BeFalse();
    }

    [Fact]
    public void AddChild_rejects_null()
    {
        var act = () => new Category("x").AddChild(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
