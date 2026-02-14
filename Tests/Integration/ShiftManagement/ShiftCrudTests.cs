using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Tests.Integration.Helpers;
using Xunit;
using FluentAssertions;

namespace TruLoad.Backend.Tests.Integration.ShiftManagement;

/// <summary>
/// Integration tests for shift management CRUD operations.
/// Tests WorkShift, WorkShiftSchedule, and UserShift entities
/// including creation, updates, deletion, listing, and user assignment.
/// </summary>
public class ShiftCrudTests : IAsyncLifetime
{
    private TruLoadDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _context = TestDbContextFactory.Create();
        await _context.Database.EnsureCreatedAsync();
        await TestDbContextFactory.SeedBaseData(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    #region Helpers

    /// <summary>
    /// Creates a WorkShift with default values for testing.
    /// </summary>
    private static WorkShift CreateTestShift(
        string name = "Morning Shift",
        string code = "MORNING",
        decimal totalHoursPerWeek = 40.00m,
        int graceMinutes = 15)
    {
        return new WorkShift
        {
            Id = Guid.NewGuid(),
            Name = name,
            Code = code,
            Description = $"{name} for testing purposes",
            TotalHoursPerWeek = totalHoursPerWeek,
            GraceMinutes = graceMinutes,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a WorkShiftSchedule for a specific day and time range.
    /// </summary>
    private static WorkShiftSchedule CreateTestSchedule(
        Guid workShiftId,
        string day,
        TimeSpan startTime,
        TimeSpan endTime,
        bool isWorkingDay = true)
    {
        return new WorkShiftSchedule
        {
            Id = Guid.NewGuid(),
            WorkShiftId = workShiftId,
            Day = day,
            StartTime = startTime,
            EndTime = endTime,
            StartTimeStr = startTime.ToString(@"hh\:mm"),
            EndTimeStr = endTime.ToString(@"hh\:mm"),
            BreakHours = 1.0m,
            IsWorkingDay = isWorkingDay,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion

    #region Create Shift

    [Fact]
    public async Task CreateShift_WithValidData_ShouldSucceed()
    {
        // Arrange
        var shift = CreateTestShift();

        // Act
        _context.WorkShifts.Add(shift);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.WorkShifts.FirstOrDefaultAsync(s => s.Id == shift.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Morning Shift");
        saved.Code.Should().Be("MORNING");
        saved.TotalHoursPerWeek.Should().Be(40.00m);
        saved.GraceMinutes.Should().Be(15);
        saved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateShift_WithSchedule_ShouldPersistScheduleDays()
    {
        // Arrange
        var shift = CreateTestShift();
        _context.WorkShifts.Add(shift);
        await _context.SaveChangesAsync();

        var weekdays = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };
        var schedules = weekdays.Select(day => CreateTestSchedule(
            shift.Id,
            day,
            new TimeSpan(8, 0, 0),
            new TimeSpan(17, 0, 0)
        )).ToList();

        // Act
        _context.WorkShiftSchedules.AddRange(schedules);
        await _context.SaveChangesAsync();

        // Assert
        var savedSchedules = await _context.WorkShiftSchedules
            .Where(s => s.WorkShiftId == shift.Id)
            .ToListAsync();

        savedSchedules.Should().HaveCount(5, "should have one schedule entry per weekday");
        savedSchedules.Should().AllSatisfy(s =>
        {
            s.StartTime.Should().Be(new TimeSpan(8, 0, 0));
            s.EndTime.Should().Be(new TimeSpan(17, 0, 0));
            s.IsWorkingDay.Should().BeTrue();
        });
    }

    [Fact]
    public async Task CreateShift_WithNightSchedule_ShouldHandleOvernightTimes()
    {
        // Arrange - night shift crossing midnight
        var shift = CreateTestShift("Night Shift", "NIGHT", 40.00m, 10);
        _context.WorkShifts.Add(shift);
        await _context.SaveChangesAsync();

        var schedule = CreateTestSchedule(
            shift.Id,
            "Monday",
            new TimeSpan(22, 0, 0),  // 10 PM
            new TimeSpan(6, 0, 0)    // 6 AM (next day)
        );

        // Act
        _context.WorkShiftSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.WorkShiftSchedules
            .FirstOrDefaultAsync(s => s.WorkShiftId == shift.Id && s.Day == "Monday");

        saved.Should().NotBeNull();
        saved!.StartTime.Should().Be(new TimeSpan(22, 0, 0));
        saved.EndTime.Should().Be(new TimeSpan(6, 0, 0));
        saved.StartTime.Should().BeGreaterThan(saved.EndTime,
            "night shifts can have start time after end time (crossing midnight)");
    }

    [Fact]
    public async Task CreateShift_DefaultValues_ShouldBeApplied()
    {
        // Arrange - create shift with minimal properties
        var shift = new WorkShift
        {
            Id = Guid.NewGuid(),
            Name = "Default Shift",
            Code = "DEFAULT"
        };

        // Act
        _context.WorkShifts.Add(shift);
        await _context.SaveChangesAsync();

        // Assert - verify default values
        var saved = await _context.WorkShifts.FirstOrDefaultAsync(s => s.Id == shift.Id);
        saved.Should().NotBeNull();
        saved!.TotalHoursPerWeek.Should().Be(40.00m, "default weekly hours should be 40");
        saved.GraceMinutes.Should().Be(0, "default grace minutes should be 0");
        saved.IsActive.Should().BeTrue("new shifts should default to active");
        saved.DeletedAt.Should().BeNull("new shifts should not be soft-deleted");
    }

    #endregion

    #region Update Shift

    [Fact]
    public async Task UpdateShift_ShouldModifyFields()
    {
        // Arrange
        var shift = CreateTestShift();
        _context.WorkShifts.Add(shift);
        await _context.SaveChangesAsync();

        // Act - update shift properties
        shift.Name = "Updated Morning Shift";
        shift.TotalHoursPerWeek = 37.50m;
        shift.GraceMinutes = 10;
        shift.UpdatedAt = DateTime.UtcNow;
        _context.WorkShifts.Update(shift);
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.WorkShifts.FirstOrDefaultAsync(s => s.Id == shift.Id);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated Morning Shift");
        updated.TotalHoursPerWeek.Should().Be(37.50m);
        updated.GraceMinutes.Should().Be(10);
    }

    [Fact]
    public async Task UpdateShift_Deactivate_ShouldSetIsActiveFalse()
    {
        // Arrange
        var shift = CreateTestShift();
        _context.WorkShifts.Add(shift);
        await _context.SaveChangesAsync();

        // Act - deactivate shift
        shift.IsActive = false;
        shift.UpdatedAt = DateTime.UtcNow;
        _context.WorkShifts.Update(shift);
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.WorkShifts.FirstOrDefaultAsync(s => s.Id == shift.Id);
        updated.Should().NotBeNull();
        updated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateShift_Schedule_ShouldModifyTimeRange()
    {
        // Arrange
        var shift = CreateTestShift();
        _context.WorkShifts.Add(shift);
        await _context.SaveChangesAsync();

        var schedule = CreateTestSchedule(shift.Id, "Monday", new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0));
        _context.WorkShiftSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act - change to earlier start time
        schedule.StartTime = new TimeSpan(7, 0, 0);
        schedule.StartTimeStr = "07:00";
        schedule.EndTime = new TimeSpan(16, 0, 0);
        schedule.EndTimeStr = "16:00";
        schedule.UpdatedAt = DateTime.UtcNow;
        _context.WorkShiftSchedules.Update(schedule);
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.WorkShiftSchedules
            .FirstOrDefaultAsync(s => s.Id == schedule.Id);
        updated.Should().NotBeNull();
        updated!.StartTime.Should().Be(new TimeSpan(7, 0, 0));
        updated.EndTime.Should().Be(new TimeSpan(16, 0, 0));
    }

    #endregion

    #region Delete Shift

    [Fact]
    public async Task DeleteShift_ShouldRemoveFromDatabase()
    {
        // Arrange
        var shift = CreateTestShift("Temporary Shift", "TEMP");
        _context.WorkShifts.Add(shift);
        await _context.SaveChangesAsync();

        // Verify it exists
        var exists = await _context.WorkShifts.AnyAsync(s => s.Id == shift.Id);
        exists.Should().BeTrue();

        // Act - hard delete
        _context.WorkShifts.Remove(shift);
        await _context.SaveChangesAsync();

        // Assert
        var deleted = await _context.WorkShifts.FirstOrDefaultAsync(s => s.Id == shift.Id);
        deleted.Should().BeNull("shift should be removed from database");
    }

    [Fact]
    public async Task DeleteShift_SoftDelete_ShouldSetDeletedAt()
    {
        // Arrange
        var shift = CreateTestShift("Soft Delete Shift", "SOFTDEL");
        _context.WorkShifts.Add(shift);
        await _context.SaveChangesAsync();

        // Act - soft delete
        shift.DeletedAt = DateTime.UtcNow;
        shift.IsActive = false;
        _context.WorkShifts.Update(shift);
        await _context.SaveChangesAsync();

        // Assert - shift still exists but is marked as deleted
        var softDeleted = await _context.WorkShifts.FirstOrDefaultAsync(s => s.Id == shift.Id);
        softDeleted.Should().NotBeNull();
        softDeleted!.DeletedAt.Should().NotBeNull();
        softDeleted.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteShift_CascadeDeletesSchedules()
    {
        // Arrange - shift with schedules
        var shift = CreateTestShift("Cascade Shift", "CASCADE");
        _context.WorkShifts.Add(shift);
        await _context.SaveChangesAsync();

        var schedules = new[]
        {
            CreateTestSchedule(shift.Id, "Monday", new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)),
            CreateTestSchedule(shift.Id, "Tuesday", new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)),
            CreateTestSchedule(shift.Id, "Wednesday", new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0))
        };

        _context.WorkShiftSchedules.AddRange(schedules);
        await _context.SaveChangesAsync();

        // Act - delete the parent shift
        _context.WorkShifts.Remove(shift);
        await _context.SaveChangesAsync();

        // Assert - cascaded schedules should also be removed
        var remainingSchedules = await _context.WorkShiftSchedules
            .Where(s => s.WorkShiftId == shift.Id)
            .CountAsync();

        remainingSchedules.Should().Be(0, "schedules should be cascade-deleted with the shift");
    }

    #endregion

    #region List Shifts

    [Fact]
    public async Task ListShifts_ShouldReturnAll()
    {
        // Arrange - create multiple shifts
        var shifts = new[]
        {
            CreateTestShift("Morning Shift", "MORNING", 40.00m, 15),
            CreateTestShift("Afternoon Shift", "AFTERNOON", 40.00m, 10),
            CreateTestShift("Night Shift", "NIGHT", 40.00m, 10)
        };

        _context.WorkShifts.AddRange(shifts);
        await _context.SaveChangesAsync();

        // Act
        var allShifts = await _context.WorkShifts.ToListAsync();

        // Assert
        allShifts.Should().HaveCount(3);
        allShifts.Select(s => s.Code).Should().BeEquivalentTo(new[] { "MORNING", "AFTERNOON", "NIGHT" });
    }

    [Fact]
    public async Task ListShifts_ActiveOnly_ShouldFilterInactive()
    {
        // Arrange
        var activeShift = CreateTestShift("Active Shift", "ACTIVE");
        var inactiveShift = CreateTestShift("Inactive Shift", "INACTIVE");
        inactiveShift.IsActive = false;

        _context.WorkShifts.AddRange(activeShift, inactiveShift);
        await _context.SaveChangesAsync();

        // Act
        var activeShifts = await _context.WorkShifts
            .Where(s => s.IsActive)
            .ToListAsync();

        // Assert
        activeShifts.Should().HaveCount(1);
        activeShifts[0].Code.Should().Be("ACTIVE");
    }

    [Fact]
    public async Task ListShifts_WithSchedules_ShouldEagerLoad()
    {
        // Arrange
        var shift = CreateTestShift();
        _context.WorkShifts.Add(shift);
        await _context.SaveChangesAsync();

        var schedules = new[] { "Monday", "Tuesday", "Wednesday" }
            .Select(day => CreateTestSchedule(shift.Id, day, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)))
            .ToArray();

        _context.WorkShiftSchedules.AddRange(schedules);
        await _context.SaveChangesAsync();

        // Act - eager load schedules
        var shiftWithSchedules = await _context.WorkShifts
            .Include(s => s.WorkShiftSchedules)
            .FirstOrDefaultAsync(s => s.Id == shift.Id);

        // Assert
        shiftWithSchedules.Should().NotBeNull();
        shiftWithSchedules!.WorkShiftSchedules.Should().HaveCount(3);
        shiftWithSchedules.WorkShiftSchedules.Select(s => s.Day)
            .Should().BeEquivalentTo(new[] { "Monday", "Tuesday", "Wednesday" });
    }

    #endregion

    #region Assign User to Shift

    [Fact]
    public async Task AssignUserToShift_ShouldCreateMapping()
    {
        // Arrange
        var shift = CreateTestShift();
        _context.WorkShifts.Add(shift);
        await _context.SaveChangesAsync();

        var user = await TestUserHelper.SeedTestUser(_context, "shiftuser@example.com");

        var userShift = new UserShift
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            WorkShiftId = shift.Id,
            StartsOn = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _context.UserShifts.Add(userShift);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.UserShifts
            .Include(us => us.User)
            .Include(us => us.WorkShift)
            .FirstOrDefaultAsync(us => us.Id == userShift.Id);

        saved.Should().NotBeNull();
        saved!.UserId.Should().Be(user.Id);
        saved.WorkShiftId.Should().Be(shift.Id);
        saved.User.Email.Should().Be("shiftuser@example.com");
        saved.WorkShift!.Name.Should().Be("Morning Shift");
    }

    [Fact]
    public async Task AssignUserToShift_WithDateRange_ShouldPersistDates()
    {
        // Arrange
        var shift = CreateTestShift();
        _context.WorkShifts.Add(shift);
        await _context.SaveChangesAsync();

        var user = await TestUserHelper.SeedTestUser(_context, "daterange@example.com");

        var startDate = new DateOnly(2025, 3, 1);
        var endDate = new DateOnly(2025, 6, 30);

        var userShift = new UserShift
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            WorkShiftId = shift.Id,
            StartsOn = startDate,
            EndsOn = endDate,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _context.UserShifts.Add(userShift);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.UserShifts.FirstOrDefaultAsync(us => us.Id == userShift.Id);
        saved.Should().NotBeNull();
        saved!.StartsOn.Should().Be(startDate);
        saved.EndsOn.Should().Be(endDate);
    }

    [Fact]
    public async Task AssignUserToShift_OpenEnded_ShouldHaveNullEndsOn()
    {
        // Arrange
        var shift = CreateTestShift();
        _context.WorkShifts.Add(shift);
        await _context.SaveChangesAsync();

        var user = await TestUserHelper.SeedTestUser(_context, "openended@example.com");

        var userShift = new UserShift
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            WorkShiftId = shift.Id,
            StartsOn = DateOnly.FromDateTime(DateTime.UtcNow),
            EndsOn = null, // open-ended assignment
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _context.UserShifts.Add(userShift);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.UserShifts.FirstOrDefaultAsync(us => us.Id == userShift.Id);
        saved.Should().NotBeNull();
        saved!.EndsOn.Should().BeNull("open-ended shift assignment should have null end date");
    }

    [Fact]
    public async Task AssignUserToRotation_ShouldLinkRotationNotShift()
    {
        // Arrange - create two shifts and a rotation
        var morningShift = CreateTestShift("Morning Shift", "MORNING");
        var nightShift = CreateTestShift("Night Shift", "NIGHT");
        _context.WorkShifts.AddRange(morningShift, nightShift);
        await _context.SaveChangesAsync();

        var rotation = new ShiftRotation
        {
            Id = Guid.NewGuid(),
            Title = "Weekly Rotation",
            CurrentActiveShiftId = morningShift.Id,
            RunDuration = 1,
            RunUnit = "Weeks",
            BreakDuration = 0,
            BreakUnit = "Day",
            IsActive = true
        };

        _context.ShiftRotations.Add(rotation);
        await _context.SaveChangesAsync();

        // Add shifts to rotation
        _context.RotationShifts.AddRange(
            new RotationShift { RotationId = rotation.Id, WorkShiftId = morningShift.Id, SequenceOrder = 1 },
            new RotationShift { RotationId = rotation.Id, WorkShiftId = nightShift.Id, SequenceOrder = 2 }
        );
        await _context.SaveChangesAsync();

        var user = await TestUserHelper.SeedTestUser(_context, "rotation@example.com");

        // Act - assign user to rotation (not a specific shift)
        var userShift = new UserShift
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            WorkShiftId = null, // not assigned to a specific shift
            ShiftRotationId = rotation.Id, // assigned to rotation
            StartsOn = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTime.UtcNow
        };

        _context.UserShifts.Add(userShift);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.UserShifts
            .Include(us => us.ShiftRotation)
            .FirstOrDefaultAsync(us => us.Id == userShift.Id);

        saved.Should().NotBeNull();
        saved!.WorkShiftId.Should().BeNull("user is assigned to rotation, not a specific shift");
        saved.ShiftRotationId.Should().Be(rotation.Id);
        saved.ShiftRotation!.Title.Should().Be("Weekly Rotation");
    }

    [Fact]
    public async Task AssignMultipleUsersToShift_ShouldCreateMultipleMappings()
    {
        // Arrange
        var shift = CreateTestShift();
        _context.WorkShifts.Add(shift);
        await _context.SaveChangesAsync();

        var user1 = await TestUserHelper.SeedTestUser(_context, "user1@example.com");
        var user2 = await TestUserHelper.SeedTestUser(_context, "user2@example.com");
        var user3 = await TestUserHelper.SeedTestUser(_context, "user3@example.com");

        var assignments = new[]
        {
            new UserShift { Id = Guid.NewGuid(), UserId = user1.Id, WorkShiftId = shift.Id, StartsOn = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow },
            new UserShift { Id = Guid.NewGuid(), UserId = user2.Id, WorkShiftId = shift.Id, StartsOn = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow },
            new UserShift { Id = Guid.NewGuid(), UserId = user3.Id, WorkShiftId = shift.Id, StartsOn = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow }
        };

        // Act
        _context.UserShifts.AddRange(assignments);
        await _context.SaveChangesAsync();

        // Assert
        var shiftUsers = await _context.UserShifts
            .Where(us => us.WorkShiftId == shift.Id)
            .ToListAsync();

        shiftUsers.Should().HaveCount(3, "three users should be assigned to the shift");
    }

    [Fact]
    public async Task GetUserShifts_ThroughUserNavigation_ShouldWork()
    {
        // Arrange
        var morningShift = CreateTestShift("Morning Shift", "MORNING");
        var nightShift = CreateTestShift("Night Shift", "NIGHT");
        _context.WorkShifts.AddRange(morningShift, nightShift);
        await _context.SaveChangesAsync();

        var user = await TestUserHelper.SeedTestUser(_context, "multishift@example.com");

        _context.UserShifts.AddRange(
            new UserShift
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                WorkShiftId = morningShift.Id,
                StartsOn = new DateOnly(2025, 1, 1),
                EndsOn = new DateOnly(2025, 3, 31),
                CreatedAt = DateTime.UtcNow
            },
            new UserShift
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                WorkShiftId = nightShift.Id,
                StartsOn = new DateOnly(2025, 4, 1),
                CreatedAt = DateTime.UtcNow
            }
        );
        await _context.SaveChangesAsync();

        // Act - load user with shift assignments
        var loadedUser = await _context.Users
            .Include(u => u.UserShifts)
            .ThenInclude(us => us.WorkShift)
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        // Assert
        loadedUser.Should().NotBeNull();
        loadedUser!.UserShifts.Should().HaveCount(2);
        loadedUser.UserShifts.Select(us => us.WorkShift!.Code)
            .Should().BeEquivalentTo(new[] { "MORNING", "NIGHT" });
    }

