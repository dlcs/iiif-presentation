using AWS.Helpers;
using AWS.S3;
using AWS.S3.Models;
using AWS.Settings;
using Core.Streams;
using IIIF.Presentation.V3.Strings;
using Microsoft.Extensions.Options;
using Models.Database.Collections;
using Newtonsoft.Json.Linq;
using Repository;
using Repository.Helpers;
using Repository.Paths;
using Services.Manifests.Helpers;

namespace Services.Search;

public class SearchDocumentBuilder(
    PresentationContext dbContext,
    IBucketReader bucketReader,
    IOptionsMonitor<AWSSettings> awsSettings,
    IPathGenerator pathGenerator,
    SettingsBasedPathGenerator settingsBasedPathGenerator) : ISearchDocumentBuilder
{
    public async Task<SearchDocument?> BuildAsync(SearchResourceTarget target, CancellationToken cancellationToken = default) =>
        target.ResourceType switch
        {
            SearchResourceType.StorageCollection or SearchResourceType.IiifCollection
                => await BuildCollectionDocument(target, cancellationToken),
            SearchResourceType.Manifest => await BuildManifestDocument(target, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(target.ResourceType), target.ResourceType, null)
        };

    private async Task<SearchDocument?> BuildCollectionDocument(SearchResourceTarget target, CancellationToken cancellationToken)
    {
        var collection = await dbContext.RetrieveCollectionAsync(target.CustomerId, target.FlatId, cancellationToken: cancellationToken);
        if (collection == null) return null;

        var hierarchy = collection.Hierarchy.GetCanonical();
        var fullPath = hierarchy.Parent == null
            ? string.Empty
            : await CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext, cancellationToken);
        hierarchy.FullPath = fullPath;

        var descriptive = collection.IsStorageCollection
            ? GetStorageCollectionDescriptiveFields(collection)
            : await GetStoredResourceFields(collection, cancellationToken);

        return new SearchDocument
        {
            Id = SearchDocumentId.Generate(target),
            CustomerId = collection.CustomerId,
            ResourceType = target.ResourceType.ToTypesenseValue(),
            FlatId = collection.Id,
            PublicId = GeneratePublicId(collection.CustomerId, fullPath),
            ApiId = pathGenerator.GenerateFlatId(hierarchy),
            Slug = hierarchy.Slug,
            FullPath = fullPath,
            ParentFlatId = hierarchy.Parent != null ? pathGenerator.GenerateFlatParentId(hierarchy) : null,
            Label = descriptive.Label ?? FlattenLanguageMap(collection.Label),
            SummaryText = descriptive.SummaryText,
            MetadataText = descriptive.MetadataText,
            RequiredStatementText = descriptive.RequiredStatementText,
            ProviderText = descriptive.ProviderText,
            HomepageText = descriptive.HomepageText,
            SeeAlsoText = descriptive.SeeAlsoText,
            RenderingText = descriptive.RenderingText,
            Rights = descriptive.Rights,
            NavDateTimestamp = descriptive.NavDateTimestamp,
            Thumbnail = collection.Thumbnail ?? descriptive.Thumbnail,
            Tags = ParseTags(collection.Tags),
            IsPublic = collection.IsPublic,
            IsProcessed = true,
            IsInProgress = false,
            ModifiedTimestamp = ToUnixTimestamp(collection.Modified),
            IiifDescriptive = descriptive.StructuredFields
        };
    }

    private async Task<SearchDocument?> BuildManifestDocument(SearchResourceTarget target, CancellationToken cancellationToken)
    {
        var manifest = await dbContext.RetrieveManifestAsync(target.CustomerId, target.FlatId, withCanvasPaintings: true, withBatches: true,
            cancellationToken: cancellationToken);
        if (manifest == null) return null;

        var hierarchy = manifest.Hierarchy.GetCanonical();
        var fullPath = await ManifestRetrieval.RetrieveFullPathForManifest(manifest.Id, manifest.CustomerId, dbContext, cancellationToken);
        hierarchy.FullPath = fullPath;

        var descriptive = await GetStoredResourceFields(manifest, cancellationToken, manifest.IsIngesting());

        return new SearchDocument
        {
            Id = SearchDocumentId.Generate(target),
            CustomerId = manifest.CustomerId,
            ResourceType = target.ResourceType.ToTypesenseValue(),
            FlatId = manifest.Id,
            PublicId = GeneratePublicId(manifest.CustomerId, fullPath),
            ApiId = pathGenerator.GenerateFlatId(hierarchy),
            Slug = hierarchy.Slug,
            FullPath = fullPath,
            ParentFlatId = hierarchy.Parent != null ? pathGenerator.GenerateFlatParentId(hierarchy) : null,
            Label = descriptive.Label ?? FlattenLanguageMap(manifest.Label),
            SummaryText = descriptive.SummaryText,
            MetadataText = descriptive.MetadataText,
            RequiredStatementText = descriptive.RequiredStatementText,
            ProviderText = descriptive.ProviderText,
            HomepageText = descriptive.HomepageText,
            SeeAlsoText = descriptive.SeeAlsoText,
            RenderingText = descriptive.RenderingText,
            Rights = descriptive.Rights,
            NavDateTimestamp = descriptive.NavDateTimestamp,
            Thumbnail = GetManifestThumbnail(manifest) ?? descriptive.Thumbnail,
            IsPublic = manifest.LastProcessed.HasValue,
            IsProcessed = manifest.LastProcessed.HasValue,
            IsInProgress = manifest.IsIngesting(),
            ModifiedTimestamp = ToUnixTimestamp(manifest.Modified),
            LastProcessedTimestamp = manifest.LastProcessed.HasValue ? ToUnixTimestamp(manifest.LastProcessed.Value) : null,
            IiifDescriptive = descriptive.StructuredFields
        };
    }

    private async Task<DescriptiveFields> GetStoredResourceFields(IHierarchyResource resource, CancellationToken cancellationToken,
        bool fromStaging = false)
    {
        var storedJson = await ReadStoredResource(resource, fromStaging, cancellationToken);
        if (storedJson == null && fromStaging)
        {
            storedJson = await ReadStoredResource(resource, false, cancellationToken);
        }

        return storedJson == null ? new DescriptiveFields() : ExtractDescriptiveFields(storedJson);
    }

    private async Task<JObject?> ReadStoredResource(IHierarchyResource resource, bool fromStaging, CancellationToken cancellationToken)
    {
        var bucketName = awsSettings.CurrentValue.S3.StorageBucket;
        var objectFromBucket = await bucketReader.GetObjectFromBucket(
            new ObjectInBucket(bucketName, resource.GetResourceBucketKey(fromStaging)), cancellationToken);

        if (objectFromBucket.Stream.IsNull()) return null;

        using var streamReader = new StreamReader(objectFromBucket.Stream);
        var json = await streamReader.ReadToEndAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(json) ? null : JObject.Parse(json);
    }

    private string GeneratePublicId(int customerId, string fullPath) =>
        settingsBasedPathGenerator.HasPathForCustomer(customerId)
            ? settingsBasedPathGenerator.GenerateHierarchicalFromFullPath(customerId, fullPath)
            : pathGenerator.GenerateHierarchicalFromFullPath(customerId, fullPath);

    private static string? GetManifestThumbnail(Manifest manifest) =>
        manifest.CanvasPaintings?
            .OrderCanvasPaintings()
            .FirstOrDefault(cp => cp.Thumbnail != null)?
            .Thumbnail?
            .ToString();

    private static string[]? ParseTags(string? tags) =>
        string.IsNullOrWhiteSpace(tags)
            ? null
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static DescriptiveFields GetStorageCollectionDescriptiveFields(Collection collection)
    {
        var structuredFields = new JObject();
        if (collection.Label != null)
        {
            structuredFields["label"] = JObject.FromObject(collection.Label);
        }

        return new DescriptiveFields
        {
            Label = FlattenLanguageMap(collection.Label),
            Thumbnail = collection.Thumbnail,
            StructuredFields = structuredFields.HasValues ? structuredFields : null
        };
    }

    private static DescriptiveFields ExtractDescriptiveFields(JObject storedJson)
    {
        var structuredFields = new JObject();
        AddStructuredField("label");
        AddStructuredField("summary");
        AddStructuredField("metadata");
        AddStructuredField("requiredStatement");
        AddStructuredField("provider");
        AddStructuredField("homepage");
        AddStructuredField("seeAlso");
        AddStructuredField("rendering");
        AddStructuredField("rights");
        AddStructuredField("navDate");
        AddStructuredField("thumbnail");

        return new DescriptiveFields
        {
            Label = ExtractText(storedJson["label"]),
            SummaryText = ExtractText(storedJson["summary"]),
            MetadataText = ExtractText(storedJson["metadata"]),
            RequiredStatementText = ExtractText(storedJson["requiredStatement"]),
            ProviderText = ExtractText(storedJson["provider"]),
            HomepageText = ExtractText(storedJson["homepage"]),
            SeeAlsoText = ExtractText(storedJson["seeAlso"]),
            RenderingText = ExtractText(storedJson["rendering"]),
            Rights = storedJson["rights"]?.Value<string>() ?? ExtractText(storedJson["rights"]),
            NavDateTimestamp = ParseNavDate(storedJson["navDate"]?.Value<string>()),
            Thumbnail = ExtractThumbnail(storedJson["thumbnail"]),
            StructuredFields = structuredFields.HasValues ? structuredFields : null
        };

        void AddStructuredField(string fieldName)
        {
            if (storedJson[fieldName] != null)
            {
                structuredFields[fieldName] = storedJson[fieldName]!.DeepClone();
            }
        }
    }

    private static string? ExtractThumbnail(JToken? thumbnailToken)
    {
        if (thumbnailToken == null) return null;
        if (thumbnailToken.Type == JTokenType.String) return thumbnailToken.Value<string>();

        return thumbnailToken
            .DescendantsAndSelf()
            .OfType<JProperty>()
            .Where(p => p.Name is "id" or "@id")
            .Select(p => p.Value.Value<string>())
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }

    private static string? FlattenLanguageMap(LanguageMap? languageMap) =>
        languageMap == null
            ? null
            : string.Join(' ', languageMap.Values.SelectMany(v => v).Where(v => !string.IsNullOrWhiteSpace(v))).NullIfEmpty();

    private static string? ExtractText(JToken? token)
    {
        if (token == null) return null;

        var values = token.DescendantsAndSelf()
            .OfType<JValue>()
            .Where(v => v.Type is JTokenType.String or JTokenType.Integer or JTokenType.Float)
            .Select(v => v.ToString())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        return values.Count == 0 ? null : string.Join(' ', values);
    }

    private static long? ParseNavDate(string? navDate)
    {
        if (string.IsNullOrWhiteSpace(navDate)) return null;
        return DateTimeOffset.TryParse(navDate, out var parsed) ? parsed.ToUnixTimeSeconds() : null;
    }

    private static long ToUnixTimestamp(DateTime dateTime) => new DateTimeOffset(dateTime).ToUnixTimeSeconds();

    private sealed class DescriptiveFields
    {
        public string? Label { get; init; }
        public string? SummaryText { get; init; }
        public string? MetadataText { get; init; }
        public string? RequiredStatementText { get; init; }
        public string? ProviderText { get; init; }
        public string? HomepageText { get; init; }
        public string? SeeAlsoText { get; init; }
        public string? RenderingText { get; init; }
        public string? Rights { get; init; }
        public long? NavDateTimestamp { get; init; }
        public string? Thumbnail { get; init; }
        public JObject? StructuredFields { get; init; }
    }
}

internal static class SearchStringX
{
    public static string? NullIfEmpty(this string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
