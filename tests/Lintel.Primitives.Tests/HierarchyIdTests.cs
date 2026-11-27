namespace Lintel.Primitives.Tests;

public class HierarchyIdTests
{
    private static HierarchyId Path(params int[] nodes) => new(nodes);

    [Fact]
    public void Ctor_and_ToString()
    {
        var result = Path(1, 2, 3);

        result.ToString().Should().Be("/1/2/3/");
        result.Nodes.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Ctor_validates_nodes()
    {
        var withNull = () => new HierarchyId(null!);
        var empty = () => new HierarchyId([]);
        var negative = () => new HierarchyId([1, -2]);

        withNull.Should().Throw<ArgumentNullException>();
        empty.Should().Throw<ArgumentException>();
        negative.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetNode_and_GetLevel()
    {
        Path(1, 2, 3).GetNode().Should().Be(3);
        Path(1, 2, 3, 4, 5).GetLevel().Should().Be(5);
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(5, false)]
    [InlineData(7, false)]
    public void IsDescendantOf_node(int node, bool expected)
        => Path(1, 2, 3, 4, 5).IsDescendantOf(node).Should().Be(expected);

    [Fact]
    public void IsDescendantOf_path_checks_prefix()
    {
        var path = Path(1, 2, 3);

        path.IsDescendantOf(Path(1, 2)).Should().BeTrue();
        path.IsDescendantOf(Path(1)).Should().BeTrue();
        path.IsDescendantOf(Path(2)).Should().BeFalse();
        path.IsDescendantOf(Path(1, 2, 3)).Should().BeFalse();
        path.IsDescendantOf(Path(1, 2, 3, 4)).Should().BeFalse();
    }

    [Theory]
    [InlineData(1, "/1/2/3/4/")]
    [InlineData(3, "/1/2/")]
    [InlineData(4, "/1/")]
    public void GetAncestor(int levelsHigher, string expected)
        => Path(1, 2, 3, 4, 5).GetAncestor(levelsHigher).ToString().Should().Be(expected);

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    public void GetAncestor_throws_when_out_of_range(int levelsHigher)
    {
        var act = () => Path(1, 2, 3, 4, 5).GetAncestor(levelsHigher);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetDescendant()
        => Path(1, 2, 3, 4, 5).GetDescendant(6).ToString().Should().Be("/1/2/3/4/5/6/");

    [Fact]
    public void Move_replaces_ancestor_prefix()
    {
        var path = Path(11, 12, 13, 14, 15);

        path.Move(Path(11, 12, 13), Path(21, 22));

        path.ToString().Should().Be("/21/22/14/15/");
    }

    [Fact]
    public void Move_replaces_only_the_prefix_when_segments_repeat()
    {
        var path = Path(1, 2, 1, 2, 3);

        path.Move(Path(1, 2), Path(9));

        path.ToString().Should().Be("/9/1/2/3/");
    }

    [Fact]
    public void Move_throws_when_not_descendant_of_source()
    {
        // 7 is an ancestor node, but /1/7/ is not the prefix of /5/7/9/.
        var path = Path(5, 7, 9);

        var act = () => path.Move(Path(1, 7), Path(2));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Current hierarchyid /5/7/9/ isn't descendant of /1/7/");
        path.ToString().Should().Be("/5/7/9/");
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Move_throws_when_argument_is_null(bool sourceIsNull, bool destIsNull)
    {
        var act = () => Path(11, 12, 13).Move(sourceIsNull ? null! : Path(11), destIsNull ? null! : Path(21));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("/1/2/3/", "/1/2/3/")]
    [InlineData("1/2/3", "/1/2/3/")]
    [InlineData(" /42/ ", "/42/")]
    public void Parse_accepts_valid_paths(string value, string expected)
        => HierarchyId.Parse(value).ToString().Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("//")]
    [InlineData("/1/abc/2/")]
    [InlineData("/1//2/")]
    [InlineData("/1/-2/")]
    public void Parse_rejects_invalid_paths(string value)
    {
        var act = () => HierarchyId.Parse(value);

        act.Should().Throw<InvalidOperationException>().WithMessage($"Invalid HierarchyId string value {value}.");
        HierarchyId.TryParse(value, out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_throws_for_null()
    {
        var act = () => HierarchyId.Parse(null!);

        act.Should().Throw<ArgumentNullException>();
        HierarchyId.TryParse(null, out _).Should().BeFalse();
    }
}
