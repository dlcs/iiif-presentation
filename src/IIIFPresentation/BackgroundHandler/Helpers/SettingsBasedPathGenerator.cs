using DLCS;
using Microsoft.Extensions.Options;
using Repository.Paths;

namespace BackgroundHandler.Helpers;

/// <summary>
/// Implementation of <see cref="PathGeneratorBase"/> using settings for base urls
/// </summary>
public class SettingsBasedPathGenerator(
    IOptions<DlcsSettings> dlcsOptions, IPresentationPathGenerator presentationPathGenerator) 
    : PathGeneratorBase(presentationPathGenerator)
{
    protected override Uri DlcsApiUrl { get; } = dlcsOptions.Value.ApiUri;
}
