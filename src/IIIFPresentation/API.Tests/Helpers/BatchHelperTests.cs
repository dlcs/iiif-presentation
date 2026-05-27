using API.Helpers;
using API.Tests.Integration.Infrastructure;
using Models.Database.General;
using Repository;
using Test.Helpers;
using Test.Helpers.Integration;
using Batch = DLCS.Models.Batch;

namespace API.Tests.Helpers;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class BatchHelperTests
{
    private readonly PresentationContext dbContext;

    public BatchHelperTests(PresentationContextFixture dbFixture)
    {
        dbContext = dbFixture.GetNewPresentationContext(dbFixture.CustomerIdProvider);
        dbFixture.CleanUp();
    }

    [Fact]
    public async Task AddBatchesToDatabase_AddsCorrectly_WithSingleBatch()
    {
        // Arrange
        var customerId = PresentationContextFixture.CustomerId;
        var manifestId = "test-manifest";
        var batchId = TestIdentifiers.BatchId();
        var batch = new Batch
        {
            ResourceId = $"http://dlcs.example.com/batches/{batchId}",
            Submitted = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Unspecified),
            Finished = new DateTime(2024, 1, 1, 13, 0, 0, DateTimeKind.Unspecified),
            Count = 10,
            Completed = 10,
            Errors = 0
        };
        var batches = new List<Batch> { batch };

        // Act
        await batches.AddBatchesToDatabase(customerId, manifestId, dbContext, DeliverableType.Asset);

