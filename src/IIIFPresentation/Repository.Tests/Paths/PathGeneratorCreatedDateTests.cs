using FakeItEasy;
using Models.Database;
using Models.Database.Collections;
using Models.Database.General;
using Repository.Paths;

namespace Repository.Tests.Paths;

/// <summary>
/// Verifies that <see cref="PathGeneratorBase"/> threads a resource's Created date through to the underlying
/// <see cref="IPresentationPathGenerator"/>, so implementations (e.g. legacy/default hostname selection) can use it.
/// </summary>
public class PathGeneratorCreatedDateTests
{
    private readonly IPresentationPathGenerator presentationPathGenerator = A.Fake<IPresentationPathGenerator>();
    private readonly IPathGenerator sut;

    public PathGeneratorCreatedDateTests()
    {
        sut = new TestPathGenerator(presentationPathGenerator);
    }

    [Fact]
    public void GenerateFlatCollectionId_PassesCollectionCreatedDate()
    {
        var created = new DateTime(2020, 1, 1);
        var collection = new Collection { Id = "test", Created = created };

        sut.GenerateFlatCollectionId(collection);

        A.CallTo(() => presentationPathGenerator.GetFlatPresentationPathForRequest(
            PresentationResourceType.CollectionPrivate, 0, "test", created)).MustHaveHappened();
    }

    [Fact]
    public void GenerateFlatManifestId_PassesManifestCreatedDate()
    {
        var created = new DateTime(2020, 1, 1);
        var manifest = new Manifest { Id = "test", CustomerId = 1, Created = created };

        sut.GenerateFlatManifestId(manifest);

        A.CallTo(() => presentationPathGenerator.GetFlatPresentationPathForRequest(
            PresentationResourceType.ManifestPrivate, 1, "test", created)).MustHaveHappened();
    }

    [Fact]
    public void GenerateCanvasId_PassesCanvasPaintingCreatedDate()
    {
        var created = new DateTime(2020, 1, 1);
        var canvasPainting = new CanvasPainting { Id = "test", CustomerId = 1, Created = created };

        sut.GenerateCanvasId(canvasPainting);

        A.CallTo(() => presentationPathGenerator.GetFlatPresentationPathForRequest(
            PresentationResourceType.Canvas, 1, "test", created)).MustHaveHappened();
    }

    [Fact]
    public void GenerateFlatParentId_PassesNullCreatedDate()
    {
        var hierarchy = new Hierarchy { Slug = "test", Parent = "parent" };

        sut.GenerateFlatParentId(hierarchy);

        A.CallTo(() => presentationPathGenerator.GetFlatPresentationPathForRequest(
            PresentationResourceType.CollectionPrivate, 0, "parent", null)).MustHaveHappened();
    }
}
