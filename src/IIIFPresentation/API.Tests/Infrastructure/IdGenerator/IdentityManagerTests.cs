using System.Data;
using API.Infrastructure.IdGenerator;
using API.Tests.Integration.Infrastructure;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Models.Database.Collections;
using Test.Helpers.Integration;

namespace API.Tests.Infrastructure.IdGenerator;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class IdentityManagerTests
{
    private readonly IIdGenerator idGenerator;
    private readonly IdentityManager sut;

    public IdentityManagerTests(PresentationContextFixture dbFixture)
    {
        idGenerator = A.Fake<IIdGenerator>();
        sut = new IdentityManager(idGenerator, dbFixture.DbContext, new NullLogger<IdentityManager>());
    }

    [Fact]
    public async Task GenerateUniqueId_CreatesNewId()
    {
        // Arrange
        A.CallTo(() => idGenerator.Generate(A<List<long>>._)).Returns("generated");

        // Act
        var id = await sut.GenerateUniqueId<Collection>(PresentationContextFixture.CustomerId);

        // Assert
        id.Should().Be("generated");
    }
    
    [Fact]
    public async Task GenerateUniqueId_CreatesNewId_AfterReattempt_IfInitialExists()
    {
        // Arrange
        A.CallTo(() => idGenerator.Generate(A<List<long>>._))
            .ReturnsNextFromSequence("root", "root", "generated");

        // Act
        var id = await sut.GenerateUniqueId<Collection>(PresentationContextFixture.CustomerId);

        // Assert
        id.Should().Be("generated");
    }

    [Fact]
    public async Task GenerateUniqueId_ThrowsException_WhenGeneratingExistingId_MaxAttempts()
    {
        // Arrange
        A.CallTo(() => idGenerator.Generate(A<List<long>>._)).Returns("root");

        // Act
        Func<Task> action = () => sut.GenerateUniqueId<Collection>(PresentationContextFixture.CustomerId);

        // Assert
        await action.Should().ThrowAsync<ConstraintException>();
        A.CallTo(() => idGenerator.Generate(A<List<long>>._)).MustHaveHappened(3, Times.Exactly);
    }
    
    [Fact]
    public async Task GenerateUniqueIds_CreatesNewIds()
    {
        // Arrange
        A.CallTo(() => idGenerator.Generate(A<List<long>>._))
            .ReturnsNextFromSequence("one", "two", "three");
        var expected = new List<string> { "one", "two", "three" };

        // Act
        var id = await sut.GenerateUniqueIds<Manifest>(PresentationContextFixture.CustomerId, 3);

        // Assert
        id.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task GenerateUniqueIds_CreatesNewIds_AfterReattempt_IfInitialExists()
    {
        // Arrange
        A.CallTo(() => idGenerator.Generate(A<List<long>>._))
            .ReturnsNextFromSequence("root", "one", "two", "root", "three");
        var expected = new List<string> { "one", "two", "three" };

        // Act
        var id = await sut.GenerateUniqueIds<Collection>(PresentationContextFixture.CustomerId, 3);

        // Assert
        id.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GenerateUniqueIds_ThrowsException_WhenGeneratingExistingIds_MaxAttempts()
    {
        // Arrange
        A.CallTo(() => idGenerator.Generate(A<List<long>>._)).Returns("root");

        // Act
        Func<Task> action = () => sut.GenerateUniqueId<Collection>(PresentationContextFixture.CustomerId);

        // Assert
        await action.Should().ThrowAsync<ConstraintException>();
        A.CallTo(() => idGenerator.Generate(A<List<long>>._)).MustHaveHappened(3, Times.Exactly);
    }
}