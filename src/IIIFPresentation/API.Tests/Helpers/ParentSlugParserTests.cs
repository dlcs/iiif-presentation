using API.Helpers;
using API.Tests.Integration.Infrastructure;
using Core.Web;
using DLCS;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Models.API.Manifest;
using Repository;
using Repository.Paths;
using Test.Helpers.Integration;

namespace API.Tests.Helpers;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class ParentSlugParserTests
{
    private readonly PresentationContext presentationContext;
    private readonly ParentSlugParser parentSlugParser;
    private const int Customer = 1;
    
    public ParentSlugParserTests(PresentationContextFixture dbFixture)
    {
        presentationContext = dbFixture.DbContext;
        
        var httpContextAccessor = A.Fake<IHttpContextAccessor>();
        httpContextAccessor.HttpContext = A.Fake<HttpContext>();
        var request = A.Fake<HttpRequest>();
        
        A.CallTo(() => request.Host).Returns(new HostString("localhost"));
        A.CallTo(() => request.Scheme).Returns("http");
        
        A.CallTo(() => httpContextAccessor.HttpContext.Request).Returns(request);
        
        var options = Options.Create(new DlcsSettings
        {
            ApiUri = new Uri("http://localhost")
        });

        var presentationPathGenerator =
            new ConfigDrivenPresentationPathGenerator(Options.Create(new TypedPathTemplateOptions
                {
                    Defaults = new Dictionary<string, string>()
                    {
                        ["ManifestPrivate"] = "{customerId}/manifests/{resourceId}",
                        ["CollectionPrivate"] = "{customerId}/collections/{resourceId}",
                        ["ResourcePublic"] = "{customerId}/{hierarchyPath}"
                    }
                }),
                httpContextAccessor);

        var pathGenerator =
            new HttpRequestBasedPathGenerator(options, presentationPathGenerator);
        parentSlugParser = new ParentSlugParser(presentationContext, pathGenerator, httpContextAccessor, new NullLogger<ParentSlugParser>());
    }

    [Fact]
    public async Task ParentSlugParser_RootCollection_NoParent_ReturnsEmptyResult()
    {
        // Arrange
        var presentationCollection = new PresentationCollection();
        const string collectionId = "root"; 
        
        // Act
        var parentSlugParserResult =
            await parentSlugParser.Parse(presentationCollection, Customer, collectionId);

        // Assert
        parentSlugParserResult.IsError.Should().BeFalse();
        parentSlugParserResult.Errors.Should().BeNull();
        parentSlugParserResult.ParsedParentSlug.Slug.Should().BeEmpty("Root collection has empty slug");
        parentSlugParserResult.ParsedParentSlug.Parent.Should().BeNull("Root collection has no parent");
    }
    
    [Fact]
    public async Task ParentSlugParser_RootCollection_Public_ReturnsEmptyResult()
    {
        // Arrange
        var presentationCollection = new PresentationCollection
        {
            PublicId = $"http://localhost/{Customer}",
        };
        const string collectionId = "root"; 
        
        // Act
        var parentSlugParserResult =
            await parentSlugParser.Parse(presentationCollection, Customer, collectionId);

        // Assert
        parentSlugParserResult.IsError.Should().BeFalse();
        parentSlugParserResult.Errors.Should().BeNull();
        parentSlugParserResult.ParsedParentSlug.Slug.Should().BeEmpty("Root collection has empty slug");
        parentSlugParserResult.ParsedParentSlug.Parent.Should().BeNull("Root collection has no parent");
    }
    
    [Theory]
    [InlineData("http://localhost/1/collections/FirstChildCollection")]
    [InlineData("http://localhost/1/iiif-manifest")]
    public async Task ParentSlugParser_RootCollection_NonRootHierarchicalPublic_ReturnsError(string publicId)
    {
        // Arrange
        var presentationCollection = new PresentationCollection
        {
            PublicId = publicId
        };
        const string collectionId = "root"; 
        
        // Act
        var parentSlugParserResult =
            await parentSlugParser.Parse(presentationCollection, Customer, collectionId);

        // Assert
        parentSlugParserResult.IsError.Should().BeTrue();
        parentSlugParserResult.Errors.Error.Should().Be("publicId incorrect");
    }
    
