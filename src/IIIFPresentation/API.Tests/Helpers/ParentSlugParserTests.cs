using API.Helpers;
using API.Tests.Integration.Infrastructure;
using DLCS;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Repository;
using Test.Helpers.Integration;

namespace API.Tests.Helpers;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class ParentSlugParserTests
{
    private readonly PresentationContext presentationContext;
    private ParentSlugParser parentSlugParser;
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
        
        parentSlugParser = new ParentSlugParser(presentationContext,
            new HttpRequestBasedPathGenerator(httpContextAccessor, options), httpContextAccessor);
    }

    [Fact]
    public async Task ParentSlugParser_ParsesFlatRootParentSlug()
    {
        // Arrange
        var slug = nameof(ParentSlugParser_ParsesFlatRootParentSlug);
        
        // Act
        var parentSlugParserResult = await parentSlugParser.Parse<PresentationCollection>(new PresentationCollection
        {
            Id = "someId",
            Parent = $"http://localhost/{Customer}/collections/root",
            Slug = slug
        }, Customer);

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
        var parentSlugParserResult = await parentSlugParser.Parse<PresentationCollection>(new PresentationCollection
        {
            Id = "someId",
            Parent = $"http://localhost/{Customer}/collections/FirstChildCollection",
            Slug = slug
        }, Customer);

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
        var parentSlugParserResult = await parentSlugParser.Parse<PresentationCollection>(new PresentationCollection
        {
            Id = "someId",
            Parent = $"http://localhost/{Customer}",
            Slug = slug
        }, Customer);

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
        var parentSlugParserResult = await parentSlugParser.Parse<PresentationCollection>(new PresentationCollection
        {
            Id = "someId",
            Parent = $"http://localhost/{Customer}/first-child",
            Slug = slug
        }, Customer);

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
        var parentSlugParserResult = await parentSlugParser.Parse<PresentationCollection>(new PresentationCollection
        {
            Id = "someId",
            PublicId = $"http://localhost/{Customer}/{slug}",
        }, Customer);

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
        var parentSlugParserResult = await parentSlugParser.Parse<PresentationCollection>(new PresentationCollection
        {
            Id = "someId",
            PublicId = $"http://localhost/{Customer}/first-child/{slug}",
        }, Customer);

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
        var parentSlugParserResult = await parentSlugParser.Parse<PresentationCollection>(new PresentationCollection
        {
            Id = "someId",
            PublicId = $"http://localhost/{Customer}/{slug}",
            Slug = "differentSlug"
        }, Customer);

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
        var parentSlugParserResult = await parentSlugParser.Parse<PresentationCollection>(new PresentationCollection
        {
            Id = "someId",
            PublicId = $"http://localhost/{Customer}/{slug}",
            Parent = $"http://localhost/{Customer}/collections/FirstChildCollection"
        }, Customer);

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
        var parentSlugParserResult = await parentSlugParser.Parse<PresentationCollection>(new PresentationCollection
        {
            Id = "someId",
            Parent = $"http://localhost/{Customer}/collections/NotAParent",
            Slug = slug
        }, Customer);

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
        var parentSlugParserResult = await parentSlugParser.Parse<PresentationCollection>(new PresentationCollection
        {
            Id = "someId",
            Parent = $"http://localhost/{Customer}/collections/IiifCollection",
            Slug = slug
        }, Customer);

        // Assert
        parentSlugParserResult.IsError.Should().BeTrue();
        parentSlugParserResult.Errors.Error.Should().Be("The parent must be a storage collection");
    }
}
