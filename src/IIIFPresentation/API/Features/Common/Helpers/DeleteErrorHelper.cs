using Core;
using Models.API.General;

namespace API.Features.Common.Helpers;

public class DeleteErrorHelper
{
    public static ResultMessage<DeleteResult, DeleteResourceType> EtagNotMatching()
        => new(DeleteResult.PreConditionFailed, DeleteResourceType.EtagNotMatching,
            "Etag does not match");
}
