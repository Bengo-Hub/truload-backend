using FluentValidation;
using TruLoad.Backend.DTOs.Weighing;

namespace TruLoad.Backend.Validators.Weighing;

/// <summary>
/// Validator for creating axle weight references
/// Enforces business rules: position range, weight bounds, grouping format
/// </summary>
public class CreateAxleWeightReferenceValidator : AbstractValidator<CreateAxleWeightReferenceDto>
{
    public CreateAxleWeightReferenceValidator()
    {
        RuleFor(x => x.AxleConfigurationId)
            .NotEmpty().WithMessage("Axle configuration ID is required");

        RuleFor(x => x.AxlePosition)
            .GreaterThanOrEqualTo(1).WithMessage("Axle position must be at least 1")
            .LessThanOrEqualTo(8).WithMessage("Axle position cannot exceed 8");

        RuleFor(x => x.AxleLegalWeightKg)
            .GreaterThan(0).WithMessage("Axle legal weight must be greater than 0 kg")
            .LessThanOrEqualTo(15000).WithMessage("Axle legal weight cannot exceed 15,000 kg");

        RuleFor(x => x.AxleGrouping)
            .NotEmpty().WithMessage("Axle grouping is required")
            .Must(x => new[] { "A", "B", "C", "D" }.Contains(x))
            .WithMessage("Axle grouping must be 'A', 'B', 'C', or 'D'");

        RuleFor(x => x.AxleGroupId)
            .NotEmpty().WithMessage("Axle group is required");

        RuleFor(x => x.TyreTypeId)
            .NotEmpty().When(x => x.TyreTypeId.HasValue)
            .WithMessage("Tyre type ID must be a valid GUID");
    }
}

/// <summary>
/// Validator for updating axle weight references
/// </summary>
public class UpdateAxleWeightReferenceValidator : AbstractValidator<UpdateAxleWeightReferenceDto>
{
    public UpdateAxleWeightReferenceValidator()
    {
        RuleFor(x => x.AxlePosition)
            .GreaterThanOrEqualTo(1).WithMessage("Axle position must be at least 1")
            .LessThanOrEqualTo(8).WithMessage("Axle position cannot exceed 8");

        RuleFor(x => x.AxleLegalWeightKg)
            .GreaterThan(0).WithMessage("Axle legal weight must be greater than 0 kg")
            .LessThanOrEqualTo(15000).WithMessage("Axle legal weight cannot exceed 15,000 kg");

        RuleFor(x => x.AxleGrouping)
            .NotEmpty().WithMessage("Axle grouping is required")
            .Must(x => new[] { "A", "B", "C", "D" }.Contains(x))
            .WithMessage("Axle grouping must be 'A', 'B', 'C', or 'D'");

        RuleFor(x => x.AxleGroupId)
            .NotEmpty().WithMessage("Axle group is required");

        RuleFor(x => x.TyreTypeId)
            .NotEmpty().When(x => x.TyreTypeId.HasValue)
            .WithMessage("Tyre type ID must be a valid GUID");
    }
}
