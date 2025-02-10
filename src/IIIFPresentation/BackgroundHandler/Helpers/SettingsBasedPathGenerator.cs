using BackgroundHandler.Settings;
using DLCS;
using Microsoft.Extensions.Options;
using Repository.Paths;

namespace BackgroundHandler.Helpers;

/// <summary>
/// Implementation of <see cref="PathGeneratorBase"/> using settings for base urls
/// </summary>
public class SettingsBasedPathGenerator(
    IOptions<DlcsSettings> dlcsOptions,
    IOptions<BackgroundHandlerSettings> backgroundHandlerOptions) : PathGeneratorBase
{
    protected override string PresentationUrl { get; } = backgroundHandlerOptions.Value.PresentationApiUrl;
    protected override Uri DlcsApiUrl { get; } = dlcsOptions.Value.ApiUri;
}
