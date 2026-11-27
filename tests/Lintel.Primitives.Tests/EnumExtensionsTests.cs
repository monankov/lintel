using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Lintel.Primitives.Tests.TestData;

namespace Lintel.Primitives.Tests;

public class EnumExtensionsTests
{
    public enum FooBarBaz
    {
        [Description("Some Foo enum")]
        Foo,

        [Display(Name = "BarBarBar")]
        Bar,

        [Display(Name = "displayNameForEnumBaz", ResourceType = typeof(TestResources))]
        [Description("Some Baz enum")]
        Baz,

        [Display(Name = "missingResource", ResourceType = typeof(TestResources))]
        Qux,

        [Display(Description = "No name")]
        Quux,
    }

    [Flags]
    public enum Access
    {
        [Description("Can read")]
        Read = 1,

        [Description("Can write")]
        Write = 2,
    }

    [Theory]
    [InlineData(FooBarBaz.Foo, "Some Foo enum")]
    [InlineData(FooBarBaz.Bar, "Bar")]
    [InlineData(FooBarBaz.Baz, "Some Baz enum")]
    public void GetDescription(FooBarBaz value, string expected)
    {
        value.GetDescription().Should().Be(expected);
        ((Enum)value).GetDescription().Should().Be(expected);
    }

    [Theory]
    [InlineData(FooBarBaz.Bar, "BarBarBar")]
    [InlineData(FooBarBaz.Foo, "Foo")]
    [InlineData(FooBarBaz.Baz, "Bazzzz")]
    [InlineData(FooBarBaz.Qux, "missingResource")]
    [InlineData(FooBarBaz.Quux, "Quux")]
    public void GetDisplayName(FooBarBaz value, string expected)
    {
        value.GetDisplayName().Should().Be(expected);
        ((Enum)value).GetDisplayName().Should().Be(expected);
    }

    [Fact]
    public void Combined_flags_and_undefined_values_fall_back_to_ToString()
    {
        (Access.Read | Access.Write).GetDescription().Should().Be("Read, Write");
        ((Enum)(Access.Read | Access.Write)).GetDisplayName().Should().Be("Read, Write");
        ((FooBarBaz)42).GetDisplayName().Should().Be("42");
        Access.Write.GetDescription().Should().Be("Can write");
    }

    [Fact]
    public void Non_generic_overloads_throw_for_null()
    {
        var description = () => ((Enum)null!).GetDescription();
        var displayName = () => ((Enum)null!).GetDisplayName();

        description.Should().Throw<ArgumentNullException>();
        displayName.Should().Throw<ArgumentNullException>();
    }
}
