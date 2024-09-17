namespace Core;

public class ResultMessage<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResultStatus{T}" /> class.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="message">A message related to the result</param>
    /// <param name="code">A code associated with the result</param>
    public ResultMessage(T value, string? message = null, int? code = null)
    {
        Value = value;
        Message = message;
        Code = code;
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
    public int? Code { get; }
}