using System.Data;
using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using API.Features.Storage.Requests;
using API.Infrastructure.IdGenerator;
using API.Infrastructure.Requests;
using AWS.Helpers;
using Core;
using Core.Auth;
using Core.IIIF;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Content;
using MediatR;
using Models.API.General;
using Models.API.Manifest;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using Repository.Paths;
using DatabaseCollection = Models.Database.Collections;

namespace API.Features.Manifest.Requests;

public class PutHierarchicalManifest(
    int customerId,
    string slug, 
    string rawRequestBody) : IRequest<ModifyEntityResult<PresentationManifest, ModifyCollectionType>>
{
    public int CustomerId { get; } = customerId;

    public string Slug { get; } = slug;
    
    public string RawRequestBody { get; } = rawRequestBody;
}

public class PutHierarchicalManifestHandler(
    PresentationContext dbContext,    
    ILogger<PutHierarchicalManifestHandler> logger,
    IdentityManager identityManager,
    IIIIFS3Service iiifS3,
    IPathGenerator pathGenerator)
    : IRequestHandler<PutHierarchicalManifest, ModifyEntityResult<PresentationManifest, ModifyCollectionType>>
{
    
    public async Task<ModifyEntityResult<PresentationManifest, ModifyCollectionType>> Handle(PutHierarchicalManifest request,
        CancellationToken cancellationToken)
    {
        var convertResult = await request.RawRequestBody.TryDeserializePresentation<PresentationManifest>(logger);
        if (convertResult.Error) return ErrorHelper.CannotValidateIIIF<PresentationManifest>();

        
        
        
        
        throw new NotImplementedException();
        // var convertResult = request.RawRequestBody.ConvertCollectionToIIIF<Collection>(logger);
        // if (convertResult.Error) return ErrorHelper.CannotValidateIIIF<Collection>();
        // var collectionFromBody = convertResult.ConvertedIIIF!;
        //
        // var splitSlug = request.Slug.Split('/');
        //
        // var parentSlug = string.Join("/", splitSlug.Take(..^1));
        // var parentCollection =
        //     await dbContext.RetrieveHierarchy(request.CustomerId, parentSlug, cancellationToken);
        //
        // var parentValidationError =
        //     ParentValidator.ValidateParentCollection<Collection>(parentCollection?.Collection);
        // if (parentValidationError != null) return parentValidationError;
        //
        // var id = await GenerateUniqueId(request, cancellationToken);
        // if (id == null) return ErrorHelper.CannotGenerateUniqueId<Collection>();
        //
        // var collection = CreateDatabaseCollection(request, collectionFromBody, id, parentCollection, splitSlug);
        // dbContext.Collections.Add(collection);
        //
        // var saveErrors =
        //     await dbContext.TrySaveCollection<Collection>(request.CustomerId, logger,
        //         cancellationToken);
        //
        // if (saveErrors != null)
        // {
        //     return saveErrors;
        // }
        //
        // await iiifS3.SaveIIIFToS3(collectionFromBody, collection, pathGenerator.GenerateFlatCollectionId(collection),
        //     false, cancellationToken);
        //
        // var hierarchy = collection.Hierarchy.GetCanonical();
        //
        // if (hierarchy.Parent != null)
        // {
        //     hierarchy.FullPath =
        //         await CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext, cancellationToken);
        // }
        //
        // collectionFromBody.Id = pathGenerator.GenerateHierarchicalId(hierarchy);
        // return ModifyEntityResult<Collection, ModifyCollectionType>.Success(collectionFromBody, WriteResult.Created, collection.Etag);
    }
}
