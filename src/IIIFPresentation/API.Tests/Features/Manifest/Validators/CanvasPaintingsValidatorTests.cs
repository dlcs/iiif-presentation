using API.Features.Manifest.Validators;
using FluentValidation.TestHelper;
using Models.Database;

namespace API.Tests.Features.Manifest.Validators;

public class CanvasPaintingsValidatorTests
{
    private readonly CanvasPaintingsValidator sut = new();

    [Theory]
    [InlineData("first", 0, null, "second", 1, null)]
    [InlineData("first", 0, null, "first", 1, null)]
    [InlineData("first", 0, 1, "first", 1, 2)]
    public void NoError_WhenValidCanvasPaintings(string firstCanvasId, int firstCanvasOrder, int? firstChoiceOrder, 
        string secondCanvaId,  int secondCanvasOrder, int? secondChoiceOrder)
    {
        // Arrange
        var canvasPaintings = new List<CanvasPainting>()
        {
            new(){ Id = firstCanvasId, CanvasOrder = firstCanvasOrder, ChoiceOrder = firstChoiceOrder },
            new(){ Id = secondCanvaId, CanvasOrder = secondCanvasOrder, ChoiceOrder = secondChoiceOrder }
        };
        
        // Act
        var result = sut.TestValidate(canvasPaintings);
        
        // Assert
        result.IsValid.Should().BeTrue();
    }
    
    [Theory]
    [InlineData(null ,null)]
    [InlineData(1 ,null)]
    [InlineData(null ,1)]
    public void Error_InChoice_WhenInvalidCanvasPaintings(int? firstChoiceOrder, int? secondChoiceOrder)
    {
        // Arrange
        var canvasPaintings = new List<CanvasPainting>()
        {
            new(){ Id = "first", CanvasOrder = 0, ChoiceOrder = firstChoiceOrder },
            new(){ Id = "first", CanvasOrder = 0, ChoiceOrder = secondChoiceOrder }
        };
        
        // Act
        var result = sut.TestValidate(canvasPaintings);
        
        // Assert
        result.IsValid.Should().BeFalse();
        result.ShouldHaveValidationErrorFor(cp => cp)
            .WithErrorMessage("Detected conflicting implicit and explicit 'canvasOrder' values");
    }
}
