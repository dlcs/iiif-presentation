using Core;
using Models.API.General;

namespace API.Features.Common.Helpers;

public class DeleteErrorHelper
{
    public static ResultMessage<DeleteResult, DeleteResourceErrorType> EtagNotMatching()
        => new(DeleteResult.PreconditionFailed, DeleteResourceErrorType.EtagNotMatching,
            "Etag does not match");
}
