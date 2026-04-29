using System.Net;
using API.Tests.Integration.Infrastructure;
using Core.Response;
using DLCS.API;
using DLCS.Models;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Models.API.Manifest;
using Models.Database.General;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository;
using Test.Helpers;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;
using Batch = DLCS.Models.Batch;
using CanvasPainting = Models.Database.CanvasPainting;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class ModifyManifestAdjunctUpdateTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    private readonly PresentationContext dbContext;
    private const int Customer = 1;
    private const int NewlyCreatedSpace = 999;
    private static readonly IDlcsApiClient DLCSApiClient = A.Fake<IDlcsApiClient>();
    private static readonly IDlcsOrchestratorClient DLCSOrchestratorClient = A.Fake<IDlcsOrchestratorClient>();

    public ModifyManifestAdjunctUpdateTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = storageFixture.DbFixture.DbContext;
        
        A.CallTo(() => DLCSApiClient.CreateSpace(Customer, A<string>._, A<CancellationToken>._))
            .Returns(new Space { Id = NewlyCreatedSpace, Name = "test" });

        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer, A<List<JObject>>._, A<bool>._, A<CancellationToken>._))
            .ReturnsLazily(x => Task.FromResult(
                new List<Batch> { new()
                {
                    ResourceId = x.Arguments.Get<List<JObject>>("deliverables").First().GetValue("batch").ToString(),
                    Submitted = DateTime.Now
                }}));

        A.CallTo(() => DLCSApiClient.GetCustomerImages(Customer,
                A<ICollection<string>>._, A<CancellationToken>._))
            .ReturnsLazily(x =>
                Task.FromResult<IList<JObject>>(new List<JObject>()));

        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture),
            services =>
                services
                    .AddSingleton(DLCSApiClient)
                    .AddSingleton(DLCSOrchestratorClient));

        storageFixture.DbFixture.CleanUp();
        dbContext.ChangeTracker.Clear();
    }
    
    [Fact]
    public async Task UpdateManifest_MakesChangesToAdjuncts_WhenAssetOnADifferentManifest()
    {
        // This test checks that an adjunct on an asset will be replaced (i.e.: 1 ingested for replacement, with no removal)
        // when a known asset is set to reingest

        // Arrange
        var (slug, id, assetId, existingAdjunctId) = TestIdentifiers.SlugResourceAssetAdjunct();

        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = $"{id}_first",
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_1")
            }
        };
        
        var initialCanvasPaintingsAnotherManifest = new List<CanvasPainting>
        {
            new()
            {
                Id = $"{id}_second",
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"fromDlcs_{assetId}_1")
            }
        };

        A.CallTo(() => DLCSApiClient.GetCustomerImages(Customer,
                A<ICollection<string>>._,
                A<CancellationToken>._)).ReturnsLazily(x => Task.FromResult((IList<JObject>)[])).Once().Then
            .ReturnsLazily((int customerId, ICollection<string> assetIds, CancellationToken can) =>
                Task.FromResult((IList<JObject>)assetIds
                    .Where(a => a.Split('/', StringSplitOptions.None).Last().StartsWith("fromDlcs_"))
                    .Select(x => JObject.Parse($$"""
                                                 {
                                                   "id": "{{x.Split('/').Last()}}",
                                                   "space": {{NewlyCreatedSpace}},
                                                   "adjuncts" : [{"id" : "{{existingAdjunctId}}"}]
                                                 }
                                                 """)).ToList()));

        var testManifest = await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: TestIdentifiers.BatchId(), ingested: true, spaceId: NewlyCreatedSpace);
        await dbContext.Manifests.AddTestManifest(id: $"{id}_2", slug: $"{slug}_2", canvasPaintings: initialCanvasPaintingsAnotherManifest,
            batchId: TestIdentifiers.BatchId(), ingested: true, spaceId: NewlyCreatedSpace);
        
        await dbContext.SaveChangesAsync();

        var batchId = TestIdentifiers.BatchId();
        var adjunctBatchId = TestIdentifiers.BatchId();
        
        var manifestWithSpace = $$"""
                          {
                              "type": "Manifest",
                              "slug": "{{slug}}",
                              "parent": "http://localhost/{{Customer}}/collections/root",
                              "paintedResources": [
                                  {
                                     "canvasPainting":{
                                        "canvasOrder": 1
                                     },
                                      "asset": {
                                          "id": "fromDlcs_{{assetId}}_1",
                                          "mediaType": "image/jpg",
                                          "batch": {{batchId}},
                                          "space": {{NewlyCreatedSpace}},
                                          "adjuncts": [
                                            {
                                                "id": "{{existingAdjunctId}}",
                                                "batch": {{adjunctBatchId}}
                                            }
                                          ]
                                      },
                                      "reingest": true
                                  }
                              ]
                          }
                          """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                manifestWithSpace, dbContext.GetETag(testManifest));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.PaintedResources.Should().HaveCount(1);

        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());

        dbManifest.CanvasPaintings.First(cp => cp.CanvasOrder == 1).Should().NotBeNull("asset added to manifest");

        // asset reingested
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
            A<List<JObject>>.That.Matches(o => o.Single().GetValue("id")!.ToString() == $"fromDlcs_{assetId}_1"),
            A<bool>._, A<CancellationToken>._)).MustHaveHappened();

        // deleted the adjunct returned from GetCustomerImages
        A.CallTo(() => DLCSApiClient.DeleteAdjuncts(Customer,
            A<List<AdjunctAssetIdentifier>>.That.Matches(a => a.Single().Adjunct.Single() == existingAdjunctId),
            A<CancellationToken>._)).MustNotHaveHappened();

        // new adjunct ingested
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
            A<List<JObject>>.That.Matches(o => o.Single().GetValue("id")!.ToString() == existingAdjunctId),
            A<bool>._, A<CancellationToken>._)).MustHaveHappened();

        dbManifest.Batches.Should().HaveCount(3); // initial batch from setup + asset batch + adjunct batch
        dbManifest.Batches[1].DeliverableType.Should().Be(DeliverableType.Asset);
        dbManifest.Batches.Last().DeliverableType.Should().Be(DeliverableType.Adjunct);
    }
    
    [Fact]
    public async Task UpdateManifest_ReplacesAdjuncts_WhenNewAdjunctOnKnownAsset()
    {
        // This test checks that an adjunct on an asset will be replaced (i.e.: 1 removed, 1 added)
        // when a known asset is set to reingest

        // Arrange
        var (slug, id, assetId, existingAdjunctId) = TestIdentifiers.SlugResourceAssetAdjunct();

        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = "first",
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_1")
            }
        };

        A.CallTo(() => DLCSApiClient.GetCustomerImages(Customer,
                A<ICollection<string>>._,
                A<CancellationToken>._)).ReturnsLazily(x => Task.FromResult((IList<JObject>)[])).Once().Then
            .ReturnsLazily((int customerId, ICollection<string> assetIds, CancellationToken can) =>
                Task.FromResult((IList<JObject>)assetIds
                    .Where(a => a.Split('/', StringSplitOptions.None).Last().StartsWith("fromDlcs_"))
                    .Select(x => JObject.Parse($$"""
                                                 {
                                                   "id": "{{x.Split('/').Last()}}",
                                                   "space": {{NewlyCreatedSpace}},
                                                   "adjuncts" : [{"id" : "{{existingAdjunctId}}"}]
                                                 }
                                                 """)).ToList()));

        var testManifest = await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: TestIdentifiers.BatchId(), ingested: true, spaceId: NewlyCreatedSpace);
        await dbContext.SaveChangesAsync();

        var batchId = TestIdentifiers.BatchId();
        var adjunctBatchId = TestIdentifiers.BatchId();
        var newAdjunctId = "different";

        var manifestWithSpace = $$"""
                          {
                              "type": "Manifest",
                              "slug": "{{slug}}",
                              "parent": "http://localhost/{{Customer}}/collections/root",
                              "paintedResources": [
                                  {
                                     "canvasPainting":{
                                        "canvasOrder": 1
                                     },
                                      "asset": {
                                          "id": "fromDlcs_{{assetId}}_1",
                                          "mediaType": "image/jpg",
                                          "batch": {{batchId}},
                                          "space": {{NewlyCreatedSpace}},
                                          "adjuncts": [
                                            {
                                                "id": "{{newAdjunctId}}",
                                                "batch": {{adjunctBatchId}}
                                            }
                                          ]
                                      },
                                      "reingest": true
                                  }
                              ]
                          }
                          """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                manifestWithSpace, dbContext.GetETag(testManifest));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.PaintedResources.Should().HaveCount(1);

        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());

        dbManifest.CanvasPaintings.First(cp => cp.CanvasOrder == 1).Should().NotBeNull("asset added to manifest");

        // asset reingested
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
            A<List<JObject>>.That.Matches(o => o.Count == 1 && o.First().GetValue("id")!.ToString() == $"fromDlcs_{assetId}_1"),
            A<bool>._, A<CancellationToken>._)).MustHaveHappened();

        // deleted the adjunct returned from GetCustomerImages
        A.CallTo(() => DLCSApiClient.DeleteAdjuncts(Customer,
            A<List<AdjunctAssetIdentifier>>.That.Matches(a => a.Count == 1 && a.First().Adjunct.Single() == existingAdjunctId),
            A<CancellationToken>._)).MustHaveHappened();

        // new adjunct ingested
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
            A<List<JObject>>.That.Matches(o => o.Count == 1 && o.First().GetValue("id")!.ToString() == newAdjunctId),
            A<bool>._, A<CancellationToken>._)).MustHaveHappened();

        dbManifest.Batches.Should().HaveCount(3); // initial batch from setup + asset batch + adjunct batch
        dbManifest.Batches[1].DeliverableType.Should().Be(DeliverableType.Asset);
        dbManifest.Batches.Last().DeliverableType.Should().Be(DeliverableType.Adjunct);
    }
    
    [Fact]
    public async Task UpdateManifest_LeavesAdjunctsAlone_WhenNewAdjunctOnKnownAsset()
    {
        // This test checks that an adjunct on an asset will be replaced (i.e.: 1 added)
        // when a known asset is set to reingest

        // Arrange
        var (slug, id, assetId, existingAdjunctId) = TestIdentifiers.SlugResourceAssetAdjunct();

        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = "first",
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_1")
            }
        };

        A.CallTo(() => DLCSApiClient.GetCustomerImages(Customer,
                A<ICollection<string>>._,
                A<CancellationToken>._)).ReturnsLazily(x => Task.FromResult((IList<JObject>)[])).Once().Then
            .ReturnsLazily((int customerId, ICollection<string> assetIds, CancellationToken can) =>
                Task.FromResult((IList<JObject>)assetIds
                    .Where(a => a.Split('/', StringSplitOptions.None).Last().StartsWith("fromDlcs_"))
                    .Select(x => JObject.Parse($$"""
                                                 {
                                                   "id": "{{x.Split('/').Last()}}",
                                                   "space": {{NewlyCreatedSpace}},
                                                   "adjuncts" : [{"id" : "{{existingAdjunctId}}"}]
                                                 }
                                                 """)).ToList()));

        var testManifest = await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: TestIdentifiers.BatchId(), ingested: true, spaceId: NewlyCreatedSpace);
        await dbContext.SaveChangesAsync();

        var batchId = TestIdentifiers.BatchId();
        var adjunctBatchId = TestIdentifiers.BatchId();

        var manifestWithSpace = $$"""
                          {
                              "type": "Manifest",
                              "slug": "{{slug}}",
                              "parent": "http://localhost/{{Customer}}/collections/root",
                              "paintedResources": [
                                  {
                                     "canvasPainting":{
                                        "canvasOrder": 1
                                     },
                                      "asset": {
                                          "id": "fromDlcs_{{assetId}}_1",
                                          "mediaType": "image/jpg",
                                          "batch": {{batchId}},
                                          "space": {{NewlyCreatedSpace}},
                                          "adjuncts": [
                                            {
                                                "id": "{{existingAdjunctId}}",
                                                "batch": {{adjunctBatchId}}
                                            }
                                          ]
                                      },
                                      "reingest": true
                                  }
                              ]
                          }
                          """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                manifestWithSpace, dbContext.GetETag(testManifest));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.PaintedResources.Should().HaveCount(1);

        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());

        dbManifest.CanvasPaintings.First(cp => cp.CanvasOrder == 1).Should().NotBeNull("asset added to manifest");

        // asset reingested
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
            A<List<JObject>>.That.Matches(o => o.Count == 1 && o.First().GetValue("id")!.ToString() == $"fromDlcs_{assetId}_1"),
            A<bool>._, A<CancellationToken>._)).MustHaveHappened();

        // no adjunct deletion occurs
        A.CallTo(() => DLCSApiClient.DeleteAdjuncts(Customer,
            A<List<AdjunctAssetIdentifier>>.That.Matches(a => a.Count == 1 && a.First().Adjunct.Single() == existingAdjunctId),
            A<CancellationToken>._)).MustNotHaveHappened();

        // new adjunct ingested
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
            A<List<JObject>>.That.Matches(o => o.Count == 1 && o.First().GetValue("id")!.ToString() == existingAdjunctId),
            A<bool>._, A<CancellationToken>._)).MustHaveHappened();

        dbManifest.Batches.Should().HaveCount(3); // initial batch from setup + asset batch + adjunct batch
        dbManifest.Batches[1].DeliverableType.Should().Be(DeliverableType.Asset);
        dbManifest.Batches.Last().DeliverableType.Should().Be(DeliverableType.Adjunct);
    }
    
    [Fact]
    public async Task UpdateManifest_RemovesAdjunct_WhenEmptyAdjunctOnKnownAsset()
    {
        // This test checks that an adjunct on an asset will be removed
        // when a known asset is set to reingest with adjuncts set to an empty list

        // Arrange
        var (slug, id, assetId, existingAdjunctId) = TestIdentifiers.SlugResourceAssetAdjunct();

        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = "first",
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_1")
            }
        };

        A.CallTo(() => DLCSApiClient.GetCustomerImages(Customer,
                A<ICollection<string>>._,
                A<CancellationToken>._)).ReturnsLazily(x => Task.FromResult((IList<JObject>)[])).Once().Then
            .ReturnsLazily((int customerId, ICollection<string> assetIds, CancellationToken can) =>
                Task.FromResult((IList<JObject>)assetIds
                    .Where(a => a.Split('/', StringSplitOptions.None).Last().StartsWith("fromDlcs_"))
                    .Select(x => JObject.Parse($$"""
                                                 {
                                                   "id": "{{x.Split('/').Last()}}",
                                                   "space": {{NewlyCreatedSpace}},
                                                   "adjuncts" : [{"id" : "{{existingAdjunctId}}"}]
                                                 }
                                                 """)).ToList()));

        var testManifest = await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: TestIdentifiers.BatchId(), ingested: true, spaceId: NewlyCreatedSpace);
        await dbContext.SaveChangesAsync();

        var batchId = TestIdentifiers.BatchId();

        var manifestWithSpace = $$"""
                          {
                              "type": "Manifest",
                              "slug": "{{slug}}",
                              "parent": "http://localhost/{{Customer}}/collections/root",
                              "paintedResources": [
                                  {
                                     "canvasPainting":{
                                        "canvasOrder": 1
                                     },
                                      "asset": {
                                          "id": "fromDlcs_{{assetId}}_1",
                                          "mediaType": "image/jpg",
                                          "batch": {{batchId}},
                                          "space": {{NewlyCreatedSpace}},
                                          "adjuncts": []
                                      },
                                      "reingest": true
                                  }
                              ]
                          }
                          """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                manifestWithSpace, dbContext.GetETag(testManifest));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.PaintedResources.Should().HaveCount(1);

        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());

        dbManifest.CanvasPaintings.First(cp => cp.CanvasOrder == 1).Should().NotBeNull("asset added to manifest");

        // asset reingested
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
            A<List<JObject>>.That.Matches(o => o.Single().GetValue("id")!.ToString() == $"fromDlcs_{assetId}_1"),
            A<bool>._, A<CancellationToken>._)).MustHaveHappened();

        // deleted the adjunct returned from GetCustomerImages
        A.CallTo(() => DLCSApiClient.DeleteAdjuncts(Customer,
            A<List<AdjunctAssetIdentifier>>.That.Matches(a => a.Single().Adjunct.Single() == existingAdjunctId),
            A<CancellationToken>._)).MustHaveHappened();
        
        dbManifest.Batches.Should().HaveCount(2); // initial batch from setup + asset batch
        dbManifest.Batches.Last().DeliverableType.Should().Be(DeliverableType.Asset);
    }
    
    [Fact]
    public async Task UpdateManifest_LeavesAdjunctsAlone_WhenNullAdjunctOnKnownAsset()
    {
        // This test checks that an adjunct on an asset will not be removed (i.e.: delete + ingest not called)
        // when a known asset is set to reingest with adjuncts set to null

        // Arrange
        var (slug, id, assetId, existingAdjunctId) = TestIdentifiers.SlugResourceAssetAdjunct();

        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = "first",
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_1")
            }
        };
        
        A.CallTo(() => DLCSApiClient.GetCustomerImages(Customer,
                A<ICollection<string>>._,
                A<CancellationToken>._)).ReturnsLazily(x => Task.FromResult((IList<JObject>)[])).Once().Then
            .ReturnsLazily((int customerId, ICollection<string> assetIds, CancellationToken can) =>
                Task.FromResult((IList<JObject>)assetIds
                    .Where(a => a.Split('/', StringSplitOptions.None).Last().StartsWith("fromDlcs_"))
                    .Select(x => JObject.Parse($$"""
                                                 {
                                                   "id": "{{x.Split('/').Last()}}",
                                                   "space": {{NewlyCreatedSpace}},
                                                   "adjuncts" : [{"id" : "{{existingAdjunctId}}"}]
                                                 }
                                                 """)).ToList()));

        var testManifest = await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: TestIdentifiers.BatchId(), ingested: true, spaceId: NewlyCreatedSpace);
        await dbContext.SaveChangesAsync();

        var batchId = TestIdentifiers.BatchId();

        var manifestWithSpace = $$"""
                          {
                              "type": "Manifest",
                              "slug": "{{slug}}",
                              "parent": "http://localhost/{{Customer}}/collections/root",
                              "paintedResources": [
                                  {
                                     "canvasPainting":{
                                        "canvasOrder": 1
                                     },
                                      "asset": {
                                          "id": "fromDlcs_{{assetId}}_1",
                                          "mediaType": "image/jpg",
                                          "batch": {{batchId}},
                                          "space": {{NewlyCreatedSpace}},
                                          "adjuncts": null
                                      },
                                      "reingest": true
                                  }
                              ]
                          }
                          """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                manifestWithSpace, dbContext.GetETag(testManifest));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.PaintedResources.Should().HaveCount(1);

        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());

        dbManifest.CanvasPaintings.First(cp => cp.CanvasOrder == 1).Should().NotBeNull("asset added to manifest");

        // asset reingested
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
            A<List<JObject>>.That.Matches(o => o.Single().GetValue("id")!.ToString() == $"fromDlcs_{assetId}_1"),
            A<bool>._, A<CancellationToken>._)).MustHaveHappened();

        // did not delete the adjunct returned from GetCustomerImages
        A.CallTo(() => DLCSApiClient.DeleteAdjuncts(Customer,
            A<List<AdjunctAssetIdentifier>>.That.Matches(a => a.Single().Adjunct.Single() == existingAdjunctId),
            A<CancellationToken>._)).MustNotHaveHappened();
        
        dbManifest.Batches.Should().HaveCount(2); // initial batch from setup + asset batch
        dbManifest.Batches.Last().DeliverableType.Should().Be(DeliverableType.Asset);
    }

    [Fact]
    public async Task UpdateManifest_HandlesMixedAdjunctTypes_WhenMultiplePaintedResources()
    {
        // Tests three adjunct scenarios occurring simultaneously across three painted resources:
        // - Asset 1: same adjunct id → no delete, adjunct reingested
        // - Asset 2: different adjunct id → old deleted, new ingested
        // - Asset 3: empty adjuncts → existing adjunct deleted, nothing ingested

        // Arrange
        var (slug, id, assetId, existingAdjunctId) = TestIdentifiers.SlugResourceAssetAdjunct();

        var existingAdjunctId1 = $"{existingAdjunctId}_1";
        var existingAdjunctId2 = $"{existingAdjunctId}_2";
        var existingAdjunctId3 = $"{existingAdjunctId}_3";
        var newAdjunctId2 = $"new_{existingAdjunctId}_2";

        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = $"{id}_cp1",
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"fromDlcs_{assetId}_1")
            },
            new()
            {
                Id = $"{id}_cp2",
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 2,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"fromDlcs_{assetId}_2")
            },
            new()
            {
                Id = $"{id}_cp3",
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 3,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"fromDlcs_{assetId}_3")
            }
        };

        var adjunctByAssetName = new Dictionary<string, string>
        {
            [$"fromDlcs_{assetId}_1"] = existingAdjunctId1,
            [$"fromDlcs_{assetId}_2"] = existingAdjunctId2,
            [$"fromDlcs_{assetId}_3"] = existingAdjunctId3,
        };

        A.CallTo(() => DLCSApiClient.GetCustomerImages(Customer,
                A<ICollection<string>>._,
                A<CancellationToken>._)).ReturnsLazily(x => Task.FromResult((IList<JObject>)[])).Once().Then
            .ReturnsLazily((int customerId, ICollection<string> assetIds, CancellationToken can) =>
                Task.FromResult((IList<JObject>)assetIds
                    .Select(assetIdStr =>
                    {
                        var name = assetIdStr.Split('/').Last();
                        return adjunctByAssetName.TryGetValue(name, out var adjId)
                            ? JObject.Parse($$"""{"id": "{{name}}", "space": {{NewlyCreatedSpace}}, "adjuncts": [{"id": "{{adjId}}"}]}""")
                            : null!;
                    })
                    .Where(x => x != null)
                    .ToList()));

        var testManifest = await dbContext.Manifests.AddTestManifest(id: id, slug: slug,
            canvasPaintings: initialCanvasPaintings, batchId: TestIdentifiers.BatchId(),
            ingested: true, spaceId: NewlyCreatedSpace);
        await dbContext.SaveChangesAsync();

        var batchId = TestIdentifiers.BatchId();
        var adjunctBatchId = TestIdentifiers.BatchId();

        var manifestWithSpace = $$"""
                          {
                              "type": "Manifest",
                              "slug": "{{slug}}",
                              "parent": "http://localhost/{{Customer}}/collections/root",
                              "paintedResources": [
                                  {
                                     "canvasPainting": { "canvasOrder": 1 },
                                      "asset": {
                                          "id": "fromDlcs_{{assetId}}_1",
                                          "mediaType": "image/jpg",
                                          "batch": {{batchId}},
                                          "space": {{NewlyCreatedSpace}},
                                          "adjuncts": [{ "id": "{{existingAdjunctId1}}", "batch": {{adjunctBatchId}} }]
                                      },
                                      "reingest": true
                                  },
                                  {
                                     "canvasPainting": { "canvasOrder": 2 },
                                      "asset": {
                                          "id": "fromDlcs_{{assetId}}_2",
                                          "mediaType": "image/jpg",
                                          "space": {{NewlyCreatedSpace}},
                                          "adjuncts": [{ "id": "{{newAdjunctId2}}" }]
                                      },
                                      "reingest": true
                                  },
                                  {
                                     "canvasPainting": { "canvasOrder": 3 },
                                      "asset": {
                                          "id": "fromDlcs_{{assetId}}_3",
                                          "mediaType": "image/jpg",
                                          "space": {{NewlyCreatedSpace}},
                                          "adjuncts": []
                                      },
                                      "reingest": true
                                  }
                              ]
                          }
                          """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                manifestWithSpace, dbContext.GetETag(testManifest));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.PaintedResources.Should().HaveCount(3);

        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());

        dbManifest.CanvasPaintings.Should().HaveCount(3, "all assets added to manifest");

        // all 3 assets reingested in a single asset batch
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
            A<List<JObject>>.That.Matches(o =>
                o.Count == 3 &&
                o.Any(j => j.GetValue("id")!.ToString() == $"fromDlcs_{assetId}_1") &&
                o.Any(j => j.GetValue("id")!.ToString() == $"fromDlcs_{assetId}_2") &&
                o.Any(j => j.GetValue("id")!.ToString() == $"fromDlcs_{assetId}_3")),
            false, A<CancellationToken>._)).MustHaveHappened();

        // adjunct_2 (replaced) and adjunct_3 (emptied) deleted together; adjunct_1 (unchanged) not deleted
        A.CallTo(() => DLCSApiClient.DeleteAdjuncts(Customer,
            A<List<AdjunctAssetIdentifier>>.That.Matches(a =>
                a.Count == 2 &&
                a.Any(x => x.Adjunct.Contains(existingAdjunctId2)) &&
                a.Any(x => x.Adjunct.Contains(existingAdjunctId3))),
            A<CancellationToken>._)).MustHaveHappened();

        A.CallTo(() => DLCSApiClient.DeleteAdjuncts(Customer,
            A<List<AdjunctAssetIdentifier>>.That.Matches(a => a.Any(x => x.Adjunct.Contains(existingAdjunctId1))),
            A<CancellationToken>._)).MustNotHaveHappened();

        // adjunct_1 (same, kept) and newAdjunctId2 ingested together; adjunct_3 (empty) not ingested
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
            A<List<JObject>>.That.Matches(o =>
                o.Count == 2 &&
                o.Any(j => j.GetValue("id")!.ToString() == existingAdjunctId1) &&
                o.Any(j => j.GetValue("id")!.ToString() == newAdjunctId2)),
            true, A<CancellationToken>._)).MustHaveHappened();

        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
            A<List<JObject>>.That.Matches(o => o.Any(j => j.GetValue("id")!.ToString() == existingAdjunctId3)),
            true, A<CancellationToken>._)).MustNotHaveHappened();

        dbManifest.Batches.Should().HaveCount(3); // initial batch from setup + asset batch + adjunct batch
        dbManifest.Batches[1].DeliverableType.Should().Be(DeliverableType.Asset);
        dbManifest.Batches.Last().DeliverableType.Should().Be(DeliverableType.Adjunct);
    }

    [Fact]
    public async Task UpdateManifest_ReplacesAdjuncts_WhenNewAdjunctOnKnownAssetImmediateReturn()
    {
        // This test checks that an adjunct on an asset will be replaced (i.e.: 1 removed, 1 added)
        // when a known asset is not set to reingest, causing an immediate return

        // Arrange
        var (slug, id, assetId, existingAdjunctId) = TestIdentifiers.SlugResourceAssetAdjunct();

        var initialCanvasPaintings = new List<CanvasPainting>
        {
            new()
            {
                Id = "first",
                StaticWidth = 1200,
                StaticHeight = 1800,
                CanvasOrder = 1,
                ChoiceOrder = 1,
                AssetId = new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_1")
            }
        };

        A.CallTo(() => DLCSApiClient.GetCustomerImages(Customer,
                A<ICollection<string>>.That.Matches(o =>
                    o.First().Split('/', StringSplitOptions.None).Last().StartsWith("fromDlcs_")),
                A<CancellationToken>._))
            .ReturnsLazily((int customerId, ICollection<string> assetIds, CancellationToken can) =>
                Task.FromResult((IList<JObject>)assetIds
                    .Where(a => a.Split('/', StringSplitOptions.None).Last().StartsWith("fromDlcs_"))
                    .Select(x => JObject.Parse($$"""
                                                 {
                                                   "id": "{{x.Split('/').Last()}}",
                                                   "space": {{NewlyCreatedSpace}},
                                                   "adjuncts" : [{"id" : "{{existingAdjunctId}}"}]
                                                 }
                                                 """)).ToList()));

        A.CallTo(() =>
                DLCSOrchestratorClient.RetrieveAssetsForManifest(A<int>.Ignored, A<string>.Ignored,
                    A<CancellationToken>.Ignored))
            .ReturnsLazily(() => ManifestTestCreator.New()
                .WithCanvas(new AssetId(Customer, NewlyCreatedSpace, $"{assetId}_1"), c => c.WithImage())
                .Build());

        var testManifest = await dbContext.Manifests.AddTestManifest(id: id, slug: slug, canvasPaintings: initialCanvasPaintings,
            batchId: TestIdentifiers.BatchId(), ingested: true, spaceId: NewlyCreatedSpace);
        await dbContext.SaveChangesAsync();

        var batchId = TestIdentifiers.BatchId();
        var newAdjunctId = "different";

        var manifestWithSpace = $$"""
                          {
                              "type": "Manifest",
                              "slug": "{{slug}}",
                              "parent": "http://localhost/{{Customer}}/collections/root",
                              "paintedResources": [
                                  {
                                     "canvasPainting":{
                                        "canvasOrder": 1
                                     },
                                      "asset": {
                                          "id": "fromDlcs_{{assetId}}_1",
                                          "mediaType": "image/jpg",
                                          "space": {{NewlyCreatedSpace}},
                                          "adjuncts": [
                                            {
                                                "id": "{{newAdjunctId}}",
                                                "batch": {{batchId}}
                                            }
                                          ]
                                      },
                                      "reingest": false
                                  }
                              ]
                          }
                          """;

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Put, $"{Customer}/manifests/{id}",
                manifestWithSpace, dbContext.GetETag(testManifest));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseManifest = await response.ReadAsPresentationResponseAsync<PresentationManifest>();

        responseManifest!.PaintedResources.Should().HaveCount(1);

        var dbManifest = dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .Include(m => m.Batches)
            .First(x => x.Id == responseManifest.Id!.Split('/', StringSplitOptions.TrimEntries).Last());

        dbManifest.CanvasPaintings.First(cp => cp.CanvasOrder == 1).Should().NotBeNull("asset added to manifest");

        // deleted the adjunct returned from GetCustomerImages
        A.CallTo(() => DLCSApiClient.DeleteAdjuncts(Customer,
            A<List<AdjunctAssetIdentifier>>.That.Matches(a => a.Count == 1 && a.First().Adjunct.Single() == existingAdjunctId),
            A<CancellationToken>._)).MustHaveHappened();

        // new adjunct ingested
        A.CallTo(() => DLCSApiClient.IngestDeliverables(Customer,
            A<List<JObject>>.That.Matches(o => o.Count == 1 && o.First().GetValue("id")!.ToString() == newAdjunctId),
            A<bool>._, A<CancellationToken>._)).MustHaveHappened();

        dbManifest.Batches.Should().HaveCount(2); // initial batch from setup + adjunct batch
        dbManifest.Batches.Last().DeliverableType.Should().Be(DeliverableType.Adjunct);
    }
}
