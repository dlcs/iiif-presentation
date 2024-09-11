﻿using Test.Helpers.Integration;

namespace API.Tests.Integration.Infrastructure;

public class CollectionDefinitions
{
    [CollectionDefinition(CollectionName)]
    public class DatabaseCollection : ICollectionFixture<PresentationContextFixture>
    {
        public const string CollectionName = "Database Collection";
    }
}