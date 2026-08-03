using System.Data;
using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using API.Infrastructure.IdGenerator;
using API.Infrastructure.Requests;
using FluentValidation;
using IIIF.Presentation.V3;
using Models.API;
using Models.Database;
using Models.Database.General;
using Repository;
using Repository.Helpers;

namespace API.Helpers;

/// <summary>
/// Result of <see cref="IHierarchicalRequestHelper.PrepareForCreate{T}"/> - everything needed to build a Create
/// request once the body has been deserialized, validated, and its id/parent reconciled against the URL.
/// </summary>
public record HierarchicalPostContext<T>(T Presentation, string ParentPath, string? Slug, string? ClientProvidedId);

/// <summary>
/// Result of <see cref="IHierarchicalRequestHelper.PrepareForUpsert{T,TDbEntity}"/> - everything needed to build an
/// Upsert request once the body has been deserialized, validated, and its id/parent/slug reconciled against the URL.
/// </summary>
public record HierarchicalPutContext<T>(T Presentation, string ParentPath, string Slug, string ResourceId);

/// <summary>
/// Result of deserializing, validating, and resolving the body's "id" property - the part of the hierarchical
/// POST/PUT prologue that's identical for both.
/// </summary>
public class DeserializedRequest<T>
{
    public PresentationResult? Error { get; private init; }
    public T? Presentation { get; private init; }
    public ResolvedRequestId? ResolvedId { get; private init; }

    public static DeserializedRequest<T> Failure(PresentationResult error) => new() { Error = error };

    public static DeserializedRequest<T> Success(T presentation, ResolvedRequestId resolvedId) =>
        new() { Presentation = presentation, ResolvedId = resolvedId };
}

/// <summary>
/// Resolves and validates the shared id/parent/slug prologue for creating or upserting a resource at a
/// hierarchical path. Centralising this means any rule that should apply to hierarchical writes of both resource
/// types (a new validation step, a new id/parent/slug source, etc.) only needs to be added once.
/// </summary>
public interface IHierarchicalRequestHelper
{
    /// <summary>
    /// Deserializes, validates (isFlatRequest:false), resolves the body's "id" property, and reconciles the
    /// URL-derived parent path against it - the full prologue for creating a resource at a hierarchical path.
    /// </summary>
    /// <param name="rawRequestBody">The raw request body</param>
    /// <param name="urlParentPath">Parent path derived from the request URL - the container being posted into</param>
    /// <param name="customerId">Customer id from the request URL</param>
    /// <param name="validator">Validator to run against the deserialized body</param>
    /// <param name="deserializeError">Error to return if the body can't be deserialized</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<(PresentationResult? error, HierarchicalPostContext<T>? context)> PrepareForCreate<T>(
        string rawRequestBody,
        string urlParentPath,
        int customerId,
        IValidator<T> validator,
        PresentationResult deserializeError,
        CancellationToken cancellationToken)
        where T : ResourceBase, IPresentation, new();

    /// <summary>
    /// Deserializes, validates (isFlatRequest:false), resolves the body's "id" property, reconciles the
    /// URL-derived parent path and slug against it, and resolves the internal id to write to (an existing resource
    /// at that path wins, otherwise a trusted id from the body, otherwise a freshly minted one) - the full prologue
    /// for creating or updating a resource at a specific hierarchical path.
    /// </summary>
    /// <param name="rawRequestBody">The raw request body</param>
    /// <param name="fullPath">Full hierarchical path the request was PUT to, including the resource's own slug</param>
    /// <param name="customerId">Customer id from the request URL</param>
    /// <param name="validator">Validator to run against the deserialized body</param>
    /// <param name="deserializeError">Error to return if the body can't be deserialized</param>
    /// <param name="existingResourceId">Selects the relevant id (CollectionId/ManifestId) off an existing hierarchy row</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="T">The Presentation model (PresentationCollection/PresentationManifest)</typeparam>
    /// <typeparam name="TDbEntity">The database entity type ids are minted against (Collection/Manifest)</typeparam>
    Task<(PresentationResult? error, HierarchicalPutContext<T>? context)> PrepareForUpsert<T, TDbEntity>(
        string rawRequestBody,
        string fullPath,
        int customerId,
        IValidator<T> validator,
        PresentationResult deserializeError,
        Func<Hierarchy?, string?> existingResourceId,
        CancellationToken cancellationToken)
        where T : ResourceBase, IPresentation, new()
        where TDbEntity : class, IIdentifiable;
}

