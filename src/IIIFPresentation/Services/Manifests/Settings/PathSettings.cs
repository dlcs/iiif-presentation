using Core.Web;

namespace Services.Manifests.Settings;

public class PathSettings
{
    public const string SettingsName = "PathSettings";
    
    public required Uri PresentationApiUrl { get; set; }

    /// <summary>
    /// Optional legacy presentation host (e.g. "presentation-api.*"). Used, in place of <see cref="PresentationApiUrl"/>,
    /// for resources created before <see cref="LegacyHostnameCutoffDate"/> - this avoids id breaks on existing
    /// resources when moving to a new default hostname. Not required - a deployment that has never had a legacy
    /// hostname can leave this unset.
    /// </summary>
    public Uri? LegacyPresentationApiUrl { get; set; }

    /// <summary>
    /// Cut-off date used alongside <see cref="LegacyPresentationApiUrl"/> - resources created before this date use
    /// the legacy hostname when generating ids, resources created on or after it use <see cref="PresentationApiUrl"/>.
    /// </summary>
    public DateTime? LegacyHostnameCutoffDate { get; set; }

    public Dictionary<int, Uri> CustomerPresentationApiUrl { get; set; } = new();

    /// <summary>
    /// Get the presentation host to use when generating an id for the specified customer/resource.
    /// </summary>
    /// <param name="customerId">CustomerId to get settings for.</param>
    /// <param name="created">
    /// Created date of the resource the id is being generated for, if known - used to decide between
    /// <see cref="LegacyPresentationApiUrl"/> and <see cref="PresentationApiUrl"/> when there's no customer
    /// specific override.
    /// </param>
    /// <returns>
    /// Customer specific override if set; else <see cref="LegacyPresentationApiUrl"/> if set and
    /// <paramref name="created"/> predates <see cref="LegacyHostnameCutoffDate"/>; else <see cref="PresentationApiUrl"/>.
    /// </returns>
    public Uri GetPresentationUrl(int customerId, DateTime? created = null)
    {
        if (CustomerPresentationApiUrl.TryGetValue(customerId, out var customerUrl)) return customerUrl;

        if (LegacyPresentationApiUrl != null && LegacyHostnameCutoffDate.HasValue &&
            created is not null && created < LegacyHostnameCutoffDate)
        {
            return LegacyPresentationApiUrl;
        }

        return PresentationApiUrl;
    }

    /// <summary>
    /// Whether the host is a customer specific host, the legacy presentation host, or the standard/default
    /// presentation host URL
    /// </summary>
    public bool IsCustomerRecognisedHost(int customerId, string host) =>
        CustomerPresentationApiUrl.GetValueOrDefault(customerId)?.Host == host ||
        (LegacyPresentationApiUrl != null && LegacyPresentationApiUrl.Host == host) ||
        PresentationApiUrl.Host == host;

    public TypedPathTemplateOptions PathRules { get; set; } = new ();
}
