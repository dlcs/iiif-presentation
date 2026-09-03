using Microsoft.Extensions.Options;
using Repository.Paths;
using Services.Manifests.Helpers;
using Services.Manifests.Settings;

namespace Services.Tests.Manifests.Helpers;

public class SettingsDrivenPresentationConfigGeneratorTests
{
    private static readonly DateTime CutoffDate = new(2026, 1, 1);

    private static SettingsDrivenPresentationConfigGenerator GetSut(bool withLegacy = true, bool withCustomer = true) =>
        new(Options.Create(new PathSettings
        {
            PresentationApiUrl = new Uri("https://iiif.example.com"),
            LegacyPresentationApiUrl = withLegacy ? new Uri("https://presentation-api.example.com") : null,
            LegacyHostnameCutoffDate = withLegacy ? CutoffDate : null,
            CustomerPresentationApiUrl = withCustomer
                ? new Dictionary<int, Uri> { [1] = new Uri("https://customer.example.com") }
                : new Dictionary<int, Uri>()
        }));

    [Fact]
    public void GetFlatPresentationPathForRequest_UsesLegacyHost_IfCreatedBeforeCutoff()
    {
        var sut = GetSut();

        var path = sut.GetFlatPresentationPathForRequest(PresentationResourceType.ManifestPrivate, 999, "foo",
            CutoffDate.AddDays(-1));

        path.Should().Be("https://presentation-api.example.com/999/manifests/foo");
    }

    [Fact]
    public void GetFlatPresentationPathForRequest_UsesDefaultHost_IfCreatedOnOrAfterCutoff()
    {
        var sut = GetSut();

        var path = sut.GetFlatPresentationPathForRequest(PresentationResourceType.ManifestPrivate, 999, "foo",
            CutoffDate);

        path.Should().Be("https://iiif.example.com/999/manifests/foo");
    }

    [Fact]
    public void GetFlatPresentationPathForRequest_UsesDefaultHost_IfCreatedNotProvided()
    {
        var sut = GetSut();

        var path = sut.GetFlatPresentationPathForRequest(PresentationResourceType.ManifestPrivate, 999, "foo");

        path.Should().Be("https://iiif.example.com/999/manifests/foo");
    }

    [Fact]
    public void GetFlatPresentationPathForRequest_UsesCustomerHost_RegardlessOfCreatedDate()
    {
        var sut = GetSut();

        var path = sut.GetFlatPresentationPathForRequest(PresentationResourceType.ManifestPrivate, 1, "foo",
            CutoffDate.AddDays(-1));

        path.Should().Be("https://customer.example.com/1/manifests/foo");
    }

    [Fact]
    public void GetHierarchyPresentationPathForRequest_UsesLegacyHost_IfCreatedBeforeCutoff()
    {
        var sut = GetSut();

        var path = sut.GetHierarchyPresentationPathForRequest(PresentationResourceType.ResourcePublic, 999,
            "some/path", CutoffDate.AddYears(-1));

        path.Should().Be("https://presentation-api.example.com/999/some/path");
    }

    [Fact]
    public void GetFlatPresentationPathForRequest_UsesDefaultHost_IfLegacyNotConfigured()
    {
        var sut = GetSut(withLegacy: false);

        var path = sut.GetFlatPresentationPathForRequest(PresentationResourceType.ManifestPrivate, 999, "foo",
            CutoffDate.AddYears(-10));

        path.Should().Be("https://iiif.example.com/999/manifests/foo");
    }
}
