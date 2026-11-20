using Models.API.Manifest.Pipelines;
using Models.Database.General;
using Services.TextServices;

namespace Services.Tests.TextServices;

public class TextPipelineOutputsTests
{
    [Fact]
    public void GetRequestedServices_ReturnsAll_WhenOutputNull()
    {
        var config = new TextPipelineConfig();

        config.GetRequestedServices().Should().Be(JobServices.All);
    }

    [Theory]
    [InlineData("All")]
    [InlineData("all")]
    public void GetRequestedServices_ReturnsAll_WhenOutputIsAll(string all)
    {
        var config = new TextPipelineConfig { Output = [all] };

        config.GetRequestedServices().Should().Be(JobServices.All);
    }

    [Fact]
    public void GetRequestedServices_AllWins_WhenCombinedWithOtherValues()
    {
        var config = new TextPipelineConfig { Output = ["All", "Figures"] };

        config.GetRequestedServices().Should().Be(JobServices.All);
    }

    [Fact]
    public void GetRequestedServices_CombinesFlags_CaseInsensitive()
    {
        var config = new TextPipelineConfig { Output = ["search", "pdf", "FULLTEXT"] };

        config.GetRequestedServices().Should()
            .Be(JobServices.Search | JobServices.Pdf | JobServices.FullText);
    }

    [Fact]
    public void GetRequestedServices_IgnoresAutocomplete()
    {
        var config = new TextPipelineConfig { Output = ["Search", "Autocomplete"] };

        config.GetRequestedServices().Should().Be(JobServices.Search);
    }

    [Fact]
    public void GetRequestedServices_AddsPdf_WhenPdfCoverPageSet()
    {
        var config = new TextPipelineConfig { Output = ["Search"], PdfCoverPage = "http://cover.page/sample.pdf" };

        config.GetRequestedServices().Should().Be(JobServices.Search | JobServices.Pdf);
    }

    [Fact]
    public void GetRequestedServices_ReturnsAll_WhenOutputNull_AndPdfCoverPageSet()
    {
        var config = new TextPipelineConfig { PdfCoverPage = "http://cover.page/sample.pdf" };

        config.GetRequestedServices().Should().Be(JobServices.All);
    }

    [Theory]
    [InlineData(null, PipelineJobStatus.CompletedWithOnlyPdf)]
    [InlineData(new[] { "All" }, PipelineJobStatus.CompletedWithOnlyPdf)]
    [InlineData(new[] { "Search", "Pdf" }, PipelineJobStatus.CompletedWithOnlyPdf)]
    [InlineData(new[] { "Pdf" }, PipelineJobStatus.Completed)]
    [InlineData(new[] { "Pdf", "TextAugmented" }, PipelineJobStatus.Completed)]
    [InlineData(new[] { "Search" }, PipelineJobStatus.CompletedNoOperation)]
    [InlineData(new[] { "Search", "FullText" }, PipelineJobStatus.CompletedNoOperation)]
    public void GetCompletedStatus_ReturnsExpected_WhenNoWordsFound(string[]? output, PipelineJobStatus expected)
    {
        var config = new TextPipelineConfig { Output = output?.ToList() };

        config.GetCompletedStatus(totalWordCount: 0).Should().Be(expected);
    }

    [Fact]
    public void GetCompletedStatus_ReturnsCompleted_WhenPdfCoverPageSetAndOnlyPdfRequested()
    {
        var config = new TextPipelineConfig { Output = ["Pdf"], PdfCoverPage = "http://cover.page/sample.pdf" };

        config.GetCompletedStatus(totalWordCount: 0).Should().Be(PipelineJobStatus.Completed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Search")]
    [InlineData("Pdf")]
    public void GetCompletedStatus_ReturnsCompleted_WhenWordsFound(string? output)
    {
        var config = new TextPipelineConfig { Output = output == null ? null : [output] };

        config.GetCompletedStatus(totalWordCount: 10).Should().Be(PipelineJobStatus.Completed);
    }

    [Theory]
    [InlineData("All")]
    [InlineData("Search")]
    [InlineData("fulltext")]
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
    [InlineData("Autocomplete")]
    [InlineData("1")]
    [InlineData("Search,Pdf")]
    public void IsValid_False_ForUnknownValues(string? output)
        => TextPipelineOutputs.IsValid(output).Should().BeFalse();
}
