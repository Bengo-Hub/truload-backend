using FluentValidation;
using TruLoad.Backend.DTOs.Weighing;

namespace TruLoad.Backend.Validators.Weighing;

/// <summary>
/// Validator for updating axle configurations
/// Enforces business rules: GVW ranges, framework validation, field constraints
/// </summary>
public class UpdateAxleConfigurationValidator : AbstractValidator<UpdateAxleConfigurationDto>
{
    public UpdateAxleConfigurationValidator()
    {
        RuleFor(x => x.AxleName)
            .NotEmpty().WithMessage("Axle name is required")
            .Length(1, 100).WithMessage("Axle name must be between 1 and 100 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters");

        RuleFor(x => x.WeightReferences)
            .Must(refs => refs == null || refs.All(r => r.AxleLegalWeightKg > 0))
            .WithMessage("All weight reference weights must be greater than 0");

        RuleFor(x => x.LegalFramework)
            .Must(x => x == null || new[] { "EAC", "TRAFFIC_ACT", "BOTH" }.Contains(x))
            .WithMessage("Legal framework must be 'EAC', 'TRAFFIC_ACT', or 'BOTH'");

        RuleFor(x => x.VisualDiagramUrl)
            .Must(x => x == null || Uri.TryCreate(x, UriKind.Absolute, out _))
            .WithMessage("Visual diagram URL must be a valid URI");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters");
    }
}
