using FluentValidation;
using Models.API.Manifest.Pipelines;
using Services.TextServices;

namespace API.Features.Manifest.Validators;

/// <summary>
/// Validates the <see cref="TextPipelineConfig"/> of a "text" pipeline
/// </summary>
public class TextPipelineConfigValidator : AbstractValidator<TextPipelineConfig>
{
    public TextPipelineConfigValidator()
    {
        RuleFor(c => c.Output)
            .Must(o => o!.Count > 0)
            .When(c => c.Output != null)
            .OverridePropertyName("output")
            .WithMessage("'output' must contain at least one value when provided");

        RuleFor(c => c.Output)
            .Must(o => o!.All(TextPipelineOutputs.IsValid))
            .When(c => c.Output != null)
            .OverridePropertyName("output")
            .WithMessage(
                $"'output' contains an unknown value. Allowed values are: {string.Join(", ", TextPipelineOutputs.AllowedValues)}");

        // TextAugmented is always requested, so on its own it would add nothing to the Manifest
        RuleFor(c => c.Output)
            .Must(o => !o!.All(v =>
                string.Equals(v, nameof(JobServices.TextAugmented), StringComparison.OrdinalIgnoreCase)))
            .When(c => c.Output?.Count > 0)
            .OverridePropertyName("output")
            .WithMessage($"'output' cannot contain only '{nameof(JobServices.TextAugmented)}'");

        RuleFor(c => c.PdfCoverPage)
            .Must(p => Uri.TryCreate(p, UriKind.Absolute, out var uri) &&
                       (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .When(c => c.PdfCoverPage != null)
            .OverridePropertyName("pdfCoverPage")
            .WithMessage("'pdfCoverPage' must be an absolute http(s) URI");
    }
}
