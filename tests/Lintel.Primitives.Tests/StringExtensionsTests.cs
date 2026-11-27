using System.Globalization;

namespace Lintel.Primitives.Tests;

public class StringExtensionsTests
{
    public enum FooBarBaz
    {
        Foo,
        Bar,
    }

    public static TheoryData<string?, Type, object?> CastData => new()
    {
        { "123", typeof(int), 123 },
        { "Foo", typeof(FooBarBaz), FooBarBaz.Foo },
        { "1", typeof(FooBarBaz), FooBarBaz.Bar },
        { "Bar", typeof(FooBarBaz?), FooBarBaz.Bar },
        { "", typeof(int?), null },
        { " ", typeof(Uri), null },
        { "text", typeof(string), "text" },
        { null, typeof(string), null },
        { "true", typeof(bool), true },
        { "3f2504e0-4f89-11d3-9a0c-0305e82c3301", typeof(Guid), Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301") },
        { "2026-11-25", typeof(DateOnly), new DateOnly(2026, 11, 25) },
        { "13:45:00", typeof(TimeOnly?), new TimeOnly(13, 45) },
        { "01:30:00", typeof(TimeSpan), TimeSpan.FromMinutes(90) },
        { "https://example.com/", typeof(Uri), new Uri("https://example.com/") },
    };

    [Theory]
    [MemberData(nameof(CastData))]
    public void Cast(string? value, Type type, object? expected)
        => value.Cast(type, true, CultureInfo.InvariantCulture).Should().Be(expected);

    [Fact]
    public void Cast_generic()
    {
        "123".Cast<int>(true).Should().Be(123);
        "".Cast<int?>().Should().BeNull();
        "abc".Cast<int>().Should().Be(0);
    }

    [Fact]
    public void Cast_uses_format_provider()
    {
        "1,5".Cast<double>(true, CultureInfo.GetCultureInfo("ru-RU")).Should().Be(1.5);
        "1.5".Cast<double>(true, CultureInfo.InvariantCulture).Should().Be(1.5);
        "1,5".Cast<decimal>(true, CultureInfo.InvariantCulture).Should().Be(15m);
    }

    [Theory]
    [InlineData("123", typeof(bool))]
    [InlineData("", typeof(int))]
    [InlineData(null, typeof(int))]
    [InlineData("Baz", typeof(FooBarBaz))]
    [InlineData("not-a-guid", typeof(Guid))]
    public void Cast_throws_if_asked_when_conversion_fails(string? value, Type type)
    {
        var act = () => value.Cast(type, true);

        act.Should().Throw<InvalidCastException>()
            .WithMessage($"Error with converting string \"{value}\" to type \"{type.Name}\"");
    }

    [Fact]
    public void Cast_returns_null_when_not_asked_to_throw()
        => "123".Cast(typeof(bool)).Should().BeNull();

    [Fact]
    public void Cast_throws_when_type_is_null()
    {
        var act = () => "1".Cast(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
