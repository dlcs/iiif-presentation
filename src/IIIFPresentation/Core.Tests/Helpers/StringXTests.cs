using Core.Helpers;

namespace Core.Tests.Helpers;

public class StringXTests
{
    [Fact]
    public void HasText_ReturnsTrue()
    {
        // Arrange
        var testString = "Hello World";
        
        // Act
        var hasText = testString.HasText();
        
        // Assert
        hasText.Should().BeTrue();
    }
    
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void HasText_ReturnsFalse(string? testString)
    {
        // Arrange and Act
        var hasText = testString.HasText();
        
        // Assert
        hasText.Should().BeFalse();
    }
    
    [Fact]
    public void Append_AppendsStringsToList()
    {
        // Arrange
        var valuesToAppend = new List<string>();
        
        // Act
        valuesToAppend.Append("Hello", "World");
        
        // Assert
        valuesToAppend[0].Should().Be("Hello");
        valuesToAppend[1].Should().Be("World");
    }
    
    [Fact]
    public void AppendIf_AppendsToListWhenConditionTrue()
    {
        // Arrange
        var valuesToAppend = new List<string>();
        
        // Act
        valuesToAppend.AppendIf(true, "Hello", "World");
        
        // Assert
        valuesToAppend[0].Should().Be("Hello");
        valuesToAppend[1].Should().Be("World");
    }
    
    [Fact]
    public void AppendIf_DoesNotAppendToListWhenConditionFalse()
    {
        // Arrange
        var valuesToAppend = new List<string>();
        
        // Act
        valuesToAppend.AppendIf(false, "Hello", "World");
        
        // Assert
        valuesToAppend.Should().BeEmpty();
    }
    
    [Fact]
    public void GetLastPathElement_ReturnsLastPathElement()
    {
        // Arrange
        var testString = "some/path";
        
        // Act
        var path = testString.GetLastPathElement();
        
        // Assert
        path.Should().Be("path");
    }
    
    [Fact]
    public void GetLastPathElement_ReturnsFullStringWhenNoSlash()
    {
        // Arrange
        var testString = "some path";
        
        // Act
        var path = testString.GetLastPathElement();
        
        // Assert
        path.Should().Be("some path");
    }
}