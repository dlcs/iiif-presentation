using AWS.SQS;
using BackgroundHandler.CustomerCreation;
using BackgroundHandler.Tests.infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Models.Database.General;
using Repository;
using Test.Helpers.Integration;

namespace BackgroundHandler.Tests.CustomerCreation;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class CustomerCreationMessageHandlerTests
{
    private readonly PresentationContext dbContext;
    private readonly CustomerCreatedMessageHandler sut;

    public CustomerCreationMessageHandlerTests(PresentationContextFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        sut = new CustomerCreatedMessageHandler(dbFixture.DbContext, new NullLogger<CustomerCreatedMessageHandler>());
    }
    
    [Fact]
    public async Task HandleMessage_False_IfMessageInvalid()
    {
        // Arrange
        var message = GetMessage("not-json");
        
        // Act
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeFalse();
    }
    
    [Fact]
    public async Task HandleMessage_True_IfRootExistsForCustomer()
    {
        // Arrange
        dbContext.Collections.Add(GetCollection(-10));
        await dbContext.SaveChangesAsync();
        var message = GetMessage("{\"name\":\"test\",\"id\":-10}");
        
        // Act
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();
    }
    
    [Fact]
    public async Task HandleMessage_True_AndCreatesRoot_IfDoesnotExists()
    {
        // Arrange
        var message = GetMessage("{\"name\":\"test\",\"id\":-100}");
        
        // Act
        (await sut.HandleMessage(message, CancellationToken.None)).Should().BeTrue();

        var root = await dbContext.Collections.FindAsync("root", -100);
        root.Should().NotBeNull();
        var hierarchy = root.Hierarchy.Single();
        hierarchy.Parent.Should().BeNull();
        hierarchy.Slug.Should().BeEmpty();
        hierarchy.Canonical.Should().BeTrue();
        hierarchy.Type.Should().Be(ResourceType.StorageCollection);
    }

    private static QueueMessage GetMessage(string body) => new(body, new Dictionary<string, string>(), "foo");

    private static Models.Database.Collections.Collection GetCollection(int customerId)
        => new()
        {
            CustomerId = customerId,
            Id = "root",
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = string.Empty,
                    Canonical = true,
                    Type = ResourceType.StorageCollection,
                }
            ]
        };
}