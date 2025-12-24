using FluentValidation;
using TruLoad.Backend.DTOs.User;

namespace TruLoad.Backend.Validators;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Invalid email format")
            .MaximumLength(255)
            .WithMessage("Email must not exceed 255 characters");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(15)
            .WithMessage("Phone number must not exceed 15 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

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
        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20)
            .WithMessage("Phone number must not exceed 20 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

        RuleFor(x => x.FullName)
            .MaximumLength(255)
            .WithMessage("Full name must not exceed 255 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.FullName));
    }
}
