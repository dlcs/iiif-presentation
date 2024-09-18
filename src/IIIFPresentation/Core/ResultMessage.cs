namespace Core;

public class ResultMessage<T, TEnum>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResultStatus{T}" /> class.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="type">A type used to generate an error</param>
    /// <param name="message">A message related to the result</param>
    public ResultMessage(T value, TEnum? type, string? message = null)
    {
        Value = value;
        Message = message;
        Type = type;
    }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ResultStatus{T}" /> class.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="message">A message related to the result</param>
    public ResultMessage(T value, string? message = null)
    {
        Value = value;
        Message = message;
    }

    /// <summary>
    /// The associated value.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// The message related to the result
    /// </summary>
    public string? Message { get; }
    
    /// <summary>
    /// A code associated with the result message
    /// </summary>
    public TEnum? Type { get; }
}