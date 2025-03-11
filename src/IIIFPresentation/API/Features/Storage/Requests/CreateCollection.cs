using System.Data;
using API.Converters;
using API.Features.Storage.Helpers;
using API.Features.Storage.Models;
using API.Helpers;
using API.Infrastructure.IdGenerator;
using API.Infrastructure.Requests;
using API.Settings;
using AWS.Helpers;
using Core;
using Core.Auth;
using MediatR;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Models.API.General;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using Repository.Paths;
using Collection = Models.Database.Collections.Collection;

namespace API.Features.Storage.Requests;

/// <summary>
/// Create a new Collection (storage or iiif) in DB and upload provided JSON to S3 if iiif-collection
/// </summary>
public class CreateCollection(int customerId, PresentationCollection collection, string rawRequestBody)
    : IRequest<ModifyEntityResult<PresentationCollection, ModifyCollectionType>>
{
    public int CustomerId { get; } = customerId;

    public PresentationCollection Collection { get; } = collection;
    
    public string RawRequestBody { get; } = rawRequestBody;
}

public class CreateCollectionHandler(
    PresentationContext dbContext,
    ILogger<CreateCollectionHandler> logger,
    IIIFS3Service iiifS3,
    IdentityManager identityManager,
    IPathGenerator pathGenerator,
    IParentSlugParser parentSlugParser,
    IOptions<ApiSettings> options)
    : IRequestHandler<CreateCollection, ModifyEntityResult<PresentationCollection, ModifyCollectionType>>
{
    private readonly ApiSettings settings = options.Value;

    private const int CurrentPage = 1;
    
    public async Task<ModifyEntityResult<PresentationCollection, ModifyCollectionType>> Handle(CreateCollection request, CancellationToken cancellationToken)
    {
        var isStorageCollection = request.Collection.Behavior.IsStorageCollection();
        TryConvertIIIFResult<IIIF.Presentation.V3.Collection>? iiifCollection = null;
        if (!isStorageCollection)
        {
            iiifCollection = request.RawRequestBody.ConvertCollectionToIIIF<IIIF.Presentation.V3.Collection>(logger);
            if (iiifCollection.Error) return ErrorHelper.CannotValidateIIIF<PresentationCollection>();
        }
        
        var parsedParentSlugResult =
            await parentSlugParser.Parse<PresentationCollection>(request.Collection, request.CustomerId, null, cancellationToken);
        if (parsedParentSlugResult.IsError) return parsedParentSlugResult.Errors;
        var parsedParentSlug = parsedParentSlugResult.ParsedParentSlug;
            
        string id;

        try
        {
            id = await identityManager.GenerateUniqueId<Collection>(request.CustomerId, cancellationToken);
        }
        catch (ConstraintException ex)
        {
            logger.LogError(ex, "An exception occured while generating a unique id");
            return ErrorHelper.CannotGenerateUniqueId<PresentationCollection>();
        }

        var dateCreated = DateTime.UtcNow;
        var collection = new Collection
        {
            Id = id,
            Created = dateCreated,
            Modified = dateCreated,
            CreatedBy = Authorizer.GetUser(),
            CustomerId = request.CustomerId,
            Tags = request.Collection.Tags,
            IsPublic = request.Collection.Behavior.IsPublic(),
            IsStorageCollection = isStorageCollection,
            Label = request.Collection.Label,
            Thumbnail = request.Collection.GetThumbnail(),
            Hierarchy =
            [
                new Hierarchy
                {
                    Type = isStorageCollection
                        ? ResourceType.StorageCollection
                        : ResourceType.IIIFCollection,
                    Slug = parsedParentSlug!.Slug!,
                    Canonical = true,
                    ItemsOrder = request.Collection.ItemsOrder,
                    Parent = parsedParentSlug.Parent!.Id
                }
            ]
        };

        dbContext.Collections.Add(collection);

        var saveErrors =
            await dbContext.TrySaveCollection<PresentationCollection>(request.CustomerId, logger,
                cancellationToken);

        if (saveErrors != null)
        {
            return saveErrors;
        }

        await UploadToS3IfRequiredAsync(collection, iiifCollection?.ConvertedIIIF, isStorageCollection,
            cancellationToken);
        
        collection.Hierarchy.GetCanonical().FullPath =
            await CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext, cancellationToken);

        var enrichedPresentationCollection = request.Collection.EnrichPresentationCollection(collection,
            settings.PageSize, CurrentPage, 0, [], parsedParentSlug.Parent, pathGenerator); // there can be no items attached to this, as it's just been created
        
        return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Success(
            enrichedPresentationCollection,
            WriteResult.Created);
    }

    private async Task UploadToS3IfRequiredAsync(Collection collection, IIIF.Presentation.V3.Collection? iiifCollection, 
        bool isStorageCollection, CancellationToken cancellationToken = default)
    {
        if (!isStorageCollection)
        {
            await iiifS3.SaveIIIFToS3(iiifCollection!, collection, pathGenerator.GenerateFlatCollectionId(collection),
                cancellationToken);
        }
    }
}
