using FluentValidation;
using TruLoad.Backend.DTOs.Shift;

namespace TruLoad.Backend.Validators;

public class CreateWorkShiftRequestValidator : AbstractValidator<CreateWorkShiftRequest>
{
    public CreateWorkShiftRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Work shift name is required")
            .MaximumLength(100)
            .WithMessage("Name must not exceed 100 characters");

        RuleFor(x => x.TotalHoursPerWeek)
            .GreaterThan(0)
            .WithMessage("Total hours per week must be greater than 0")
            .LessThanOrEqualTo(168)
            .WithMessage("Total hours per week cannot exceed 168 (7 days * 24 hours)");

        RuleFor(x => x.GraceMinutes)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Grace minutes cannot be negative")
            .LessThanOrEqualTo(60)
            .WithMessage("Grace minutes cannot exceed 60");

        RuleFor(x => x.Schedules)
            .NotEmpty()
            .WithMessage("At least one schedule is required")
            .Must(schedules => schedules.Count <= 7)
            .WithMessage("Cannot have more than 7 schedules (one per day)");

        RuleForEach(x => x.Schedules)
            .SetValidator(new CreateWorkShiftScheduleRequestValidator());

        RuleFor(x => x.Schedules)
            .Must(HaveUniqueDays)
            .WithMessage("Each day can only appear once in schedules");
    }

    private bool HaveUniqueDays(List<CreateWorkShiftScheduleRequest> schedules)
    {
        var days = schedules.Select(s => s.Day.ToLower()).ToList();
        return days.Count == days.Distinct().Count();
    }
}

public class CreateWorkShiftScheduleRequestValidator : AbstractValidator<CreateWorkShiftScheduleRequest>
{
    private static readonly string[] ValidDays = { "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday" };

    public CreateWorkShiftScheduleRequestValidator()
    {
        RuleFor(x => x.Day)
            .NotEmpty()
            .WithMessage("Day is required")
            .Must(day => ValidDays.Contains(day.ToLower()))
            .WithMessage("Day must be a valid day of the week (Monday-Sunday)");

        RuleFor(x => x.StartTime)
            .Must(time => time >= TimeSpan.Zero && time < TimeSpan.FromHours(24))
            .WithMessage("Start time must be between 00:00 and 23:59");

        RuleFor(x => x.EndTime)
            .Must(time => time >= TimeSpan.Zero && time <= TimeSpan.FromHours(24))
            .WithMessage("End time must be between 00:00 and 24:00");

        RuleFor(x => x)
            .Must(schedule => schedule.EndTime > schedule.StartTime || 
                             (schedule.EndTime == TimeSpan.FromHours(24) && schedule.StartTime == TimeSpan.Zero))
            .WithMessage("End time must be after start time");

        RuleFor(x => x.BreakHours)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Break hours cannot be negative")
            .LessThan(24)
            .WithMessage("Break hours must be less than 24");
    }
}

public class UpdateWorkShiftRequestValidator : AbstractValidator<UpdateWorkShiftRequest>
{
    public UpdateWorkShiftRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(100)
            .WithMessage("Name must not exceed 100 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Name));

        RuleFor(x => x.TotalHoursPerWeek)
            .GreaterThan(0)
            .WithMessage("Total hours per week must be greater than 0")
            .LessThanOrEqualTo(168)
            .WithMessage("Total hours per week cannot exceed 168")
            .When(x => x.TotalHoursPerWeek.HasValue);

        RuleFor(x => x.GraceMinutes)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Grace minutes cannot be negative")
            .LessThanOrEqualTo(60)
            .WithMessage("Grace minutes cannot exceed 60")
            .When(x => x.GraceMinutes.HasValue);
    }
}
