using System.Net;
using Amazon.S3;
using API.Tests.Integration.Infrastructure;
using Core.Response;
using DLCS.API;
using DLCS.Models;
using FakeItEasy;
using IIIF.Serialisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Models.API.Manifest;
using Models.Database.General;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository;
using Services.Manifests.Helpers;
using Test.Helpers;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using Batch = DLCS.Models.Batch;
using CanvasPainting = Models.Database.CanvasPainting;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class ModifyManifestManifestAdjunctTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    private readonly PresentationContext dbContext;
    private readonly IAmazonS3 amazonS3;
    private const int Customer = 1;
    private const int NewlyCreatedSpace = 999;
    private static readonly IDlcsApiClient DLCSApiClient = A.Fake<IDlcsApiClient>();
    private static readonly IDlcsOrchestratorClient DLCSOrchestratorClient = A.Fake<IDlcsOrchestratorClient>();

    public ModifyManifestManifestAdjunctTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = storageFixture.DbFixture.DbContext;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();

        Fake.ClearRecordedCalls(DLCSApiClient);

        A.CallTo(() => DLCSApiClient.CreateSpace(Customer, A<string>._, A<CancellationToken>._))
            .Returns(new Space { Id = NewlyCreatedSpace, Name = "test" });

        // Return a fresh batch ID for each IngestDeliverables call
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer, A<List<JObject>>._, A<bool>._, A<CancellationToken>._))
            .ReturnsLazily(_ => Task.FromResult(new List<Batch>
            {
                new() { ResourceId = TestIdentifiers.BatchId().ToString(), Submitted = DateTime.Now }
            }));

        // Default: no images exist in DLCS
        A.CallTo(() => DLCSApiClient.GetCustomerImages(Customer, A<ICollection<string>>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IList<JObject>>([]));

        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture),
            services => services
                .AddSingleton(DLCSApiClient)
                .AddSingleton(DLCSOrchestratorClient));

        storageFixture.DbFixture.CleanUp();
        dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task CreateManifest_WithManifestAdjuncts_CreatesStubAssetAndIngestsAdjuncts()
    {
        // Arrange
        var (slug, _) = TestIdentifiers.SlugResource();
        var adjunctId = "mets.xml";

        var payload = $$"""
            {
                "type": "Manifest",
                "slug": "{{slug}}",
                "parent": "http://localhost/{{Customer}}/collections/root",
                "adjuncts": [
                    { "id": "{{adjunctId}}", "mediaType": "text/xml", "iiifLink": "seeAlso" }
                ]
            }
            """;

        var request = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post,
            $"{Customer}/manifests", payload);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var manifestId = responseManifest!.Id!.Split('/').Last();

        var expectedManifestAdjunctId = ResourceAdjunctInteractions.GetResourceStubAssetId(new PresentationManifest(), Customer, manifestId).Asset;

        // Manifest-level adjunct asset should be created via regular queue (adjunctQueue = false) in space 0
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
                A<List<JObject>>.That.Matches(list =>
                    list.Count == 1 &&
                    list[0][AssetProperties.Id]!.Value<string>() == expectedManifestAdjunctId &&
                    list[0][AssetProperties.Space]!.Value<int>() == 0),
                false, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        // Adjuncts should be ingested via adjunct queue (adjunctQueue = true)
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
                A<List<JObject>>.That.Matches(list =>
                    list.Count == 1 &&
                    list[0][AdjunctProperties.Id]!.Value<string>() == adjunctId),
                true, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CreateManifest_WithManifestAdjuncts_ReturnsAdjunctsInResponse()
    {
        // Arrange
        var (slug, _) = TestIdentifiers.SlugResource();

        var payload = $$"""
            {
                "type": "Manifest",
                "slug": "{{slug}}",
                "parent": "http://localhost/{{Customer}}/collections/root",
                "adjuncts": [
                    { "id": "mets.xml", "mediaType": "text/xml" }
                ]
            }
            """;

        var request = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post,
            $"{Customer}/manifests", payload);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.Adjuncts.Should().HaveCount(1);
        responseManifest.Adjuncts![0]["id"]!.Value<string>().Should().Be("mets.xml");
        responseManifest.Adjuncts[0]["asset"].Should().BeNull("asset property should be stripped from response");
    }

    [Fact]
    public async Task CreateManifest_WithManifestAdjuncts_TracksBatchesInDb()
    {
        // Arrange
        var (slug, _) = TestIdentifiers.SlugResource();

        var payload = $$"""
            {
                "type": "Manifest",
                "slug": "{{slug}}",
                "parent": "http://localhost/{{Customer}}/collections/root",
                "adjuncts": [
                    { "id": "mets.xml", "mediaType": "text/xml" }
                ]
            }
            """;

        var request = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post,
            $"{Customer}/manifests", payload);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var manifestId = responseManifest!.Id!.Split('/').Last();

        var dbManifest = dbContext.Manifests
            .Include(m => m.Batches)
            .First(m => m.Id == manifestId);

        // One batch for the stub asset (Asset type) and one for the adjuncts (Adjunct type)
        dbManifest.Batches.Should().HaveCount(2);
        dbManifest.Batches.Should().Contain(b => b.DeliverableType == DeliverableType.Asset);
        dbManifest.Batches.Should().Contain(b => b.DeliverableType == DeliverableType.Adjunct);
    }

    [Fact]
    public async Task CreateManifest_WithManifestAdjuncts_WritesManifestToStagingLocation()
    {
        // Arrange
        var (slug, _) = TestIdentifiers.SlugResource();

        var payload = $$"""
            {
                "type": "Manifest",
                "slug": "{{slug}}",
                "parent": "http://localhost/{{Customer}}/collections/root",
                "adjuncts": [
                    { "id": "mets.xml", "mediaType": "text/xml" }
                ]
            }
            """;

        var request = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post,
            $"{Customer}/manifests", payload);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var manifestId = responseManifest!.Id!.Split('/').Last();

        // Manifest must be in staging (not final) so the background handler can pick it up
        var savedS3 = await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
            $"staging/{Customer}/manifests/{manifestId}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<IIIF.Presentation.V3.Manifest>();
        s3Manifest.Id.Should().EndWith(manifestId);
    }

    [Fact]
    public async Task CreateManifest_WithManifestAdjuncts_SetsManifestScopeOnStubAsset()
    {
        // Arrange
        var (slug, _) = TestIdentifiers.SlugResource();

        var payload = $$"""
            {
                "type": "Manifest",
                "slug": "{{slug}}",
                "parent": "http://localhost/{{Customer}}/collections/root",
                "adjuncts": [
                    { "id": "mets.xml", "mediaType": "text/xml" }
                ]
            }
            """;

        var request = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post,
            $"{Customer}/manifests", payload);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var manifestId = responseManifest!.Id!.Split('/').Last();

        // Stub asset should have the manifest ID in its scopes
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
                A<List<JObject>>.That.Matches(list =>
                    list.Count == 1 &&
                    list[0][AssetProperties.Manifests] != null &&
                    ((JArray)list[0][AssetProperties.Manifests]!).Any(m => m.Value<string>() == manifestId)),
                false, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CreateManifest_WithManifestAdjunctsAndCanvasAssets_CreatesBothStubAndCanvasAssets()
    {
        // Arrange
        var (slug, _) = TestIdentifiers.SlugResource();

        var payload = $$"""
            {
                "type": "Manifest",
                "slug": "{{slug}}",
                "parent": "http://localhost/{{Customer}}/collections/root",
                "adjuncts": [
                    { "id": "mets.xml", "mediaType": "text/xml" }
                ],
                "paintedResources": [
                    {
                        "asset": {
                            "id": "canvas-asset",
                            "origin": "https://example.com/image.jpg",
                            "mediaType": "image/jpeg"
                        }
                    }
                ]
            }
            """;

        var request = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post,
            $"{Customer}/manifests", payload);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(request);

        // Assert - 202 because canvas assets are ingesting
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var manifestId = responseManifest!.Id!.Split('/').Last();

        // Canvas asset and stub asset are batched together in one IngestDeliverables call
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
                A<List<JObject>>.That.Matches(list =>
                    list.Count == 2 &&
                    list.Any(a => a[AssetProperties.Id]!.Value<string>() == "canvas-asset") &&
                    list.Any(a =>
                        a[AssetProperties.Id]!.Value<string>() == ResourceAdjunctInteractions.GetResourceStubAssetId(new PresentationManifest(), Customer, manifestId).Asset &&
                        a[AssetProperties.Space]!.Value<int>() == 0)),
                false, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        // Adjunct batch
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
                A<List<JObject>>.That.Matches(list =>
                    list.Count == 1 && list[0][AdjunctProperties.Id]!.Value<string>() == "mets.xml"),
                true, A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Fact]
    public async Task UpdateManifest_AddingManifestAdjuncts_CreatesStubAssetAndIngestsAdjuncts()
    {
        // Arrange
        var (slug, id) = TestIdentifiers.SlugResource();
        var testManifest = await dbContext.Manifests.AddTestManifest(id: id, slug: slug,
            batchId: TestIdentifiers.BatchId(), ingested: true);
        await dbContext.SaveChangesAsync();

        var payload = $$"""
            {
                "type": "Manifest",
                "slug": "{{slug}}",
                "parent": "http://localhost/{{Customer}}/collections/root",
                "adjuncts": [
                    { "id": "mets.xml", "mediaType": "text/xml" }
                ]
            }
            """;

        var request = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/manifests/{id}", payload, dbContext.GetETag(testManifest));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Stub asset created
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
                A<List<JObject>>.That.Matches(list =>
                    list.Count == 1 &&
                    list[0][AssetProperties.Id]!.Value<string>() == ResourceAdjunctInteractions.GetResourceStubAssetId(new PresentationManifest(), Customer, id).Asset &&
                    list[0][AssetProperties.Space]!.Value<int>() == 0),
                false, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        // Adjuncts ingested
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
                A<List<JObject>>.That.Matches(list =>
                    list.Count == 1 && list[0][AdjunctProperties.Id]!.Value<string>() == "mets.xml"),
                true, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task UpdateManifest_ReplacingManifestAdjuncts_DeletesOldAndIngestsNew()
    {
        // Arrange
        var (slug, id) = TestIdentifiers.SlugResource();
        var existingAdjunctId = "old-mets.xml";
        var newAdjunctId = "new-mets.xml";
        var manifestAdjunctId = ResourceAdjunctInteractions.GetResourceStubAssetId(new PresentationManifest(), Customer, id).Asset;

        // Simulate: manifest-level adjunct asset exists in DLCS with an existing adjunct
        A.CallTo(() => DLCSApiClient.GetCustomerImages(Customer, A<ICollection<string>>._, A<CancellationToken>._))
            .ReturnsLazily((int _, ICollection<string> assetIds, CancellationToken _) =>
                Task.FromResult<IList<JObject>>(assetIds
                    .Where(a => a.EndsWith(manifestAdjunctId))
                    .Select(_ => JObject.Parse($$"""
                        {
                            "id": "{{manifestAdjunctId}}",
                            "space": 0,
                            "adjuncts": [{ "id": "{{existingAdjunctId}}" }]
                        }
                        """))
                    .ToList()));

        var testManifest = await dbContext.Manifests.AddTestManifest(id: id, slug: slug,
            batchId: TestIdentifiers.BatchId(), ingested: true);
        await dbContext.SaveChangesAsync();

        var payload = $$"""
            {
                "type": "Manifest",
                "slug": "{{slug}}",
                "parent": "http://localhost/{{Customer}}/collections/root",
                "adjuncts": [
                    { "id": "{{newAdjunctId}}", "mediaType": "text/xml" }
                ]
            }
            """;

        var request = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/manifests/{id}", payload, dbContext.GetETag(testManifest));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Old adjunct deleted
        A.CallTo(() => DLCSApiClient.DeleteAdjuncts(Customer,
                A<IEnumerable<AdjunctAssetIdentifier>>.That.Matches(list =>
                    list.Any(a => a.Adjunct.Contains(existingAdjunctId))),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        // New adjunct ingested
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
                A<List<JObject>>.That.Matches(list =>
                    list.Count == 1 && list[0][AdjunctProperties.Id]!.Value<string>() == newAdjunctId),
                true, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        // Stub asset NOT re-created (it already exists)
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
                A<List<JObject>>.That.Matches(list =>
                    list.Any(j => j[AssetProperties.Space] != null && j[AssetProperties.Space]!.Value<int>() == 0)),
                false, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task UpdateManifest_EmptyManifestAdjuncts_DeletesAllExistingAdjuncts()
    {
        // Arrange
        var (slug, id) = TestIdentifiers.SlugResource();
        var existingAdjunctId = "old-mets.xml";
        var manifestAdjunctId = ResourceAdjunctInteractions.GetResourceStubAssetId(new PresentationManifest(), Customer, id).Asset;

        // Simulate: manifest-level adjunct asset exists with existing adjunct
        A.CallTo(() => DLCSApiClient.GetCustomerImages(Customer, A<ICollection<string>>._, A<CancellationToken>._))
            .ReturnsLazily((int _, ICollection<string> assetIds, CancellationToken _) =>
                Task.FromResult<IList<JObject>>(assetIds
                    .Where(a => a.EndsWith(manifestAdjunctId))
                    .Select(_ => JObject.Parse($$"""
                        {
                            "id": "{{manifestAdjunctId}}",
                            "space": 0,
                            "adjuncts": [{ "id": "{{existingAdjunctId}}" }]
                        }
                        """))
                    .ToList()));

        var testManifest = await dbContext.Manifests.AddTestManifest(id: id, slug: slug,
            batchId: TestIdentifiers.BatchId(), ingested: true);
        await dbContext.SaveChangesAsync();

        var payload = $$"""
            {
                "type": "Manifest",
                "slug": "{{slug}}",
                "parent": "http://localhost/{{Customer}}/collections/root",
                "adjuncts": []
            }
            """;

        var request = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/manifests/{id}", payload, dbContext.GetETag(testManifest));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        responseManifest!.Adjuncts.Should().BeNull("clearing adjuncts returns no adjuncts property, not an empty array");

        // Existing adjunct deleted
        A.CallTo(() => DLCSApiClient.DeleteAdjuncts(Customer,
                A<IEnumerable<AdjunctAssetIdentifier>>.That.Matches(list =>
                    list.Any(a => a.Adjunct.Contains(existingAdjunctId))),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        // No new adjuncts ingested
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer, A<List<JObject>>._, true, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task UpdateManifest_NullManifestAdjuncts_LeavesAdjunctsAlone()
    {
        // Arrange
        var (slug, id) = TestIdentifiers.SlugResource();

        var testManifest = await dbContext.Manifests.AddTestManifest(id: id, slug: slug,
            batchId: TestIdentifiers.BatchId(), ingested: true);
        await dbContext.SaveChangesAsync();

        // No adjuncts property in payload
        var payload = $$"""
            {
                "type": "Manifest",
                "slug": "{{slug}}",
                "parent": "http://localhost/{{Customer}}/collections/root"
            }
            """;

        var request = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put,
            $"{Customer}/manifests/{id}", payload, dbContext.GetETag(testManifest));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // No DLCS calls for adjuncts
        A.CallTo(() => DLCSApiClient.DeleteAdjuncts(Customer, A<IEnumerable<AdjunctAssetIdentifier>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer, A<List<JObject>>._, true, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task UpdateManifest_WithTrackedAssets_ReturnsMergedAdjunctsFromStubCanvasInResponse()
    {
        // Arrange - existing manifest with an already-ingested canvas painting
        var (slug, id, assetId) = TestIdentifiers.SlugResourceAsset();
        const string seeAlsoId = "https://example.com/mets.xml";
        const string renderingId = "https://example.com/document.pdf";
        const string annotationId = "https://example.com/annotations/1";
        var stubAssetId = new AssetId(Customer, 0, $"Manifest_{id}");

        var canvasPainting = new CanvasPainting
        {
            Id = "cp1",
            CanvasOrder = 1,
            ChoiceOrder = 1,
            AssetId = new AssetId(Customer, NewlyCreatedSpace, assetId)
        };

        var testManifest = await dbContext.Manifests.AddTestManifest(id: id, slug: slug,
            canvasPaintings: new List<CanvasPainting> { canvasPainting }, batchId: TestIdentifiers.BatchId(),
            ingested: true, spaceId: NewlyCreatedSpace);
        await dbContext.SaveChangesAsync();

        // NQ manifest includes the canvas AND stub canvas carrying all three manifest-level adjunct types
        A.CallTo(() => DLCSOrchestratorClient.RetrieveAssetsForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily(() => ManifestTestCreator.New()
                .WithCanvas(new AssetId(Customer, NewlyCreatedSpace, assetId), c => c.WithImage())
                .WithCanvas(stubAssetId, c => c.WithImage()
                    .WithAdjunctSeeAlso(seeAlsoId)
                    .WithAdjunctRendering(renderingId)
                    .WithAdjunctAnnotation(annotationId))
                .Build());

        // PUT with same tracked asset and no adjuncts → canBeBuiltUpfront = true (no new batches)
        var payload = $$"""
            {
                "type": "Manifest",
                "slug": "{{slug}}",
                "parent": "http://localhost/{{Customer}}/collections/root",
                "paintedResources": [
                    {
                        "canvasPainting": { "canvasOrder": 1 },
                        "asset": { "id": "{{assetId}}", "mediaType": "image/jpg" }
                    }
                ]
            }
            """;

        var request = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
            payload, dbContext.GetETag(testManifest));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        responseManifest!.SeeAlso.Should().ContainSingle(s => s.Id == seeAlsoId,
            "manifest-level seeAlso from stub canvas must appear in the write response");
        responseManifest.Rendering.Should().ContainSingle(r => r.Id == renderingId,
            "manifest-level rendering from stub canvas must appear in the write response");
        responseManifest.Annotations.Should().ContainSingle(a => a.Id == annotationId,
            "manifest-level annotations from stub canvas must appear in the write response");

        var savedS3 = await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName, $"{Customer}/manifests/{id}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<IIIF.Presentation.V3.Manifest>();
        s3Manifest.SeeAlso.Should().ContainSingle(s => s.Id == seeAlsoId,
            "manifest-level seeAlso from stub canvas must be persisted to S3");
        s3Manifest.Rendering.Should().ContainSingle(r => r.Id == renderingId,
            "manifest-level rendering from stub canvas must be persisted to S3");
        s3Manifest.Annotations.Should().ContainSingle(a => a.Id == annotationId,
            "manifest-level annotations from stub canvas must be persisted to S3");
    }

    [Fact]
    public async Task CreateManifest_WithOnlyManifestAdjuncts_WhenBatchesCompleteUpfront_WritesSeeAlsoToS3()
    {
        // Arrange
        var (slug, _) = TestIdentifiers.SlugResource();
        const string seeAlsoId = "https://example.com/mets.xml";

        // Override: batches complete immediately (Finished is set) → canBeBuiltUpfront = true
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer, A<List<JObject>>._, A<bool>._, A<CancellationToken>._))
            .ReturnsLazily(_ => Task.FromResult(new List<Batch>
            {
                new() { ResourceId = TestIdentifiers.BatchId().ToString(), Submitted = DateTime.Now, Finished = DateTime.Now }
            }));

        // NQ manifest carries only the stub canvas with a seeAlso adjunct
        A.CallTo(() => DLCSOrchestratorClient.RetrieveAssetsForManifest(A<int>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily((int _, string manifestId, CancellationToken _) =>
            {
                var stubAssetId = new AssetId(Customer, 0, $"Manifest_{manifestId}");
                return ManifestTestCreator.New()
                    .WithCanvas(stubAssetId, c => c.WithImage().WithAdjunctSeeAlso(seeAlsoId))
                    .Build();
            });

        var payload = $$"""
            {
                "type": "Manifest",
                "slug": "{{slug}}",
                "parent": "http://localhost/{{Customer}}/collections/root",
                "adjuncts": [
                    { "id": "mets.xml", "mediaType": "text/xml", "iiifLink": "seeAlso" }
                ]
            }
            """;

        var request = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post,
            $"{Customer}/manifests", payload);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var manifestId = responseManifest!.Id!.Split('/').Last();

        responseManifest.SeeAlso.Should().ContainSingle(s => s.Id == seeAlsoId,
            "manifest-level seeAlso from stub canvas must appear in the write response");

        var savedS3 = await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
            $"{Customer}/manifests/{manifestId}");
        var s3Manifest = savedS3.ResponseStream.FromJsonStream<IIIF.Presentation.V3.Manifest>();
        s3Manifest.SeeAlso.Should().ContainSingle(s => s.Id == seeAlsoId,
            "manifest-level seeAlso from stub canvas must be persisted to real S3 location");
    }

    [Fact]
    public async Task CreateManifest_WithInvalidAdjunctId_Returns400()
    {
        // Arrange
        var (slug, _) = TestIdentifiers.SlugResource();

        var payload = $$"""
            {
                "type": "Manifest",
                "slug": "{{slug}}",
                "parent": "http://localhost/{{Customer}}/collections/root",
                "adjuncts": [
                    { "mediaType": "text/xml" }
                ]
            }
            """;

        var request = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post,
            $"{Customer}/manifests", payload);

        // Act
        var response = await httpClient.AsCustomer().SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateManifest_WithManifestAdjuncts_ReturnsAdjunctsPopulatedFromDlcs()
    {
        // Arrange: create a manifest with adjuncts via POST
        var (slug, _) = TestIdentifiers.SlugResource();
        var adjunctId = "mets.xml";

        var postPayload = $$"""
            {
                "type": "Manifest",
                "slug": "{{slug}}",
                "parent": "http://localhost/{{Customer}}/collections/root",
                "adjuncts": [
                    { "id": "{{adjunctId}}", "mediaType": "text/xml" }
                ]
            }
            """;

        var postResponse = await httpClient.AsCustomer().SendAsync(
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", postPayload));
        postResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var manifestId = (await postResponse.ReadAsPresentationResponseAsync<PresentationManifest>())!.Id!.Split('/').Last();

        // Mock DLCS to return the manifest-level adjunct asset with adjuncts for the manifest-scoped lookup
        var manifestAdjunctId = ResourceAdjunctInteractions.GetResourceStubAssetId(new PresentationManifest(), Customer, manifestId).Asset;
        A.CallTo(() => DLCSApiClient.GetCustomerImages(Customer, manifestId, A<CancellationToken>._))
            .Returns(Task.FromResult<IList<JObject>>(
            [
                JObject.Parse($$"""
                    {
                        "@id": "https://localhost/customers/{{Customer}}/spaces/0/images/{{manifestAdjunctId}}",
                        "id": "{{manifestAdjunctId}}",
                        "space": 0,
                        "adjuncts": [{ "id": "{{adjunctId}}", "mediaType": "text/xml" }]
                    }
                    """)
            ]));

        // Act
        var getResponse = await httpClient.AsCustomer().SendAsync(
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Get, $"{Customer}/manifests/{manifestId}"));

        // Assert - 202 because the adjunct batches are still in Ingesting state
        getResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var manifest = await getResponse.ReadAsPresentationResponseAsync<PresentationManifest>();
        manifest!.Adjuncts.Should().HaveCount(1);
        manifest.Adjuncts![0]["id"]!.Value<string>().Should().Be(adjunctId);

        // Stub asset was sent to DLCS (space 0, adjunctQueue = false)
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
                A<List<JObject>>.That.Matches(list =>
                    list.Count == 1 &&
                    list[0][AssetProperties.Id]!.Value<string>() == ResourceAdjunctInteractions.GetResourceStubAssetId(new PresentationManifest(), Customer, manifestId).Asset &&
                    list[0][AssetProperties.Space]!.Value<int>() == 0),
                false, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        // Adjunct was sent to DLCS (adjunctQueue = true)
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
                A<List<JObject>>.That.Matches(list =>
                    list.Count == 1 &&
                    list[0][AdjunctProperties.Id]!.Value<string>() == adjunctId),
                true, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

}