        // Assert
        var trackedBatch = dbContext.Batches.Local.SingleOrDefault(b => b.Id == batchId);
        trackedBatch.Should().NotBeNull();
    }

    [Fact]
    public async Task AddBatchesToDatabase_MapsProperties_Correctly()
    {
        // Arrange
        var customerId = PresentationContextFixture.CustomerId;
        var manifestId = "test-manifest";
        var batchId = TestIdentifiers.BatchId();
        var submitted = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);
        var finished = new DateTime(2024, 1, 1, 13, 0, 0, DateTimeKind.Unspecified);
        var batch = new Batch
        {
            ResourceId = $"http://dlcs.example.com/batches/{batchId}",
            Submitted = submitted,
            Finished = finished
        };
        var batches = new List<Batch> { batch };

        // Act
        await batches.AddBatchesToDatabase(customerId, manifestId, dbContext, DeliverableType.Asset);

        // Assert
        var trackedBatch = dbContext.Batches.Local.Single(b => b.Id == batchId);
        trackedBatch.CustomerId.Should().Be(customerId);
        trackedBatch.ManifestId.Should().Be(manifestId);
        trackedBatch.Submitted.Should().Be(submitted.ToUniversalTime());
        trackedBatch.Submitted.Kind.Should().Be(DateTimeKind.Utc);
        trackedBatch.Finished.Should().NotBeNull();
        trackedBatch.Finished!.Value.Kind.Should().Be(DateTimeKind.Utc);
        trackedBatch.Finished.Should().Be(finished.ToUniversalTime());
        trackedBatch.Status.Should().Be(BatchStatus.Completed);
        trackedBatch.DeliverableType.Should().Be(DeliverableType.Asset);
        trackedBatch.Processed.Should().NotBeNull();
    }

    [Fact]
    public async Task AddBatchesToDatabase_SetsStatusToIngesting_WhenFinishedIsNull()
    {
        // Arrange
        var customerId = PresentationContextFixture.CustomerId;
        var manifestId = "test-manifest";
        var batchId = TestIdentifiers.BatchId();
        var batch = new Batch
        {
            ResourceId = $"http://dlcs.example.com/batches/{batchId}",
            Submitted = DateTime.UtcNow,
            Finished = null
        };
        var batches = new List<Batch> { batch };

        // Act
        await batches.AddBatchesToDatabase(customerId, manifestId, dbContext, DeliverableType.Asset);

        // Assert
        var trackedBatch = dbContext.Batches.Local.Single(b => b.Id == batchId);
        trackedBatch.Status.Should().Be(BatchStatus.Ingesting);
        trackedBatch.Processed.Should().BeNull();
        trackedBatch.Finished.Should().BeNull();
    }

    [Fact]
    public async Task AddBatchesToDatabase_SetsProcessedToNull_WhenFinishedIsNull()
    {
        // Arrange
        var customerId = PresentationContextFixture.CustomerId;
        var manifestId = "test-manifest";
        var batchId = TestIdentifiers.BatchId();
        var batch = new Batch
        {
            ResourceId = $"http://dlcs.example.com/batches/{batchId}",
            Submitted = DateTime.UtcNow,
            Finished = null
        };
        var batches = new List<Batch> { batch };

        // Act
        await batches.AddBatchesToDatabase(customerId, manifestId, dbContext, DeliverableType.Asset);

        // Assert
        var trackedBatch = dbContext.Batches.Local.Single(b => b.Id == batchId);
        trackedBatch.Processed.Should().BeNull();
    }

    [Theory]
    [InlineData(DeliverableType.Asset)]
    [InlineData(DeliverableType.Adjunct)]
    public async Task AddBatchesToDatabase_SetsDeliverableType_ToSpecifiedValue(DeliverableType deliverableType)
    {
        // Arrange
        var customerId = PresentationContextFixture.CustomerId;
        var manifestId = "test-manifest";
        var batchId = TestIdentifiers.BatchId();
        var batch = new Batch
        {
            ResourceId = $"http://dlcs.example.com/batches/{batchId}",
            Submitted = DateTime.UtcNow,
            Finished = DateTime.UtcNow
        };
        var batches = new List<Batch> { batch };

        // Act
        await batches.AddBatchesToDatabase(customerId, manifestId, dbContext, deliverableType);

        // Assert
        var trackedBatch = dbContext.Batches.Local.Single(b => b.Id == batchId);
        trackedBatch.DeliverableType.Should().Be(deliverableType);
    }

    [Fact]
    public async Task AddBatchesToDatabase_AddsMultipleBatches_Correctly()
    {
        // Arrange
        var customerId = PresentationContextFixture.CustomerId;
        var manifestId = "test-manifest";
        var idOne = TestIdentifiers.BatchId();
        var idTwo = TestIdentifiers.BatchId();
        var idThree = TestIdentifiers.BatchId();
        var batches = new List<Batch>
        {
            new()
            {
                ResourceId = $"http://dlcs.example.com/batches/{idOne}",
                Submitted = DateTime.UtcNow,
                Finished = null
            },
            new()
            {
                ResourceId = $"http://dlcs.example.com/batches/{idTwo}",
                Submitted = DateTime.UtcNow,
                Finished = DateTime.UtcNow
            },
            new()
            {
                ResourceId = $"http://dlcs.example.com/batches/{idThree}",
                Submitted = DateTime.UtcNow,
                Finished = null
            }
        };

        // Act
        await batches.AddBatchesToDatabase(customerId, manifestId, dbContext, DeliverableType.Asset);

        // Assert
        var trackedBatches = dbContext.Batches.Local.Where(b => b.Id == idOne || b.Id == idTwo || b.Id == idThree).ToList();
        trackedBatches.Should().HaveCount(3);
        trackedBatches.Select(b => b.Id).Should().ContainInOrder(idOne, idTwo, idThree);
    }

    [Fact]
    public async Task AddBatchesToDatabase_HandlesEmptyBatchList()
    {
        // Arrange
        var customerId = PresentationContextFixture.CustomerId;
        var manifestId = "test-manifest";
        var batches = new List<Batch>();

        // Act - should not throw
        await batches.AddBatchesToDatabase(customerId, manifestId, dbContext, DeliverableType.Asset);

        // Assert
        var allBatches = dbContext.Batches.Local.ToList();
        allBatches.Should().BeEmpty();
    }

    [Fact]
    public async Task AddBatchesToDatabase_ConvertsSubmittedTimeToUtc()
    {
        // Arrange
        var customerId = PresentationContextFixture.CustomerId;
        var manifestId = "test-manifest";
        var batchId = TestIdentifiers.BatchId();
        var unspecifiedTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);
        var batch = new Batch
        {
            ResourceId = $"http://dlcs.example.com/batches/{batchId}",
            Submitted = unspecifiedTime,
            Finished = null
        };
        var batches = new List<Batch> { batch };

        // Act
        await batches.AddBatchesToDatabase(customerId, manifestId, dbContext, DeliverableType.Asset);

        // Assert
        var trackedBatch = dbContext.Batches.Local.Single(b => b.Id == batchId);
        trackedBatch.Submitted.Kind.Should().Be(DateTimeKind.Utc);
        trackedBatch.Submitted.Should().Be(unspecifiedTime.ToUniversalTime());
    }

    [Fact]
    public async Task AddBatchesToDatabase_SetsProcessedToCurrentTime_WhenFinishedHasValue()
    {
        // Arrange
        var customerId = PresentationContextFixture.CustomerId;
        var manifestId = "test-manifest";
        var batchId = TestIdentifiers.BatchId();
        var batch = new Batch
        {
            ResourceId = $"http://dlcs.example.com/batches/{batchId}",
            Submitted = DateTime.UtcNow,
            Finished = DateTime.UtcNow
        };
        var batches = new List<Batch> { batch };

        // Act
        await batches.AddBatchesToDatabase(customerId, manifestId, dbContext, DeliverableType.Asset);

        // Assert
        var trackedBatch = dbContext.Batches.Local.Single(b => b.Id == batchId);
        trackedBatch.Processed.Should().NotBeNull();
        trackedBatch.Processed!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task AddBatchesToDatabase_ConvertsFinishedTimeToUtc()
    {
        // Arrange
        var customerId = PresentationContextFixture.CustomerId;
        var manifestId = "test-manifest";
        var batchId = TestIdentifiers.BatchId();
        var unspecifiedTime = new DateTime(2024, 1, 1, 13, 0, 0, DateTimeKind.Unspecified);
        var batch = new Batch
        {
            ResourceId = $"http://dlcs.example.com/batches/{batchId}",
            Submitted = DateTime.UtcNow,
            Finished = unspecifiedTime
        };
        var batches = new List<Batch> { batch };

        // Act
        await batches.AddBatchesToDatabase(customerId, manifestId, dbContext, DeliverableType.Asset);

        // Assert
        var trackedBatch = dbContext.Batches.Local.Single(b => b.Id == batchId);
        trackedBatch.Finished.Should().NotBeNull();
        trackedBatch.Finished!.Value.Kind.Should().Be(DateTimeKind.Utc);
        trackedBatch.Finished.Should().Be(unspecifiedTime.ToUniversalTime());
    }
}