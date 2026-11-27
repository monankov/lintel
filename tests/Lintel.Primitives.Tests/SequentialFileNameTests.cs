using System.Text.RegularExpressions;

namespace Lintel.Primitives.Tests;

public partial class SequentialFileNameTests
{
    [GeneratedRegex(@"^\d{14}-[0-9a-f]{32}$")]
    private static partial Regex NamePattern();

    [Fact]
    public void NewFileName_has_date_and_guid_parts()
        => NewFileNameShouldMatch(SequentialFileName.NewFileName(), "");

    [Theory]
    [InlineData("pdf", ".pdf")]
    [InlineData(".pdf", ".pdf")]
    [InlineData(".tar.gz", ".tar.gz")]
    public void NewFileNameWithExtension_adds_dot_when_missing(string extension, string expected)
        => NewFileNameShouldMatch(SequentialFileName.NewFileNameWithExtension(extension), expected);

    [Theory]
    [InlineData("report.docx", ".docx")]
    [InlineData(@"C:\files\photo.JPG", ".JPG")]
    [InlineData("README", "")]
    [InlineData(null, "")]
    public void NewFileName_keeps_extension_of_existing_file(string? existing, string expected)
        => NewFileNameShouldMatch(SequentialFileName.NewFileName(existing), expected);

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void NewFileNameWithExtension_rejects_empty_extension(string? extension)
    {
        var act = () => SequentialFileName.NewFileNameWithExtension(extension!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Names_are_unique()
        => Enumerable.Range(0, 100).Select(_ => SequentialFileName.NewFileName()).Should().OnlyHaveUniqueItems();

    private static void NewFileNameShouldMatch(string fileName, string extension)
    {
        fileName.Should().EndWith(extension);
        NamePattern().IsMatch(fileName[..^extension.Length]).Should().BeTrue(fileName);
    }
}
