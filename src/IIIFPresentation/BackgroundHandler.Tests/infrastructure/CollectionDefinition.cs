using Test.Helpers.Integration;

namespace BackgroundHandler.Tests.infrastructure;

public class CollectionDefinitions
{
    [CollectionDefinition(CollectionName)]
    public class DatabaseCollection : ICollectionFixture<PresentationContextFixture>
    {
        public const string CollectionName = "Database Collection";
    }
}