public class HierarchicalRequestHelper(
    PresentationContext dbContext,
    IdentityManager identityManager,
    IRequestIdResolver requestIdResolver,
    ILogger<HierarchicalRequestHelper> logger) : IHierarchicalRequestHelper
{
    public async Task<(PresentationResult? error, HierarchicalPostContext<T>? context)> PrepareForCreate<T>(
        string rawRequestBody,
        string urlParentPath,
        int customerId,
        IValidator<T> validator,
        PresentationResult deserializeError,
        CancellationToken cancellationToken)
        where T : ResourceBase, IPresentation, new()
    {
        var deserialized = await DeserializeValidateAndResolveId(rawRequestBody, customerId, validator,
            deserializeError, cancellationToken);
        if (deserialized.Error != null) return (deserialized.Error, null);
        var resolvedId = deserialized.ResolvedId!;

        var (parentPathError, parentPath) = ReconcileId(urlParentPath, resolvedId.HierarchicalParentPath,
            UpsertErrorHelper.ParentSourcesDoNotMatch());
        if (parentPathError != null) return (parentPathError, null);

        return (null,
            new HierarchicalPostContext<T>(deserialized.Presentation!, parentPath!, resolvedId.Slug, resolvedId.FlatId));
    }

    public async Task<(PresentationResult? error, HierarchicalPutContext<T>? context)> PrepareForUpsert<T, TDbEntity>(
        string rawRequestBody,
        string fullPath,
        int customerId,
        IValidator<T> validator,
        PresentationResult deserializeError,
        Func<Hierarchy?, string?> existingResourceId,
        CancellationToken cancellationToken)
        where T : ResourceBase, IPresentation, new()
        where TDbEntity : class, IIdentifiable
    {
        var deserialized = await DeserializeValidateAndResolveId(rawRequestBody, customerId, validator,
            deserializeError, cancellationToken);
        if (deserialized.Error != null) return (deserialized.Error, null);
        var resolvedId = deserialized.ResolvedId!;

        var (urlParentPath, urlSlug) = SplitPath(fullPath);

        var (parentPathError, parentPath) = ReconcileId(urlParentPath, resolvedId.HierarchicalParentPath,
            UpsertErrorHelper.ParentSourcesDoNotMatch());
        if (parentPathError != null) return (parentPathError, null);

        var (slugError, slug) = ReconcileId(urlSlug, resolvedId.Slug, UpsertErrorHelper.SlugSourcesDoNotMatch());
        if (slugError != null) return (slugError, null);

        var existingHierarchy = await dbContext.RetrieveHierarchy(customerId, fullPath, cancellationToken);

        var (idError, resourceId) = await ResolveIdForUpsert<TDbEntity>(existingResourceId(existingHierarchy),
            resolvedId.FlatId, customerId, cancellationToken);
        if (idError != null) return (idError, null);

        return (null, new HierarchicalPutContext<T>(deserialized.Presentation!, parentPath!, slug!, resourceId!));
    }

    /// <summary>
    /// Deserializes the body, validates it, and resolves its "id" property - the part of the prologue common to
    /// both creating and upserting a resource at a hierarchical path.
    /// </summary>
    private async Task<DeserializedRequest<T>> DeserializeValidateAndResolveId<T>(
        string rawRequestBody,
        int customerId,
        IValidator<T> validator,
        PresentationResult deserializeError,
        CancellationToken cancellationToken)
        where T : ResourceBase, IPresentation, new()
    {
        var deserialized = await rawRequestBody.TryDeserializePresentation<T>(logger);
        if (deserialized.Error) return DeserializedRequest<T>.Failure(deserializeError);
        var presentation = deserialized.ConvertedIIIF!;

        var validationError = await UpsertErrorHelper.ValidateAsync(validator, presentation, cancellationToken);
        if (validationError != null) return DeserializedRequest<T>.Failure(validationError);

        var resolvedId = requestIdResolver.Resolve(customerId, presentation.Id);
        if (resolvedId.IsError) return DeserializedRequest<T>.Failure(resolvedId.Error!);

        return DeserializedRequest<T>.Success(presentation, resolvedId);
    }

    /// <summary>
    /// Splits a full hierarchical path (as addressed by a PUT) into the parent path (everything but the last
    /// segment) and the slug (the last segment)
    /// </summary>
    private static (string parentPath, string slug) SplitPath(string fullPath)
    {
        var lastSeparator = fullPath.LastIndexOf('/');
        return lastSeparator >= 0
            ? (fullPath[..lastSeparator], fullPath[(lastSeparator + 1)..])
            : (string.Empty, fullPath);
    }

    /// <summary>
    /// Reconciles a value derived from the request URL against one derived from the body's "id" property - if both
    /// are present they must agree, otherwise <paramref name="mismatchError"/> is returned
    /// </summary>
    private static (PresentationResult? error, string? value) ReconcileId(string urlValue, string? idValue,
        PresentationResult mismatchError)
    {
        if (idValue != null && idValue != urlValue)
        {
            return (mismatchError, null);
        }

        return (null, urlValue);
    }

    /// <summary>
    /// Resolves the internal id to write to when upserting a resource at a hierarchical path: an existing
    /// resource at that path wins, otherwise a trusted id from the request body's "id" property, otherwise a
    /// freshly minted one.
    /// </summary>
    private async Task<(PresentationResult? error, string? id)> ResolveIdForUpsert<T>(string? existingId,
        string? resolvedFlatId, int customerId, CancellationToken cancellationToken)
        where T : class, IIdentifiable
    {
        if (existingId != null) return (null, existingId);
        if (resolvedFlatId != null) return (null, resolvedFlatId);

        var generatedId = await GenerateUniqueId<T>(customerId, cancellationToken);
        return generatedId == null ? (UpsertErrorHelper.CannotGenerateUniqueId(), null) : (null, generatedId);
    }

    private async Task<string?> GenerateUniqueId<T>(int customerId, CancellationToken cancellationToken)
        where T : class, IIdentifiable
    {
        try
        {
            return await identityManager.GenerateUniqueId<T>(customerId, cancellationToken);
        }
        catch (ConstraintException ex)
        {
            logger.LogError(ex, "An exception occured while generating a unique id");
            return null;
        }
    }
}
