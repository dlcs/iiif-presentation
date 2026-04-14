namespace Core.Exceptions;

public class CanvasPaintingValidationException(IEnumerable<(string canvasId, string reason)> errors)
    : PresentationException("Errors found when trying to parse into Canvas Paintings")
{
    public (string canvasId, string reason)[] Errors { get; } = errors.ToArray();
}
