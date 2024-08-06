﻿namespace Core;

public class ResultMessage<T>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ResultStatus{T}" /> class.
    /// </summary>
    /// <param name="message">A message related to the result</param>
    /// <param name="value">The value.</param>
    public ResultMessage(string message, T value)
    {
        Value = value;
        Message = message;
    }

    /// <summary>
    ///     The associated value.
    /// </summary>
    public T Value { get; }

    /// <summary>
    ///     The message related to the result
    /// </summary>
    public string Message { get; }
}