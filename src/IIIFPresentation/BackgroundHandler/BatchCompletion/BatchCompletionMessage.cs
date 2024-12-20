namespace BackgroundHandler.BatchCompletion;

public record BatchCompletionMessage(
    int Id,
    int CustomerId,
    int Total,
    int Success,
    int Errors,
    bool Superseded,
    DateTime Started,
    DateTime Finished);
