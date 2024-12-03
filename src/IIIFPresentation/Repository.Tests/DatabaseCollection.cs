using Test.Helpers.Integration;

namespace Repository.Tests;

[CollectionDefinition(CollectionName)]
public class DatabaseCollection : ICollectionFixture<PresentationContextFixture>
{
    public const string CollectionName = "Database Collection";
}