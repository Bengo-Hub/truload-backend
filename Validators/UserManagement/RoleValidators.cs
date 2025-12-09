using FluentValidation;
using TruLoad.Backend.DTOs.User;

namespace TruLoad.Backend.Validators;

public class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Role name is required")
            .MaximumLength(100)
            .WithMessage("Name must not exceed 100 characters")
            .Matches("^[A-Za-z0-9_ ]+$")
            .WithMessage("Name must contain only letters, numbers, spaces, and underscores");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage("Description must not exceed 500 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.Permissions)
            .Must(BeValidJson)
            .WithMessage("Permissions must be valid JSON")
            .When(x => !string.IsNullOrWhiteSpace(x.Permissions));
    }

    private bool BeValidJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return true;

        try
        {
            System.Text.Json.JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage("Description must not exceed 500 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.Permissions)
            .Must(BeValidJson)
            .WithMessage("Permissions must be valid JSON")
            .When(x => !string.IsNullOrWhiteSpace(x.Permissions));
    }

    private bool BeValidJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return true;

        try
        {
            System.Text.Json.JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
