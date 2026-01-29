using API.Features.Storage.Helpers;
using IIIF.Presentation.V3.Strings;

namespace API.Tests.Helpers;

public class CheckEqualityXTests
{
    [Fact]
    public void CheckEqualityX_CheckSingleObject_Equal()
    {
        // Arrange
        var first = new LanguageMap("en", "test");
        var second = new LanguageMap("en", "test");
        
        // Act
        var equal = first.CheckEquality(second);
        
        // Assert
        equal.Should().BeTrue();
    }
    
    [Fact]
    public void CheckEqualityX_CheckMultipleKey_Equal()
    {
        // Arrange
        var first = new LanguageMap
        {
            {"en", ["test"] },
            {"fr", ["test"] }
        };
        var second = new LanguageMap
        {
            {"en", ["test"] },
            {"fr", ["test"] }
        };
        
        // Act
        var equal = first.CheckEquality(second);
        
        // Assert
        equal.Should().BeTrue();
    }
    
    [Fact]
    public void CheckEqualityX_CheckMultipleValue_Equal()
    {
        // Arrange
        var first = new LanguageMap("en", ["test", "test"]);
        var second = new LanguageMap("en", ["test", "test"]);
        
        // Act
        var equal = first.CheckEquality(second);
        
        // Assert
        equal.Should().BeTrue();
    }
    
    [Fact]
    public void CheckEqualityX_CheckMultipleDictionaryAndValue_Equal()
    {
        // Arrange
        var first = new LanguageMap
        {
            {"en", ["test", "test"] },
            {"fr", ["test", "test"] }
        };
        var second = new LanguageMap
        {
            {"en", ["test", "test"] },
            {"fr", ["test", "test"] }
        };
        
        // Act
        var equal = first.CheckEquality(second);
        
        // Assert
        equal.Should().BeTrue();
    }
    
    [Fact]
    public void CheckEqualityX_NullFirst_NotEqual()
    {
        // Arrange
        LanguageMap? first = null;
        var second = new LanguageMap("en", "test");
        
        // Act
        var equal = first.CheckEquality(second);
        
        // Assert
        equal.Should().BeFalse();
    }
    
    [Fact]
    public void CheckEqualityX_NullSecond_NotEqual()
    {
        // Arrange
        var first = new LanguageMap("en", "test");
        LanguageMap? second = null;
        
        // Act
        var equal = first.CheckEquality(second);
        
        // Assert
        equal.Should().BeFalse();
    }
    
    [Fact]
    public void CheckEqualityX_CheckSingleObject_ValueNotEqual()
    {
        // Arrange
        var first = new LanguageMap("en", "test");
        var second = new LanguageMap("en", "not equal");
        
        // Act
        var equal = first.CheckEquality(second);
        
        // Assert
        equal.Should().BeFalse();
    }
    
    [Fact]
    public void CheckEqualityX_CheckSingleObject_KeyNotEqual()
    {
        // Arrange
        var first = new LanguageMap("en", "test");
        var second = new LanguageMap("not equal", "test");
        
        // Act
        var equal = first.CheckEquality(second);
        
        // Assert
        equal.Should().BeFalse();
    }
    
    [Fact]
    public void CheckEqualityX_CheckMultipleKeyValue_NotEqual()
    {
        // Arrange
        var first = new LanguageMap
        {
            {"en", ["test"] },
            {"fr", ["test"] }
        };
        var second = new LanguageMap
        {
            {"en", ["test"] },
            {"fr", ["not equal"] }
        };
        
        // Act
        var equal = first.CheckEquality(second);
        
        // Assert
        equal.Should().BeFalse();
    }
    
    [Fact]
    public void CheckEqualityX_CheckMultipleValue_NotEqual()
    {
        // Arrange
        var first = new LanguageMap("en", ["test", "test"]);
        var second = new LanguageMap("en", ["test", "not equal"]);
        
        // Act
        var equal = first.CheckEquality(second);
        
        // Assert
        equal.Should().BeFalse();
    }
    
    [Fact]
    public void CheckEqualityX_CheckMultipleDictionaryAndValue_NotEqual()
    {
        // Arrange
        var first = new LanguageMap
        {
            {"en", ["test", "test"] },
            {"fr", ["test", "test"] }
        };
        var second = new LanguageMap
        {
            {"en", ["test", "test"] },
            {"fr", ["test", "not equal"] }
        };
        
        // Act
        var equal = first.CheckEquality(second);
        
        // Assert
        equal.Should().BeFalse();
    }
    
    [Fact]
    public void CheckEqualityX_CheckMultipleDictionaryAdditionalSecond_NotEqual()
    {
        // Arrange
        var first = new LanguageMap
        {
            {"en", ["test", "test"] }
        };
        var second = new LanguageMap
        {
            {"en", ["test", "test"] },
            {"fr", ["test", "test"] }
        };
        
        // Act
        var equal = first.CheckEquality(second);
        
        // Assert
        equal.Should().BeFalse();
    }
    
    [Fact]
    public void CheckEqualityX_CheckMultipleDictionaryAdditionalFirst_NotEqual()
    {
        // Arrange
        var first = new LanguageMap
        {
            {"en", ["test", "test"] },
            {"fr", ["test", "test"] }
        };
        var second = new LanguageMap
        {
            {"en", ["test", "test"] }
        };
        
        // Act
        var equal = first.CheckEquality(second);
        
        // Assert
        equal.Should().BeFalse();
    }
}
