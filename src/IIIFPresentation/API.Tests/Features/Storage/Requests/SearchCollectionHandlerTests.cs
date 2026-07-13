using API.Features.Storage.Requests;
using API.Settings;
using API.Tests.Integration.Infrastructure;
using AWS.Settings;
using Core.Web;
using IIIF.Presentation.V3.Strings;
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

        return new SearchCollectionHandler(dbContext, settingsBasedPathGenerator, settingsBasedPathGenerator,
            apiSettings);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenCollectionDoesNotExist()
    {
        // Arrange
        await using var ctx = GetScopedContext();

        // Act
        var result = await GetSut(ctx).Handle(new SearchCollection("i-do-not-exist", "thompson", 1, 10),
            CancellationToken.None);

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
        var result = await GetSut(ctx).Handle(new SearchCollection("iiif-coll", "thompson", 1, 10),
            CancellationToken.None);

        // Assert
        result.BadRequest.Should().BeTrue();
        result.Error.Should().BeTrue("BadRequest must not be mistaken for success by callers that ignore it");
        result.Entity.Should().BeNull();
        result.ErrorMessage.Should().Be("Search is only supported for storage collections");
    }

    [Fact]
    public async Task Handle_ReturnsResults_WhenCollectionIsStorageCollection()
    {
        // Arrange
        await using var ctx = GetScopedContext();
        var storageCollection =
            (await ctx.Collections.AddTestCollection(id: "storage-coll", customer: Customer, parent: null)).Entity;
        storageCollection.Label = new LanguageMap("en", ["Hunter S. Thompson"]);
        await ctx.SaveChangesAsync();

        // Act
        var result = await GetSut(ctx).Handle(new SearchCollection("storage-coll", "thompson", 1, 10),
            CancellationToken.None);

        // Assert
        result.BadRequest.Should().BeFalse();
        result.Error.Should().BeFalse();
        result.Entity!.TotalItems.Should().Be(1);
    }
}
