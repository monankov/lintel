using System.Globalization;

namespace Lintel.Primitives.Tests;

public class NumberExtensionsTests
{
    [Theory]
    [InlineData(0L, "0")]
    [InlineData(999L, "999")]
    [InlineData(-999L, "-999")]
    [InlineData(1_000L, "1K")]
    [InlineData(1_234L, "1.2K")]
    [InlineData(1_250L, "1.3K")]
    [InlineData(9_960L, "10K")]
    [InlineData(12_345L, "12K")]
    [InlineData(123_456L, "123K")]
    [InlineData(999_499L, "999K")]
    [InlineData(999_500L, "1M")]
    [InlineData(5_500_000L, "5.5M")]
    [InlineData(123_400_000L, "123M")]
    [InlineData(2_000_000_000L, "2B")]
    [InlineData(-1_500L, "-1.5K")]
    [InlineData(7_300_000_000_000L, "7.3T")]
    [InlineData(5_000_000_000_000_000L, "5,000T")]
    [InlineData(long.MinValue, "-9,223,372T")]
    public void ToCompactString_uses_units_and_one_decimal_below_ten(long value, string expected)
        => value.ToCompactString(CultureInfo.InvariantCulture).Should().Be(expected);

    [Fact]
    public void ToCompactString_uses_the_culture()
    {
        var russian = new CultureInfo("ru-RU");

        1_234.ToCompactString(russian).Should().Be("1,2K");
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        1_234.ToCompactString().Should().Be("1.2K");
        42.ToCompactString().Should().Be("42");
    }
}
