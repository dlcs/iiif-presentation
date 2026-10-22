using Models.API.Manifest;
using Services.TextServices;

namespace Services.Tests.TextServices;

public class TextPipelineOutputsTests
{
    [Fact]
    public void GetRequestedServices_ReturnsAll_WhenConfigNull()
    {
        PipelineConfig? config = null;

        config.GetRequestedServices().Should().Be(JobServices.All);
    }

    [Fact]
    public void GetRequestedServices_ReturnsAll_WhenOutputNull()
    {
        var config = new PipelineConfig { Action = "Index" };

        config.GetRequestedServices().Should().Be(JobServices.All);
    }

    [Theory]
    [InlineData("All")]
    [InlineData("all")]
    public void GetRequestedServices_ReturnsAll_WhenOutputIsAll(string all)
    {
        var config = new PipelineConfig { Output = [all] };

        config.GetRequestedServices().Should().Be(JobServices.All);
    }

    [Fact]
    public void GetRequestedServices_AllWins_WhenCombinedWithOtherValues()
    {
        var config = new PipelineConfig { Output = ["All", "Figures"] };

        config.GetRequestedServices().Should().Be(JobServices.All);
    }

    [Fact]
    public void GetRequestedServices_CombinesFlags_CaseInsensitive()
    {
        var config = new PipelineConfig { Output = ["search", "Autocomplete", "FULLTEXT"] };

        config.GetRequestedServices().Should()
            .Be(JobServices.Search | JobServices.Autocomplete | JobServices.FullText);
    }

    [Fact]
    public void GetRequestedServices_AddsPdf_WhenPdfCoverPageSet()
    {
        var config = new PipelineConfig { Output = ["Search"], PdfCoverPage = "http://cover.page/sample.pdf" };

        config.GetRequestedServices().Should().Be(JobServices.Search | JobServices.Pdf);
    }

    [Fact]
    public void GetRequestedServices_ReturnsAll_WhenOutputNull_AndPdfCoverPageSet()
    {
        var config = new PipelineConfig { PdfCoverPage = "http://cover.page/sample.pdf" };

        config.GetRequestedServices().Should().Be(JobServices.All);
    }

    [Theory]
    [InlineData("All")]
    [InlineData("Search")]
    [InlineData("autocomplete")]
    [InlineData("FullText")]
    [InlineData("Annotations")]
    [InlineData("Pdf")]
    [InlineData("TextAugmented")]
    [InlineData("Figures")]
    public void IsValid_True_ForKnownValues(string output)
        => TextPipelineOutputs.IsValid(output).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("None")]
    [InlineData("Thumbnails")]
    [InlineData("1")]
    [InlineData("Search,Pdf")]
    public void IsValid_False_ForUnknownValues(string? output)
        => TextPipelineOutputs.IsValid(output).Should().BeFalse();
}
