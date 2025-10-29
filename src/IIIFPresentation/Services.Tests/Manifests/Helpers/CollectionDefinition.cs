using Test.Helpers.Integration;

namespace Services.Tests.Manifests.Helpers;

public class CollectionDefinitions
{
    [CollectionDefinition(CollectionName)]
    public class DatabaseCollection : ICollectionFixture<PresentationContextFixture>
    {
        public const string CollectionName = "Database Collection";
    }
}
