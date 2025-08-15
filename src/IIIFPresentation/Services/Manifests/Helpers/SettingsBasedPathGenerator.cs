using DLCS;
using Microsoft.Extensions.Options;
using Repository.Paths;

namespace Services.Manifests.Helpers;

/// <summary>
/// Implementation of <see cref="PathGeneratorBase"/> using settings for base urls
/// </summary>
public class SettingsBasedPathGenerator(
    IOptions<DlcsSettings> dlcsOptions, SettingsDrivenPresentationConfigGenerator presentationPathGenerator) 
    : PathGeneratorBase(presentationPathGenerator)
{
    protected override Uri DlcsApiUrl { get; } = dlcsOptions.Value.ApiUri;
}
