namespace Services.TextServices;

/// <summary>
/// Identifier for a text-services pipeline job, in the format "{customerId}/iiif/{resourceId}".
/// </summary>
public class TextJobId
{
    private const string Separator = "iiif";

    /// <summary>Id of customer</summary>
    public int CustomerId { get; }

    /// <summary>Id of the related Manifest or Collection resource</summary>
    public string ResourceId { get; }

    /// <summary>
    /// Identifier for a text-services pipeline job, in the format "{customerId}/iiif/{resourceId}".
    /// </summary>
    public TextJobId(int customerId, string resourceId)
    {
        CustomerId = customerId;
        ResourceId = resourceId;
    }

    public override string ToString() => $"{CustomerId}/{Separator}/{ResourceId}";

    /// <summary>
    /// Create a new <see cref="TextJobId"/> from a string in the format "{customerId}/iiif/{resourceId}".
    /// </summary>
    /// <exception cref="TextJobIdException">Thrown when <paramref name="jobId"/> is not in the expected format.</exception>
    public static TextJobId FromString(string jobId)
    {
        if (!TryParseInternal(jobId, out var result))
        {
            throw new TextJobIdException(
                $"TextJobId '{jobId}' is invalid. Must be in format customerId/{Separator}/resourceId");
        }

        return result!;
    }

    /// <summary>
    /// Attempt to parse a string in the format "{customerId}/iiif/{resourceId}" into a <see cref="TextJobId"/>.
    /// </summary>
    /// <returns><c>true</c> if parsing succeeded; otherwise <c>false</c>.</returns>
    public static bool TryParse(string? jobId, out TextJobId? result) => TryParseInternal(jobId, out result);

    private static bool TryParseInternal(string? jobId, out TextJobId? result)
    {
        result = null;
        if (string.IsNullOrEmpty(jobId)) return false;

        var parts = jobId.Split("/", StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], out var customerId)) return false;
        if (parts[1] != Separator) return false;

        result = new TextJobId(customerId, parts[2]);
        return true;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        var jobId = (TextJobId)obj;
        return jobId.ToString() == this.ToString();
    }

    public static bool operator ==(TextJobId? jobId1, TextJobId? jobId2)
    {
        if (jobId1 is null)
        {
            return jobId2 is null;
        }

        if (jobId2 is null)
        {
            return false;
        }

        return jobId1.Equals(jobId2);
    }

    public static bool operator !=(TextJobId? jobId1, TextJobId? jobId2)
        => !(jobId1 == jobId2);

    public override int GetHashCode() => HashCode.Combine(CustomerId, ResourceId);
}