    [Fact]
    public async Task ParentSlugParser_ParsesFlatRootParentSlug_IfRootIdButNotCollection()
    {
        // Arrange
        var slug = nameof(ParentSlugParser_ParsesFlatRootParentSlug);
        const string rootId = "root"; 
        
        // Act
        var parentSlugParserResult = await parentSlugParser.Parse(new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/root",
            Slug = slug
        }, Customer, rootId);

        // Assert
        parentSlugParserResult.IsError.Should().BeFalse();
        parentSlugParserResult.Errors.Should().BeNull();
        parentSlugParserResult.ParsedParentSlug.Slug.Should().Be(slug);
        parentSlugParserResult.ParsedParentSlug.Parent.Id.Should().Be("root");
    }

    [Fact]
    public async Task ParentSlugParser_ParsesFlatRootParentSlug()
    {
        // Arrange
        var slug = nameof(ParentSlugParser_ParsesFlatRootParentSlug);
        
        // Act
        var parentSlugParserResult = await parentSlugParser.Parse(new PresentationCollection
        {
            Parent = $"http://localhost/{Customer}/collections/root",
            Slug = slug
        }, Customer, null);

        // Assert
        parentSlugParserResult.IsError.Should().BeFalse();
        parentSlugParserResult.Errors.Should().BeNull();
        parentSlugParserResult.ParsedParentSlug.Slug.Should().Be(slug);
        parentSlugParserResult.ParsedParentSlug.Parent.Id.Should().Be("root");
    }
    
    [Fact]
    public async Task ParentSlugParser_ParsesFlatChildParentSlug()
    {
        // Arrange
        var slug = nameof(ParentSlugParser_ParsesFlatRootParentSlug);
        
        // Act
        var parentSlugParserResult = await parentSlugParser.Parse(new PresentationCollection
        {
            Parent = $"http://localhost/{Customer}/collections/FirstChildCollection",
            Slug = slug
        }, Customer, null);

        // Assert
        parentSlugParserResult.IsError.Should().BeFalse();
        parentSlugParserResult.Errors.Should().BeNull();
        parentSlugParserResult.ParsedParentSlug.Slug.Should().Be(slug);
        parentSlugParserResult.ParsedParentSlug.Parent.Id.Should().Be("FirstChildCollection");
    }
    
    [Fact]
    public async Task ParentSlugParser_ParsesHierarchicalRootParentSlug()
    {
        // Arrange
        var slug = nameof(ParentSlugParser_ParsesHierarchicalRootParentSlug);
        
        // Act
        var parentSlugParserResult = await parentSlugParser.Parse(new PresentationCollection
        {
            Parent = $"http://localhost/{Customer}",
            Slug = slug
        }, Customer, null);

        // Assert
        parentSlugParserResult.IsError.Should().BeFalse();
        parentSlugParserResult.Errors.Should().BeNull();
        parentSlugParserResult.ParsedParentSlug.Slug.Should().Be(slug);
        parentSlugParserResult.ParsedParentSlug.Parent.Id.Should().Be("root");
    }
    
    [Fact]
    public async Task ParentSlugParser_ParsesHierarchicalChildParentSlug()
    {
        // Arrange
        var slug = nameof(ParentSlugParser_ParsesHierarchicalChildParentSlug);
        
        // Act
        var parentSlugParserResult = await parentSlugParser.Parse(new PresentationCollection
        {
            Parent = $"http://localhost/{Customer}/first-child",
            Slug = slug
        }, Customer, null);

        // Assert
        parentSlugParserResult.IsError.Should().BeFalse();
        parentSlugParserResult.Errors.Should().BeNull();
        parentSlugParserResult.ParsedParentSlug.Slug.Should().Be(slug);
        parentSlugParserResult.ParsedParentSlug.Parent.Id.Should().Be("FirstChildCollection");
    }
    
    [Fact]
    public async Task ParentSlugParser_ParsesPublicId()
    {
        // Arrange
        var slug = nameof(ParentSlugParser_ParsesPublicId);
        
        // Act
        var parentSlugParserResult = await parentSlugParser.Parse(new PresentationCollection
        {
            PublicId = $"http://localhost/{Customer}/{slug}",
        }, Customer, null);

        // Assert
        parentSlugParserResult.IsError.Should().BeFalse();
        parentSlugParserResult.Errors.Should().BeNull();
        parentSlugParserResult.ParsedParentSlug.Slug.Should().Be(slug);
        parentSlugParserResult.ParsedParentSlug.Parent.Id.Should().Be("root");
    }
    
    [Fact]
    public async Task ParentSlugParser_ParsesPublicIdChild()
    {
        // Arrange
        var slug = nameof(ParentSlugParser_ParsesPublicIdChild);
        
        // Act
        var parentSlugParserResult = await parentSlugParser.Parse(new PresentationCollection
        {
            PublicId = $"http://localhost/{Customer}/first-child/{slug}",
        }, Customer, null);

        // Assert
        parentSlugParserResult.IsError.Should().BeFalse();
        parentSlugParserResult.Errors.Should().BeNull();
        parentSlugParserResult.ParsedParentSlug.Slug.Should().Be(slug);
        parentSlugParserResult.ParsedParentSlug.Parent.Id.Should().Be("FirstChildCollection");
    }
    
    [Fact]
    public async Task ParentSlugParser_Fails_PublicIdNotMatchingSlug()
    {
        // Arrange
        var slug = nameof(ParentSlugParser_Fails_PublicIdNotMatchingSlug);
        
        // Act
        var parentSlugParserResult = await parentSlugParser.Parse(new PresentationCollection
        {
            PublicId = $"http://localhost/{Customer}/{slug}",
            Slug = "differentSlug"
        }, Customer, null);

        // Assert
        parentSlugParserResult.IsError.Should().BeTrue();
        parentSlugParserResult.Errors.Error.Should().Be("The slug must match the one specified in the public id");
    }
    
    [Fact]
    public async Task ParentSlugParser_Fails_PublicIdNotMatchingParent()
    {
        // Arrange
        var slug = nameof(ParentSlugParser_Fails_PublicIdNotMatchingParent);
        
        // Act
        var parentSlugParserResult = await parentSlugParser.Parse(new PresentationCollection
        {
            PublicId = $"http://localhost/{Customer}/{slug}",
            Parent = $"http://localhost/{Customer}/collections/FirstChildCollection"
        }, Customer, null);

        // Assert
        parentSlugParserResult.IsError.Should().BeTrue();
        parentSlugParserResult.Errors.Error.Should().Be("The parent must match the one specified in the public id");
    }
    
    [Fact]
    public async Task ParentSlugParser_Fails_InvalidParent()
    {
        // Arrange
        var slug = nameof(ParentSlugParser_Fails_InvalidParent);
        
        // Act
        var parentSlugParserResult = await parentSlugParser.Parse(new PresentationCollection
        {
            Parent = $"http://localhost/{Customer}/collections/NotAParent",
            Slug = slug
        }, Customer, null);

        // Assert
        parentSlugParserResult.IsError.Should().BeTrue();
        parentSlugParserResult.Errors.Error.Should().Be("The parent collection could not be found");
    }
    
    [Fact]
    public async Task ParentSlugParser_Fails_InvalidHost()
    {
        var slug = nameof(ParentSlugParser_Fails_InvalidHost);
        
        // Act
        var parentSlugParserResult = await parentSlugParser.Parse(new PresentationCollection
        {
            PublicId = $"http://example.com/{Customer}/{slug}",
        }, Customer, null);

        // Assert
        parentSlugParserResult.IsError.Should().BeTrue();
        parentSlugParserResult.Errors.Error.Should().Be("The parent collection could not be found");
    }
    
    [Fact]
    public async Task ParentSlugParser_Fails_ParentIsIIIF()
    {
        // Arrange
        var slug = nameof(ParentSlugParser_Fails_ParentIsIIIF);
        
        // Act
        var parentSlugParserResult = await parentSlugParser.Parse(new PresentationCollection
        {
            Parent = $"http://localhost/{Customer}/collections/IiifCollection",
            Slug = slug
        }, Customer, null);

        // Assert
        parentSlugParserResult.IsError.Should().BeTrue();
        parentSlugParserResult.Errors.Error.Should().Be("The parent must be a storage collection");
    }
}
