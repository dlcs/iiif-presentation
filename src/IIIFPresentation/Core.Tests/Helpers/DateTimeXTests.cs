using Core.Helpers;

namespace Core.Tests.Helpers;

public class DateTimeXTests
{
    [Fact]
    public void ToSecondPrecision_DropsSubSecondComponent()
    {
        // Arrange
        var dateTime = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(9_876_543);

        // Act
        var truncated = dateTime.ToSecondPrecision();

        // Assert
        truncated.Should().Be(new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc));
    }

    [Fact]
    public void ToSecondPrecision_IsNoOp_WhenAlreadyWholeSeconds()
    {
        // Arrange
        var dateTime = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        // Act
        var truncated = dateTime.ToSecondPrecision();

        // Assert
        truncated.Should().Be(dateTime);
    }

    [Fact]
    public void ToSecondPrecision_PreservesKind()
    {
        // Arrange
        var dateTime = new DateTime(2025, 1, 2, 3, 4, 5, 678, DateTimeKind.Utc);

        // Act
        var truncated = dateTime.ToSecondPrecision();

        // Assert
        truncated.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Floor_TruncatesToArbitraryInterval()
    {
        // Arrange
        var dateTime = new DateTime(2025, 1, 2, 3, 47, 5, DateTimeKind.Utc);

        // Act
        var truncated = dateTime.Floor(TimeSpan.FromMinutes(15));

        // Assert
        truncated.Should().Be(new DateTime(2025, 1, 2, 3, 45, 0, DateTimeKind.Utc));
    }
}