    #endregion

    #region Shift Rotation

    [Fact]
    public async Task ShiftRotation_ShouldLinkMultipleShiftsInSequence()
    {
        // Arrange
        var morningShift = CreateTestShift("Morning Shift", "MORNING");
        var afternoonShift = CreateTestShift("Afternoon Shift", "AFTERNOON");
        var nightShift = CreateTestShift("Night Shift", "NIGHT");
        _context.WorkShifts.AddRange(morningShift, afternoonShift, nightShift);
        await _context.SaveChangesAsync();

        var rotation = new ShiftRotation
        {
            Id = Guid.NewGuid(),
            Title = "3-Shift Rotation",
            CurrentActiveShiftId = morningShift.Id,
            RunDuration = 2,
            RunUnit = "Weeks",
            BreakDuration = 1,
            BreakUnit = "Day",
            IsActive = true
        };

        _context.ShiftRotations.Add(rotation);
        await _context.SaveChangesAsync();

        // Act - add shifts to rotation in sequence
        _context.RotationShifts.AddRange(
            new RotationShift { RotationId = rotation.Id, WorkShiftId = morningShift.Id, SequenceOrder = 1 },
            new RotationShift { RotationId = rotation.Id, WorkShiftId = afternoonShift.Id, SequenceOrder = 2 },
            new RotationShift { RotationId = rotation.Id, WorkShiftId = nightShift.Id, SequenceOrder = 3 }
        );
        await _context.SaveChangesAsync();

        // Assert
        var savedRotation = await _context.ShiftRotations
            .Include(r => r.RotationShifts)
            .ThenInclude(rs => rs.WorkShift)
            .FirstOrDefaultAsync(r => r.Id == rotation.Id);

        savedRotation.Should().NotBeNull();
        savedRotation!.RotationShifts.Should().HaveCount(3);
        savedRotation.RotationShifts.OrderBy(rs => rs.SequenceOrder)
            .Select(rs => rs.WorkShift.Code)
            .Should().ContainInOrder("MORNING", "AFTERNOON", "NIGHT");
    }

    #endregion
}
