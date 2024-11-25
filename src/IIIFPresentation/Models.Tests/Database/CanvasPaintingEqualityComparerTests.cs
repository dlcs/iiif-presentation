using IIIF.Presentation.V3.Strings;
using Models.Database;

namespace Models.Tests.Database;

public class CanvasPaintingEqualityComparerTests
{
    [Fact]
    public void Equals_True_IfBothNull()
        => CanvasPaintingEqualityComparer.Instance.Equals(null, null).Should().BeTrue();
    
    [Fact]
    public void Equals_False_IfFirstNull()
        => CanvasPaintingEqualityComparer.Instance.Equals(new CanvasPainting(), null).Should().BeFalse();
    
    [Fact]
    public void Equals_False_IfSecondNull()
        => CanvasPaintingEqualityComparer.Instance.Equals(null, new CanvasPainting()).Should().BeFalse();

    [Fact]
    public void Equals_True_IfBothNew()
        => CanvasPaintingEqualityComparer.Instance.Equals(new CanvasPainting(), new CanvasPainting()).Should().BeTrue();

    [Fact]
    public void Equals_True_IfAllRelevantFieldsMatch()
    {
        // These first 6 fields are what defines equality
        var one = new CanvasPainting
        {
            Id = "foo",
            ManifestId = "abc123",
            CustomerId = 10,
            CanvasOriginalId = new Uri("https://test.exmple.com"),
            CanvasOrder = 0,
            ChoiceOrder = 1,
            Label = new LanguageMap("en", "foo"),
            StaticHeight = 10,
        };
        
        var two = new CanvasPainting
        {
            Id = "foo",
            ManifestId = "abc123",
            CustomerId = 10,
            CanvasOriginalId = new Uri("https://test.exmple.com"),
            CanvasOrder = 0,
            ChoiceOrder = 1,
            Label = new LanguageMap("en", "bar"),
            StaticHeight = 99,
        };
        
        CanvasPaintingEqualityComparer.Instance.Equals(one, two).Should().BeTrue();
    }
    
    [Fact]
    public void Equals_True_IfAllRelevantFieldsMatch_NullablesAreNull()
    {
        // These 6 fields are what defines equality
        var one = new CanvasPainting
        {
            Id = "foo",
            ManifestId = "abc123",
            CustomerId = 10,
            CanvasOriginalId = null,
            CanvasOrder = 0,
            ChoiceOrder = null,
        };
        
        var two = new CanvasPainting
        {
            Id = "foo",
            ManifestId = "abc123",
            CustomerId = 10,
            CanvasOriginalId = null,
            CanvasOrder = 0,
            ChoiceOrder = null,
        };
        
        CanvasPaintingEqualityComparer.Instance.Equals(one, two).Should().BeTrue();
    }
    
    [Fact]
    public void Equals_True_IfSameInstance()
    {
        var one = new CanvasPainting
        {
            Id = "foo",
            ManifestId = "abc123",
            CustomerId = 10,
            CanvasOriginalId = new Uri("https://test.exmple.com"),
            CanvasOrder = 0,
            ChoiceOrder = null
        };
        
        CanvasPaintingEqualityComparer.Instance.Equals(one, one).Should().BeTrue();
    }
    
    [Theory]
    [MemberData(nameof(NonMatchingCanvasPaintingData))]
    public void Equals_False_IfValuesDiffer(CanvasPainting two)
    {
        var one = new CanvasPainting
        {
            Id = "foo",
            ManifestId = "abc123",
            CustomerId = 10,
            CanvasOriginalId = new Uri("https://test.exmple.com"),
            CanvasOrder = 0,
            ChoiceOrder = null
        };
        
        CanvasPaintingEqualityComparer.Instance.Equals(one, two).Should().BeFalse();
    }

    public static TheoryData<CanvasPainting> NonMatchingCanvasPaintingData =>
        new()
        {
            new CanvasPainting
            {
                Id = "bar", // "foo"
                ManifestId = "abc123",
                CustomerId = 10,
                CanvasOriginalId = new Uri("https://test.exmple.com"),
                CanvasOrder = 0,
                ChoiceOrder = null
            },
            new CanvasPainting
            {
                Id = "foo",
                ManifestId = "def", // "abc123"
                CustomerId = 10,
                CanvasOriginalId = new Uri("https://test.exmple.com"),
                CanvasOrder = 0,
                ChoiceOrder = null
            },
            new CanvasPainting
            {
                Id = "foo",
                ManifestId = "abc123",
                CustomerId = 11, // 10
                CanvasOriginalId = new Uri("https://test.exmple.com"),
                CanvasOrder = 0,
                ChoiceOrder = null
            },
            new CanvasPainting
            {
                Id = "foo",
                ManifestId = "abc123",
                CustomerId = 10,
                CanvasOriginalId = new Uri("http://test.exmple.com"), // https://test.exmple.com
                CanvasOrder = 0,
                ChoiceOrder = null
            },
            new CanvasPainting
            {
                Id = "foo",
                ManifestId = "abc123",
                CustomerId = 10,
                CanvasOriginalId = new Uri("https://test.exmple.com"),
                CanvasOrder = 1, // 0
                ChoiceOrder = null
            },
            new CanvasPainting
            {
                Id = "foo",
                ManifestId = "abc123",
                CustomerId = 10,
                CanvasOriginalId = new Uri("https://test.exmple.com"),
                CanvasOrder = 0,
                ChoiceOrder = 2 // null
            },
        };
}