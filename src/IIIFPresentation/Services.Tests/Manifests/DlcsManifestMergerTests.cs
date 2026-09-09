using System.Net;
using DLCS.API;
using DLCS.Exceptions;
using FakeItEasy;
using IIIF.Presentation.V3;
using Microsoft.Extensions.Logging.Abstractions;
using Models.Database;
using Services.Manifests;
using DbManifest = Models.Database.Collections.Manifest;

namespace Services.Tests.Manifests;

public class DlcsManifestMergerTests
{
    private readonly IDlcsOrchestratorClient dlcsOrchestratorClient = A.Fake<IDlcsOrchestratorClient>();
    private readonly IManifestMerger manifestMerger = A.Fake<IManifestMerger>();
    private readonly DlcsManifestMerger sut;

    private static readonly DbManifest DbManifest = new() { Id = "my-manifest", CustomerId = 99 };

    public DlcsManifestMergerTests()
    {
        sut = new DlcsManifestMerger(dlcsOrchestratorClient, manifestMerger, new NullLogger<DlcsManifestMerger>());
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
                A<List<CanvasPainting>?>._, DbManifest.CustomerId, DbManifest.Id))
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
            A<int>._, A<string>._)).MustNotHaveHappened();
    }
}
