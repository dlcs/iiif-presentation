using System.Runtime.InteropServices;
using API.Infrastructure;
using API.Infrastructure.Helpers;
using API.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Test.Helpers.Helpers;

namespace API.Tests.Infrastructure;

public class ETagCacheTests
{
    private readonly ETagCache eTagCache;
    private readonly MemoryCache cache;
    
    public ETagCacheTests()
    {
        cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        var settings = new TestOptionsMonitor<CacheSettings>(new CacheSettings());
        eTagCache = new ETagCache(cache, settings);
    }

    [Fact]
    public void SetEtagForPath_Sets_TwoWayEntries()
    {
        // Arrange
        const string path = nameof(SetEtagForPath_Sets_TwoWayEntries);
        var guid = Guid.NewGuid();
        
        // Act
        eTagCache.SetEtagForPath(path, guid);
        
        // Assert
        
        // Direct
        cache.Get<Guid>($"__etag_ebp:{path}").Should().Be(guid);
        // Reverse
        cache.Get<string>($"__etag_pbe:{guid:N}").Should().Be(path);
    }
    
    [Fact]
    public void IfNoneMatchForPath_Matches_ExistingEntry()
    {
        // Arrange
        const string path = nameof(IfNoneMatchForPath_Matches_ExistingEntry);
        var guid = Guid.NewGuid();
        eTagCache.SetEtagForPath(path, guid);
        var ifNoneMatchHeaderValue = new StringValues($"\"{guid:N}\"");
        var providedETags = ifNoneMatchHeaderValue.AsETagValues();
        
        // Act
        var doesMatch = eTagCache.IfNoneMatchForPath(path, providedETags, out var etag);
        
        // Assert
        doesMatch.Should().BeTrue();
        etag.Should().Be(guid);
    }
    
    [Fact]
    public void IfNoneMatchForPath_Matches_ExistingEntry_FromMultiple()
    {
        // Arrange
        const string path = nameof(IfNoneMatchForPath_Matches_ExistingEntry);
        var guid = Guid.NewGuid();
        eTagCache.SetEtagForPath(path, guid);
        var providedGuids = new[] { Guid.NewGuid(), guid, Guid.NewGuid() }.Select(g => $"\"{g:N}\"").ToArray();
        var ifNoneMatchHeaderValue = new StringValues(providedGuids);
        var providedETags = ifNoneMatchHeaderValue.AsETagValues();
        
        // Act
        var doesMatch = eTagCache.IfNoneMatchForPath(path, providedETags, out var etag);
        
        // Assert
        doesMatch.Should().BeTrue();
        etag.Should().Be(guid);
    }
    
    [Fact]
    public void Invalidate_ByGuid_Removes_TwoWayEntries()
    {
        // Arrange
        const string path = nameof(Invalidate_ByGuid_Removes_TwoWayEntries);
        var guid = Guid.NewGuid();
        eTagCache.SetEtagForPath(path, guid);
        
        // Act
        eTagCache.Invalidate(guid);
        
        // Assert
        // Direct
        cache.Get<Guid>($"__etag_ebp:{path}").Should().Be(Guid.Empty); // default(Guid)
        // Reverse
        cache.Get<string>($"__etag_pbe:{guid:N}").Should().BeNull();
    }
    
    [Fact]
    public void Invalidate_ByString_Removes_TwoWayEntries()
    {
        // Arrange
        const string path = nameof(Invalidate_ByString_Removes_TwoWayEntries);
        var guid = Guid.NewGuid();
        eTagCache.SetEtagForPath(path, guid);
        
        // Act
        var etag = $"\"{guid:N}\"";
        eTagCache.Invalidate(etag);
        
        // Assert
        // Direct
        cache.Get<Guid>($"__etag_ebp:{path}").Should().Be(Guid.Empty); // default(Guid)
        // Reverse
        cache.Get<string>($"__etag_pbe:{guid:N}").Should().BeNull();
    }
    
    [Theory]
    [InlineData("")]
    [InlineData((string?)null)]
    [InlineData("Def not a guid")]
    [InlineData("3cf7842c858149be87e4c6f527c35f3f")]
    [InlineData("\"3cf7842c858149be87e4c6f527c35f3f\"")]
    public void Invalidate_ByString_DoesNotThrow(string? etag)
    {
        var action = new Action(() => eTagCache.Invalidate(etag));
        action.Should().NotThrow();
    }
}
