using Core;
using Models.API.General;

namespace API.Features.Common.Helpers;

public class DeleteErrorHelper
{
    public static ResultMessage<DeleteResult, DeleteResourceErrorType> EtagNotMatching()
        => new(DeleteResult.PreconditionFailed, DeleteResourceErrorType.EtagNotMatching,
            "Etag does not match");
    
    public static ResultMessage<DeleteResult, DeleteResourceErrorType> NotFound()
        => new(DeleteResult.NotFound, DeleteResourceErrorType.NotFound);

    public static ResultMessage<DeleteResult, DeleteResourceErrorType> CannotDeleteRootCollection()
        => new(DeleteResult.BadRequest, DeleteResourceErrorType.CannotDeleteRootCollection,
            "Cannot delete a root collection");
    
    public static ResultMessage<DeleteResult, DeleteResourceErrorType> UnknownError(string resourceType)
        => new(DeleteResult.BadRequest, DeleteResourceErrorType.Unknown,
            $"Error deleting {resourceType}");
}
