#nullable disable

using API.Features.Storage.Helpers;
using API.Tests.Integration.Infrastructure;
using Core.Helpers;
using IIIF.Presentation.V3.Strings;
using Microsoft.EntityFrameworkCore;
using Models.Database.General;
using Repository;
using Repository.Collections;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace API.Tests.Helpers;

/// <summary>
/// Tests for <see cref="PresentationContextX.SearchCollectionItems"/> - the raw jsonb label search. Split out from
/// <see cref="PresentationContextXTests"/> as it needs its own seed data. Seeds under dedicated customers, each with
/// their own root collection, and queries via a context scoped to that customer.
/// </summary>
[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class SearchCollectionItemsTests
{
    private readonly PresentationContextFixture dbFixture;
    private const int Customer = 9001;
    private const int OtherCustomer = 9002;

    public SearchCollectionItemsTests(PresentationContextFixture dbFixture)
    {
        this.dbFixture = dbFixture;
        dbFixture.CleanUp();
    }

    private PresentationContext GetScopedContext(int customer = Customer)
    {
        var provider = new TestCustomerIdProvider();
        provider.SetCustomerId(customer);
        return dbFixture.GetNewPresentationContext(provider);
    }

    /// <summary>
    /// Gets a context for the customer, ensuring they have the root collection they'd get on creation. CleanUp()
    /// preserves 'root' for all customers, so this is idempotent across tests sharing the fixture.
    /// </summary>
    private async Task<PresentationContext> GetSeededContext(int customer = Customer)
    {
        var ctx = GetScopedContext(customer);
        if (!await ctx.Collections.AnyAsync(c => c.Id == RootCollection.Id))
        {
            await ctx.Collections.AddTestRootCollection(customer);
            await ctx.SaveChangesAsync();
        }

        return ctx;
    }

    private async Task Seed()
    {
        await using var ctx = await GetSeededContext();

        var hunterCollection = (await ctx.Collections.AddTestCollection(id: "hst-coll", customer: Customer)).Entity;
        hunterCollection.Label = new LanguageMap("en", ["Hunter S. Thompson"]);

        // token order differs from search, and value sits alongside a non-matching value
        await ctx.Manifests.AddTestManifest(id: "hst-man", customer: Customer,
            label: new LanguageMap("en", ["Fear and Loathing", "Thompson, Hunter"]));

        var emmaCollection = (await ctx.Collections.AddTestCollection(id: "emma-coll", customer: Customer)).Entity;
        emmaCollection.Label = new LanguageMap("en", ["Emma Thompson"]); // missing 'hunter'

        // 'hunter' and 'thompson' in *separate* values -> must NOT match under same-value AND
        var split = new LanguageMap("none", ["Hunter"]);
        split.Add("en", ["Thompson biography"]);
        await ctx.Manifests.AddTestManifest(id: "split-man", customer: Customer, label: split);

        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task SearchCollectionItems_MatchesAllTokensInSingleValue_CaseInsensitive_OrderIndependent()
    {
        await Seed();
        await using var ctx = GetScopedContext();

        var results = await ctx.SearchCollectionItems("hunter thompson".SplitOnWhitespace()).ToListAsync();

        results.Select(h => h.ResourceId).Should().BeEquivalentTo(["hst-coll", "hst-man"]);
    }

    [Fact]
    public async Task SearchCollectionItems_ExcludesPartialAndCrossValueMatches()
    {
        await Seed();
        await using var ctx = GetScopedContext();

        var resourceIds = (await ctx.SearchCollectionItems("hunter thompson".SplitOnWhitespace()).ToListAsync())
            .Select(h => h.ResourceId).ToList();

        resourceIds.Should().NotContain("emma-coll", "'Emma Thompson' is missing the 'hunter' token");
        resourceIds.Should().NotContain("split-man", "'hunter'/'thompson' are in separate label values");
    }

    [Fact]
    public async Task SearchCollectionItems_ReturnsNothing_WhenNoMatch()
    {
        await Seed();
        await using var ctx = GetScopedContext();

        var results = await ctx.SearchCollectionItems("kerouac".SplitOnWhitespace()).ToListAsync();

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchCollectionItems_IsScopedToCustomer_ByGlobalQueryFilter()
    {
        await Seed(); // customer 9001 gets hst-coll / hst-man matching 'hunter thompson'

        // an identically-labelled resource under a different customer
        await using (var otherCtx = await GetSeededContext(OtherCustomer))
        {
            await otherCtx.Manifests.AddTestManifest(id: "other-hst", customer: OtherCustomer,
                label: new LanguageMap("en", ["Hunter S. Thompson"]));
            await otherCtx.SaveChangesAsync();
        }

        await using var ctx = GetScopedContext(Customer);
        var resourceIds = (await ctx.SearchCollectionItems("hunter thompson".SplitOnWhitespace()).ToListAsync())
            .Select(h => h.ResourceId).ToList();

        resourceIds.Should().BeEquivalentTo(["hst-coll", "hst-man"]);
        resourceIds.Should().NotContain("other-hst", "the global query filter scopes results to the customer");
    }

    [Fact]
    public async Task SearchCollectionItems_ComposesOrderingAndPaging()
    {
        await Seed();
        await using var ctx = GetScopedContext();

        // single token 'thompson' matches every seeded item that has it in a value:
        // hst-coll, hst-man, emma-coll and split-man ('Thompson biography')
        var query = ctx.SearchCollectionItems("thompson".SplitOnWhitespace());

        var total = await query.CountAsync();
        var firstPage = await query.AsOrderedCollectionItemsQuery(orderBy: "id").Skip(0).Take(2).ToListAsync();
        var secondPage = await query.AsOrderedCollectionItemsQuery(orderBy: "id").Skip(2).Take(2).ToListAsync();

        total.Should().Be(4);
        firstPage.Should().HaveCount(2);
        secondPage.Should().HaveCount(2);
        firstPage.Concat(secondPage).Select(h => h.ResourceId).Should()
            .BeEquivalentTo(["hst-coll", "hst-man", "emma-coll", "split-man"]);
    }

    [Fact]
    public async Task SearchCollectionItems_MatchesResourcesAtAnyDepth()
    {
        await using (var seedCtx = await GetSeededContext())
        {
            var parent = (await seedCtx.Collections.AddTestCollection(id: "depth-parent", customer: Customer)).Entity;
            parent.Label = new LanguageMap("en", ["Somewhere else entirely"]);

            var child = (await seedCtx.Collections.AddTestCollection(id: "depth-child", customer: Customer,
                parent: "depth-parent")).Entity;
            child.Label = new LanguageMap("en", ["Kerouac letters"]);

            await seedCtx.Manifests.AddTestManifest(id: "depth-man", customer: Customer, parent: "depth-child",
                label: new LanguageMap("en", ["Kerouac scrolls"]));

            await seedCtx.SaveChangesAsync();
        }

        await using var ctx = GetScopedContext();
        var results = await ctx.SearchCollectionItems("kerouac".SplitOnWhitespace()).ToListAsync();

        results.Select(h => h.ResourceId).Should()
            .BeEquivalentTo(["depth-child", "depth-man"], "search is across all resources, regardless of nesting");
    }

    [Fact]
    public async Task SearchCollectionItems_ExcludesNonCanonicalHierarchyRows()
    {
        await using (var seedCtx = await GetSeededContext())
        {
            await seedCtx.Manifests.AddTestManifest(id: "alias-man", customer: Customer,
                label: new LanguageMap("en", ["Kerouac scrolls"]));

            // a second, non-canonical path to the same manifest - it must not yield a duplicate result
            seedCtx.Hierarchy.Add(new Hierarchy
            {
                ManifestId = "alias-man",
                CustomerId = Customer,
                Slug = "alias-man-alt",
                Parent = RootCollection.Id,
                Canonical = false,
                Type = ResourceType.IIIFManifest
            });

            await seedCtx.SaveChangesAsync();
        }

        await using var ctx = GetScopedContext();
        var results = await ctx.SearchCollectionItems("kerouac".SplitOnWhitespace()).ToListAsync();

        results.Should().ContainSingle().Which.Canonical.Should().BeTrue();
    }

    [Fact]
    public async Task SearchCollectionItems_MatchesLabelValue_InAnyLanguage()
    {
        await using (var seedCtx = await GetSeededContext())
        {
            await seedCtx.Manifests.AddTestManifest(id: "welsh-man", customer: Customer,
                label: new LanguageMap("cy", ["Llyfr Kerouac"]));
            await seedCtx.SaveChangesAsync();
        }

        await using var ctx = GetScopedContext();
        var results = await ctx.SearchCollectionItems("kerouac".SplitOnWhitespace()).ToListAsync();

        results.Select(h => h.ResourceId).Should().BeEquivalentTo(["welsh-man"],
            "all label values are searched, whatever the language key");
    }

    [Theory]
    [InlineData("a_c", "abc-man")] // '_' is a single-char ILIKE wildcard if not escaped
    [InlineData("50%", "pct-man")] // '%' is a multi-char ILIKE wildcard if not escaped
    public async Task SearchCollectionItems_TreatsWildcardCharacters_Literally(string term, string expectedMatch)
    {
        await using (var seedCtx = await GetSeededContext())
        {
            // would be matched by an unescaped 'a_c' / '50%' pattern
            await seedCtx.Manifests.AddTestManifest(id: "abc-decoy", customer: Customer,
                label: new LanguageMap("en", ["abc"]));
            await seedCtx.Manifests.AddTestManifest(id: "pct-decoy", customer: Customer,
                label: new LanguageMap("en", ["50 shades"]));

            // contain the literal characters
            await seedCtx.Manifests.AddTestManifest(id: "abc-man", customer: Customer,
                label: new LanguageMap("en", ["file a_c backup"]));
            await seedCtx.Manifests.AddTestManifest(id: "pct-man", customer: Customer,
                label: new LanguageMap("en", ["50% proof"]));

            await seedCtx.SaveChangesAsync();
        }

        await using var ctx = GetScopedContext();
        var results = await ctx.SearchCollectionItems(term.SplitOnWhitespace()).ToListAsync();

        results.Select(h => h.ResourceId).Should().BeEquivalentTo([expectedMatch]);
    }

    [Fact]
    public async Task SearchCollectionItems_IncludesCollectionAndManifest_ForConversion()
    {
        await Seed();
        await using var ctx = GetScopedContext();

        var results = await ctx.SearchCollectionItems("hunter thompson".SplitOnWhitespace()).ToListAsync();

        results.Single(h => h.ResourceId == "hst-coll").Collection.Should().NotBeNull();
        results.Single(h => h.ResourceId == "hst-man").Manifest.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchCollectionItems_IgnoresResourcesWithNoLabel()
    {
        await using (var seedCtx = await GetSeededContext())
        {
            // no label at all - must be skipped rather than blowing up the jsonb_each
            await seedCtx.Collections.AddTestCollection(customer: Customer);
            await seedCtx.Manifests.AddTestManifest(customer: Customer);
            await seedCtx.SaveChangesAsync();
        }

        await using var ctx = GetScopedContext();

        var results = await ctx.SearchCollectionItems("kerouac".SplitOnWhitespace()).ToListAsync();

        results.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchCollectionItems_ReturnsEmptyComposableQuery_WhenNoTokens(string term)
    {
        await Seed();
        await using var ctx = GetScopedContext();

        var query = ctx.SearchCollectionItems(term.SplitOnWhitespace());

        (await query.CountAsync()).Should().Be(0);
        (await query.Skip(0).Take(10).ToListAsync()).Should().BeEmpty();
    }
}
