using Newtonsoft.Json.Linq;
using Services.TextServices;

namespace Services.Tests.TextServices;

public class TextPipelineResultTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\n \n")]
    public void Create_HasNoErrors_WhenErrorsEmpty(string? errors)
    {
        var result = TextPipelineResult.Create(2, 10, errors);

        result.Errors.Should().BeNull();
    }

    [Fact]
    public void Create_SplitsErrorsOnNewline()
    {
        var result = TextPipelineResult.Create(3, 10, "canvas/1: 403 Forbidden\n canvas/2: timed out \n");

        result.Errors.Should().Equal("canvas/1: 403 Forbidden", "canvas/2: timed out");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("canvas/1: 403 Forbidden")]
    public void GetWarning_NoTextFound_WhenNoWords(string? errors)
    {
        TextPipelineResult.Create(2, 0, errors, skippedCanvases: 1, pagesWithoutTextSource: 1, pagesWithoutWords: 1)
            .GetWarning().Should().Be(TextPipelineResult.NoTextFoundWarning);
    }

    [Fact]
    public void GetWarning_PageErrors_WhenWordsAndErrors()
    {
        TextPipelineResult.Create(3, 100, "canvas/1: 500 Internal Server Error").GetWarning()
            .Should().Be(TextPipelineResult.PageErrorsWarning);
    }

    [Fact]
    public void GetWarning_Null_WhenWordsAndNoErrors()
    {
        TextPipelineResult.Create(2, 100, null, skippedCanvases: 1, pagesWithoutTextSource: 1, pagesWithoutWords: 1)
            .GetWarning().Should().BeNull();
    }

    [Fact]
    public void ToJObject_IsCamelCase_AndOmitsNullErrors()
    {
        var json = TextPipelineResult.Create(2, 100, null, skippedCanvases: 1, pagesWithoutTextSource: 2,
            pagesWithoutWords: 3).ToJObject();

        JToken.DeepEquals(json, JObject.Parse("""
            {
              "totalPages": 2,
              "totalWordCount": 100,
              "skippedCanvases": 1,
              "pagesWithoutTextSource": 2,
              "pagesWithoutWords": 3
            }
            """)).Should().BeTrue();
    }

    [Fact]
    public void ToJObject_IncludesAllErrors()
    {
        var errors = string.Join("\n", Enumerable.Range(1, 5).Select(i => $"canvas/{i}: 404"));

        var json = TextPipelineResult.Create(5, 0, errors).ToJObject();

        json["errors"]!.ToObject<string[]>().Should().HaveCount(5);
    }
}
