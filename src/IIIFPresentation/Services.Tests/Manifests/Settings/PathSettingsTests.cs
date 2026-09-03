using Services.Manifests.Settings;

namespace Services.Tests.Manifests.Settings;

public class PathSettingsTests
{
    private static readonly Uri DefaultUrl = new("https://iiif.example.com");
    private static readonly Uri LegacyUrl = new("https://presentation-api.example.com");
    private static readonly Uri CustomerUrl = new("https://customer.example.com");
    private static readonly DateTime CutoffDate = new(2026, 1, 1);

    private static PathSettings GetSettings(bool withLegacy = true, bool withCustomer = true) =>
        new()
        {
            PresentationApiUrl = DefaultUrl,
            LegacyPresentationApiUrl = withLegacy ? LegacyUrl : null,
            LegacyHostnameCutoffDate = withLegacy ? CutoffDate : null,
            CustomerPresentationApiUrl = withCustomer
                ? new Dictionary<int, Uri> { [1] = CustomerUrl }
                : new Dictionary<int, Uri>()
        };

    [Fact]
    public void GetPresentationUrl_ReturnsCustomerUrl_IfSet_RegardlessOfCreatedDate()
    {
        var settings = GetSettings();

        settings.GetPresentationUrl(1, CutoffDate.AddYears(-1)).Should().Be(CustomerUrl);
        settings.GetPresentationUrl(1, CutoffDate.AddYears(1)).Should().Be(CustomerUrl);
        settings.GetPresentationUrl(1).Should().Be(CustomerUrl);
    }

    [Fact]
    public void GetPresentationUrl_ReturnsLegacyUrl_IfNoCustomerUrl_AndCreatedBeforeCutoff()
    {
        var settings = GetSettings();

        var result = settings.GetPresentationUrl(999, CutoffDate.AddDays(-1));

        result.Should().Be(LegacyUrl);
    }

    [Fact]
    public void GetPresentationUrl_ReturnsDefaultUrl_IfNoCustomerUrl_AndCreatedOnOrAfterCutoff()
    {
        var settings = GetSettings();

        settings.GetPresentationUrl(999, CutoffDate).Should().Be(DefaultUrl);
        settings.GetPresentationUrl(999, CutoffDate.AddDays(1)).Should().Be(DefaultUrl);
    }

    [Fact]
    public void GetPresentationUrl_ReturnsDefaultUrl_IfCreatedNotProvided()
    {
        var settings = GetSettings();

        var result = settings.GetPresentationUrl(999);

        result.Should().Be(DefaultUrl);
    }

    [Fact]
    public void GetPresentationUrl_ReturnsDefaultUrl_IfLegacyUrlNotConfigured_EvenIfCreatedBeforeCutoff()
    {
        var settings = GetSettings(withLegacy: false);

        var result = settings.GetPresentationUrl(999, CutoffDate.AddYears(-10));

        result.Should().Be(DefaultUrl);
    }

    [Theory]
    [InlineData(1, "customer.example.com", true)]
    [InlineData(1, "presentation-api.example.com", true)]
    [InlineData(1, "iiif.example.com", true)]
    [InlineData(1, "unknown.example.com", false)]
    [InlineData(999, "customer.example.com", false)]
    [InlineData(999, "presentation-api.example.com", true)]
    [InlineData(999, "iiif.example.com", true)]
    public void IsCustomerRecognisedHost_RecognisesCustomerLegacyAndDefaultHosts(int customerId, string host,
        bool expected)
    {
        var settings = GetSettings();

        var result = settings.IsCustomerRecognisedHost(customerId, host);

        result.Should().Be(expected);
    }

    [Fact]
    public void IsCustomerRecognisedHost_DoesNotRecogniseLegacyHost_IfNotConfigured()
    {
        var settings = GetSettings(withLegacy: false);

        var result = settings.IsCustomerRecognisedHost(999, "presentation-api.example.com");

        result.Should().BeFalse();
    }
}
