using System.Net;
using DLCS.API;
using DLCS.Exceptions;
using FakeItEasy;
using IIIF.Presentation.V3;
using DLCS;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.Database;
using Models.Database.General;
using Repository;
using Repository.Paths;
using Services.Manifests;
using Services.Manifests.Helpers;
using Services.Manifests.Settings;
using DbManifest = Models.Database.Collections.Manifest;

namespace Services.Tests.Manifests;

public class DlcsManifestMergerTests
{
    private readonly IDlcsOrchestratorClient dlcsOrchestratorClient = A.Fake<IDlcsOrchestratorClient>();
    private readonly IManifestMerger manifestMerger = A.Fake<IManifestMerger>();
    private readonly IPathGenerator pathGenerator = A.Fake<IPathGenerator>();
    private readonly DlcsManifestMerger sut;

    private const string FullPath = "some/path/my-manifest";
    private const string PublicId = "https://localhost/99/some/path/my-manifest";

    // FullPath already populated, as it is after an API write, so no database lookup is needed
    private static readonly DbManifest DbManifest = new()
    {
        Id = "my-manifest",
        CustomerId = 99,
        Hierarchy = [new Hierarchy { Slug = "my-manifest", CustomerId = 99, Canonical = true, FullPath = FullPath }]
    };

    public DlcsManifestMergerTests()
    {
        // No customer-specific paths configured, so the public id comes from pathGenerator
        var settingsBasedPathGenerator = new SettingsBasedPathGenerator(
            Options.Create(new DlcsSettings { ApiUri = new Uri("https://dlcs.api") }),
            new SettingsDrivenPresentationConfigGenerator(Options.Create(new PathSettings
            {
                PresentationApiUrl = new Uri("https://localhost")
            })));
        A.CallTo(() => pathGenerator.GenerateHierarchicalId(
                A<Hierarchy>.That.Matches(h => h.FullPath == FullPath)))
            .Returns(PublicId);
        sut = new DlcsManifestMerger(dlcsOrchestratorClient, manifestMerger, pathGenerator,
            settingsBasedPathGenerator, A.Fake<PresentationContext>(), new NullLogger<DlcsManifestMerger>());
    }

    [Fact]
    public async Task Augment_MergesNamedQueryManifest_OnSuccess()
    {
        var namedQueryManifest = new Manifest { Id = "https://example.com/nq" };
        var merged = new Manifest { Id = "https://example.com/merged" };
        A.CallTo(() => dlcsOrchestratorClient.RetrieveAssetsForManifest(DbManifest.CustomerId, DbManifest.Id,
                A<CancellationToken>._))
            .Returns(namedQueryManifest);
        A.CallTo(() => manifestMerger.MergeManifest(A<Manifest>._, namedQueryManifest,
                A<List<CanvasPainting>?>._, DbManifest.CustomerId, DbManifest.Id, PublicId))
            .Returns(merged);

        var baseManifest = new Manifest { Id = "https://example.com/manifest" };

        var result = await sut.Augment(baseManifest, DbManifest, CancellationToken.None);

        result.Should().BeSameAs(merged);
    }

    [Fact]
    public async Task Augment_ThrowsSimpleGenericError_WhenNamedQueryCallFails()
    {
        // The underlying error could be a raw/unhelpful downstream response (e.g. HTML from a 404 that doesn't
        // reliably mean anything specific) - it should never be passed straight back to the caller
        var namedQueryError = new DlcsException("<html>404 not found</html>", HttpStatusCode.NotFound);
        A.CallTo(() => dlcsOrchestratorClient.RetrieveAssetsForManifest(DbManifest.CustomerId, DbManifest.Id,
                A<CancellationToken>._))
            .Throws(namedQueryError);

        var baseManifest = new Manifest { Id = "https://example.com/manifest" };

        Func<Task> action = () => sut.Augment(baseManifest, DbManifest, CancellationToken.None);

        var thrown = await action.Should().ThrowAsync<DlcsException>();
        thrown.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
        thrown.Which.InnerException.Should().Be(namedQueryError);
        thrown.Which.Message.Should().Contain(DbManifest.Id).And.NotContain("<html>");

        A.CallTo(() => manifestMerger.MergeManifest(A<Manifest>._, A<Manifest?>._, A<List<CanvasPainting>?>._,
            A<int>._, A<string>._, A<string>._)).MustNotHaveHappened();
    }
}
