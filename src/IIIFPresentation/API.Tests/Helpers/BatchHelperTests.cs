using API.Helpers;
using API.Tests.Integration.Infrastructure;
using Models.Database.General;
using Repository;
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
        var batch = new Batch
        {
            ResourceId = "http://dlcs.example.com/batches/456",
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
        var trackedBatch = dbContext.Batches.Local.SingleOrDefault(b => b.Id == 456);
        trackedBatch.Should().NotBeNull();
    }

    [Fact]
    public async Task AddBatchesToDatabase_MapsProperties_Correctly()
    {
        // Arrange
        var customerId = PresentationContextFixture.CustomerId;
        var manifestId = "test-manifest";
        var submitted = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);
        var finished = new DateTime(2024, 1, 1, 13, 0, 0, DateTimeKind.Unspecified);
        var batch = new Batch
        {
            ResourceId = "http://dlcs.example.com/batches/789",
            Submitted = submitted,
            Finished = finished
        };
        var batches = new List<Batch> { batch };

        // Act
        await batches.AddBatchesToDatabase(customerId, manifestId, dbContext, DeliverableType.Asset);

        // Assert
        var trackedBatch = dbContext.Batches.Local.Single(b => b.Id == 789);
        trackedBatch.CustomerId.Should().Be(customerId);
        trackedBatch.ManifestId.Should().Be(manifestId);
        trackedBatch.Submitted.Should().Be(submitted.ToUniversalTime());
        trackedBatch.Finished.Should().Be(finished);
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
        var batch = new Batch
        {
            ResourceId = "http://dlcs.example.com/batches/101",
            Submitted = DateTime.UtcNow,
            Finished = null
        };
        var batches = new List<Batch> { batch };

        // Act
        await batches.AddBatchesToDatabase(customerId, manifestId, dbContext, DeliverableType.Asset);

        // Assert
        var trackedBatch = dbContext.Batches.Local.Single(b => b.Id == 101);
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
        var batch = new Batch
        {
            ResourceId = "http://dlcs.example.com/batches/202",
            Submitted = DateTime.UtcNow,
            Finished = null
        };
        var batches = new List<Batch> { batch };

        // Act
        await batches.AddBatchesToDatabase(customerId, manifestId, dbContext, DeliverableType.Asset);

        // Assert
        var trackedBatch = dbContext.Batches.Local.Single(b => b.Id == 202);
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
        var batch = new Batch
        {
            ResourceId = "http://dlcs.example.com/batches/303",
            Submitted = DateTime.UtcNow,
            Finished = DateTime.UtcNow
        };
        var batches = new List<Batch> { batch };

        // Act
        await batches.AddBatchesToDatabase(customerId, manifestId, dbContext, deliverableType);

        // Assert
        var trackedBatch = dbContext.Batches.Local.Single(b => b.Id == 303);
        trackedBatch.DeliverableType.Should().Be(deliverableType);
    }

    [Fact]
    public async Task AddBatchesToDatabase_AddsMultipleBatches_Correctly()
    {
        // Arrange
        var customerId = PresentationContextFixture.CustomerId;
        var manifestId = "test-manifest";
        var batches = new List<Batch>
        {
            new()
            {
                ResourceId = "http://dlcs.example.com/batches/111",
                Submitted = DateTime.UtcNow,
                Finished = null
            },
            new()
            {
                ResourceId = "http://dlcs.example.com/batches/222",
                Submitted = DateTime.UtcNow,
                Finished = DateTime.UtcNow
            },
            new()
            {
                ResourceId = "http://dlcs.example.com/batches/333",
                Submitted = DateTime.UtcNow,
                Finished = null
            }
        };

        // Act
        await batches.AddBatchesToDatabase(customerId, manifestId, dbContext, DeliverableType.Asset);

        // Assert
        var trackedBatches = dbContext.Batches.Local.Where(b => b.Id is 111 or 222 or 333).ToList();
        trackedBatches.Should().HaveCount(3);
        trackedBatches.Select(b => b.Id).Should().ContainInOrder(111, 222, 333);
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
        var unspecifiedTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);
        var batch = new Batch
        {
            ResourceId = "http://dlcs.example.com/batches/606",
            Submitted = unspecifiedTime,
            Finished = null
        };
        var batches = new List<Batch> { batch };

        // Act
        await batches.AddBatchesToDatabase(customerId, manifestId, dbContext, DeliverableType.Asset);

        // Assert
        var trackedBatch = dbContext.Batches.Local.Single(b => b.Id == 606);
        trackedBatch.Submitted.Kind.Should().Be(DateTimeKind.Utc);
        trackedBatch.Submitted.Should().Be(unspecifiedTime.ToUniversalTime());
    }

    [Fact]
    public async Task AddBatchesToDatabase_SetsProcessedToCurrentTime_WhenFinishedHasValue()
    {
        // Arrange
        var customerId = PresentationContextFixture.CustomerId;
        var manifestId = "test-manifest";
        var batch = new Batch
        {
            ResourceId = "http://dlcs.example.com/batches/707",
            Submitted = DateTime.UtcNow,
            Finished = DateTime.UtcNow
        };
        var batches = new List<Batch> { batch };

        // Act
        await batches.AddBatchesToDatabase(customerId, manifestId, dbContext, DeliverableType.Asset);

        // Assert
        var trackedBatch = dbContext.Batches.Local.Single(b => b.Id == 707);
        trackedBatch.Processed.Should().NotBeNull();
        trackedBatch.Processed!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(3));
    }
}
