using FluentValidation;
using TruLoad.Backend.DTOs.User;

namespace TruLoad.Backend.Validators;

public class CreateOrganizationRequestValidator : AbstractValidator<CreateOrganizationRequest>
{
    public CreateOrganizationRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Organization code is required")
            .MaximumLength(50)
            .WithMessage("Code must not exceed 50 characters")
            .Matches("^[A-Z0-9_-]+$")
            .WithMessage("Code must contain only uppercase letters, numbers, hyphens, and underscores");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Organization name is required")
            .MaximumLength(255)
            .WithMessage("Name must not exceed 255 characters");

        RuleFor(x => x.OrgType)
            .Must(type => type == null || new[] { "government", "private" }.Contains(type.ToLowerInvariant()))
            .WithMessage("Organization type must be 'government' or 'private'")
            .When(x => !string.IsNullOrWhiteSpace(x.OrgType));

        RuleFor(x => x.ContactEmail)
            .EmailAddress()
            .WithMessage("Invalid email format")
            .MaximumLength(255)
            .WithMessage("Email must not exceed 255 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));

        RuleFor(x => x.ContactPhone)
            .MaximumLength(20)
            .WithMessage("Phone must not exceed 20 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactPhone));
    }
}

public class UpdateOrganizationRequestValidator : AbstractValidator<UpdateOrganizationRequest>
{
    public UpdateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(255)
            .WithMessage("Name must not exceed 255 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Name));

        RuleFor(x => x.OrgType)
            .Must(type => type == null || new[] { "government", "private" }.Contains(type.ToLowerInvariant()))
            .WithMessage("Organization type must be 'government' or 'private'")
            .When(x => !string.IsNullOrWhiteSpace(x.OrgType));

        RuleFor(x => x.ContactEmail)
            .EmailAddress()
            .WithMessage("Invalid email format")
            .MaximumLength(255)
            .WithMessage("Email must not exceed 255 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));

        RuleFor(x => x.ContactPhone)
            .MaximumLength(20)
            .WithMessage("Phone must not exceed 20 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactPhone));
    }
}
