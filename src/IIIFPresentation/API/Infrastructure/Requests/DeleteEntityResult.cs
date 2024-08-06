using Core;

namespace API.Infrastructure.Requests;

/// <summary>
///     Represents the result of a request to delete an entity
/// </summary>
public class DeleteEntityResult : IModifyRequest
{
    /// <summary>
    ///     The associated value.
    /// </summary>
    public DeleteResult Value { get; private init; }

    /// <summary>
    ///     The message related to the result
    /// </summary>
    public string? Message { get; private init; }

    public static DeleteEntityResult Success => new() { Value = DeleteResult.Deleted };

    public bool IsSuccess => Value == DeleteResult.Deleted;

    public static DeleteEntityResult Failure(string message, DeleteResult result)
    {
        return new DeleteEntityResult { Message = message, Value = result };
    }
}