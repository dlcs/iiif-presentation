using API.Features.Storage.Models;
using API.Settings;
using AWS.Settings;
using DLCS;

namespace API.Tests.Features.Storage.Models;

public class RequestModifiersTests
{
    private static ApiSettings Settings(int pageSize = 100, int maxPageSize = 1000) => new()
    {
        AWS = new AWSSettings(),
        DLCS = new DlcsSettings { ApiUri = new Uri("https://localhost") },
        PageSize = pageSize,
        MaxPageSize = maxPageSize
    };

    private sealed record TestPagedRequest(int? Page, int? PageSize, string? OrderBy = null, bool Descending = false)
        : IPagedRequest;

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_DefaultsPageSize_WhenNullOrNonPositive(int? pageSize)
    {
        var result = RequestModifiers.Create(new TestPagedRequest(1, pageSize), Settings(pageSize: 100));

        result.PageSize.Should().Be(100);
    }

    [Fact]
    public void Create_CapsPageSize_AtMaxPageSize()
    {
        var result = RequestModifiers.Create(new TestPagedRequest(1, 5000), Settings(maxPageSize: 1000));

        result.PageSize.Should().Be(1000);
    }

    [Fact]
    public void Create_KeepsPageSize_WhenWithinRange()
    {
        var result = RequestModifiers.Create(new TestPagedRequest(1, 50), Settings(pageSize: 100, maxPageSize: 1000));

        result.PageSize.Should().Be(50);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_DefaultsPage_ToOne_WhenNullOrNonPositive(int? page)
    {
        var result = RequestModifiers.Create(new TestPagedRequest(page, 10), Settings());

        result.Page.Should().Be(1);
    }

    [Fact]
    public void Create_KeepsPage_WhenPositive()
    {
        var result = RequestModifiers.Create(new TestPagedRequest(7, 10), Settings());

        result.Page.Should().Be(7);
    }

    [Fact]
    public void Create_PassesOrderingThrough()
    {
        var result = RequestModifiers.Create(new TestPagedRequest(1, 10, "created", true), Settings());

        result.OrderBy.Should().Be("created");
        result.Descending.Should().BeTrue();
    }

    [Fact]
    public void GetOrderByParameter_ReturnsNull_WhenNoOrderBy()
    {
        var modifiers = RequestModifiers.Create(new TestPagedRequest(1, 10), Settings());

        modifiers.GetOrderByParameter().Should().BeNull();
    }

    [Fact]
    public void GetOrderByParameter_UsesOrderBy_WhenAscending()
    {
        var modifiers = RequestModifiers.Create(new TestPagedRequest(1, 10, "slug"), Settings());

        modifiers.GetOrderByParameter().Should().Be("orderBy=slug");
    }

    [Fact]
    public void GetOrderByParameter_UsesOrderByDescending_WhenDescending()
    {
        var modifiers = RequestModifiers.Create(new TestPagedRequest(1, 10, "slug", true), Settings());

        modifiers.GetOrderByParameter().Should().Be("orderByDescending=slug");
    }
}
