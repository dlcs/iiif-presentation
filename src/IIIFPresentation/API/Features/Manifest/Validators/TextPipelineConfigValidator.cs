using FluentValidation;
using Models.API.Manifest;
using Services.TextServices;

namespace API.Features.Manifest.Validators;

/// <summary>
/// Validates the <see cref="PipelineConfig"/> of a "text" pipeline, including the settings read via
/// <see cref="PipelineHelper.AsTextConfig"/>
/// </summary>
public class TextPipelineConfigValidator : AbstractValidator<PipelineConfig>
{
    public TextPipelineConfigValidator()
    {
        RuleFor(c => c.Settings)
            .Must(s => s!.Keys.All(IsKnownSetting))
            .When(c => c.Settings != null)
            .WithMessage(c =>
                $"'config' contains unknown properties: {string.Join(", ", c.Settings!.Keys.Where(k => !IsKnownSetting(k)))}. Allowed properties are: action, {string.Join(", ", TextPipelineConfig.SettingNames)}");

        RuleFor(c => c)
            .Must(c => c.TryGetTextConfig(out _))
            .WithMessage("'output' must be a list of strings and 'pdfCoverPage' must be a string")
            .DependentRules(SettingsValidation);
    }

    // Only run once the settings are known to be readable
    private void SettingsValidation()
    {
        RuleFor(c => c.AsTextConfig().Output)
            .Must(o => o!.Count > 0)
            .When(c => c.AsTextConfig().Output != null)
            .OverridePropertyName("output")
            .WithMessage("'output' must contain at least one value when provided");

        RuleFor(c => c.AsTextConfig().Output)
            .Must(o => o!.All(TextPipelineOutputs.IsValid))
            .When(c => c.AsTextConfig().Output != null)
            .OverridePropertyName("output")
            .WithMessage(
                $"'output' contains an unknown value. Allowed values are: {string.Join(", ", TextPipelineOutputs.AllowedValues)}");

        // TextAugmented is always requested, so on its own it would add nothing to the Manifest
        RuleFor(c => c.AsTextConfig().Output)
            .Must(o => !o!.All(v =>
                string.Equals(v, nameof(JobServices.TextAugmented), StringComparison.OrdinalIgnoreCase)))
            .When(c => c.AsTextConfig().Output?.Count > 0)
            .OverridePropertyName("output")
            .WithMessage($"'output' cannot contain only '{nameof(JobServices.TextAugmented)}'");

        RuleFor(c => c.AsTextConfig().PdfCoverPage)
            .Must(p => Uri.TryCreate(p, UriKind.Absolute, out var uri) &&
                       (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .When(c => c.AsTextConfig().PdfCoverPage != null)
            .OverridePropertyName("pdfCoverPage")
            .WithMessage("'pdfCoverPage' must be an absolute http(s) URI");
    }

    private static bool IsKnownSetting(string name) =>
        TextPipelineConfig.SettingNames.Contains(name, StringComparer.OrdinalIgnoreCase);
}
