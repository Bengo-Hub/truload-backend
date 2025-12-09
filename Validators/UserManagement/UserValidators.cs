using FluentValidation;
using TruLoad.Backend.DTOs.User;

namespace TruLoad.Backend.Validators;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.AuthServiceUserId)
            .NotEmpty()
            .WithMessage("Auth service user ID is required");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Invalid email format")
            .MaximumLength(255)
            .WithMessage("Email must not exceed 255 characters");

        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .WithMessage("Phone must not exceed 20 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.FullName)
            .MaximumLength(255)
            .WithMessage("Full name must not exceed 255 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.FullName));
    }
}

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .WithMessage("Phone must not exceed 20 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.FullName)
            .MaximumLength(255)
            .WithMessage("Full name must not exceed 255 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.FullName));

        RuleFor(x => x.Status)
            .Must(status => status == null || new[] { "active", "inactive", "suspended" }.Contains(status))
            .WithMessage("Status must be 'active', 'inactive', or 'suspended'")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));
    }
}
