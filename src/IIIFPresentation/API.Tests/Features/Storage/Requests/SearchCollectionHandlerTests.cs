using API.Features.Storage.Requests;
using API.Settings;
using API.Tests.Integration.Infrastructure;
using AWS.Settings;
using Core.Web;
using IIIF.Presentation.V3.Strings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Repository;
using Services.Manifests.Helpers;
using Services.Manifests.Settings;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace API.Tests.Features.Storage.Requests;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class SearchCollectionHandlerTests
{
    private readonly PresentationContextFixture dbFixture;
    private const int Customer = 641;

    public SearchCollectionHandlerTests(PresentationContextFixture dbFixture)
    {
        this.dbFixture = dbFixture;
        dbFixture.CleanUp();
    }

    private PresentationContext GetScopedContext()
    {
        var provider = new TestCustomerIdProvider();
        provider.SetCustomerId(Customer);
        return dbFixture.GetNewPresentationContext(provider);
    }

    private static SearchCollectionHandler GetSut(PresentationContext dbContext)
    {
        var dlcsSettings = DefaultSettings.DlcsSettings();
        var settingsBasedPathGenerator = new SettingsBasedPathGenerator(Options.Create(dlcsSettings),
            new SettingsDrivenPresentationConfigGenerator(Options.Create(new PathSettings
            {
                PresentationApiUrl = new Uri("https://presentation.api"),
                PathRules = PathRewriteOptions.Default
            })));

        var apiSettings = Options.Create(new ApiSettings
        {
            AWS = new AWSSettings(),
            DLCS = dlcsSettings
        });

        return new SearchCollectionHandler(dbContext, settingsBasedPathGenerator, apiSettings,
            new NullLogger<SearchCollectionHandler>());
    }

    /// <summary>
    /// Gets a context for the customer, ensuring they have the root collection they'd get on creation. CleanUp()
    /// preserves 'root' for all customers, so this is idempotent across tests sharing the fixture.
    /// </summary>
    private async Task<PresentationContext> GetSeededContext()
    {
        var ctx = GetScopedContext();
        if (!await ctx.Collections.AnyAsync(c => c.Id == RootCollection.Id))
        {
            await ctx.Collections.AddTestRootCollection(Customer);
            await ctx.SaveChangesAsync();
        }

        return ctx;
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenCollectionDoesNotExist()
    {
        // Arrange
        await using var ctx = GetScopedContext();

        // Act
        var result = await GetSut(ctx).Handle(
            new SearchCollection("i-do-not-exist", "thompson", ["thompson"], 1, 10), CancellationToken.None);

        // Assert
        result.EntityNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ReturnsBadRequest_WhenCollectionIsNotStorageCollection()
    {
        // Arrange
        await using var ctx = GetScopedContext();
        await ctx.Collections.AddTestCollection(id: "iiif-coll", customer: Customer, parent: null, isStorage: false);
        await ctx.SaveChangesAsync();

        // Act - unreachable via the API today (search is root-only) but the handler must still refuse
        var result = await GetSut(ctx).Handle(new SearchCollection("iiif-coll", "thompson", ["thompson"], 1, 10),
            CancellationToken.None);

        // Assert
        result.BadRequest.Should().BeTrue();
        result.Error.Should().BeTrue("BadRequest must not be mistaken for success by callers that ignore it");
        result.Entity.Should().BeNull();
        result.ErrorMessage.Should().Be("Search is only supported for the root storage collection");
    }

    [Fact]
    public async Task Handle_ReturnsBadRequest_WhenStorageCollectionIsNotRoot()
    {
        // Arrange
        await using var ctx = await GetSeededContext();
        var storageCollection = (await ctx.Collections.AddTestCollection(id: "storage-coll", customer: Customer)).Entity;
        storageCollection.Label = new LanguageMap("en", ["Hunter S. Thompson"]);
        await ctx.SaveChangesAsync();

        // Act - the search query is customer-wide, so a non-root collection would return items from outside itself
        var result = await GetSut(ctx).Handle(new SearchCollection("storage-coll", "thompson", ["thompson"], 1, 10),
            CancellationToken.None);

        // Assert
        result.BadRequest.Should().BeTrue();
        result.Entity.Should().BeNull();
        result.ErrorMessage.Should().Be("Search is only supported for the root storage collection");
    }

    [Fact]
    public async Task Handle_ReturnsResults_WhenCollectionIsRoot()
    {
        // Arrange
        await using var ctx = await GetSeededContext();
        await ctx.Manifests.AddTestManifest(id: "hst-man", customer: Customer,
            label: new LanguageMap("en", ["Hunter S. Thompson"]));
        await ctx.SaveChangesAsync();

        // Act
        var result = await GetSut(ctx).Handle(
            new SearchCollection(RootCollection.Id, "thompson", ["thompson"], 1, 10), CancellationToken.None);

        // Assert
        result.BadRequest.Should().BeFalse();
        result.Error.Should().BeFalse();
        result.Entity!.TotalItems.Should().Be(1);
    }
}
