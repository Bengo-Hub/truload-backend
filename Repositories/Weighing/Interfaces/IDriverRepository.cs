using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Data.Repositories.Weighing;

public interface IDriverRepository
{
    Task<Driver?> GetByIdAsync(Guid id);
    Task<Driver?> GetByIdNumberAsync(string idNumber);
    Task<Driver?> GetByLicenseAsync(string licenseNo);
    Task<IEnumerable<Driver>> SearchAsync(string query, Guid? transporterId = null);
    Task<Driver> CreateAsync(Driver driver);
    Task UpdateAsync(Driver driver);

    /// <summary>Finds an active driver by normalized full name + surname (case-insensitive). Used to
    /// reuse an existing record when a new driver is captured without an ID/license.</summary>
    Task<Driver?> FindActiveByNameAsync(string fullNames, string surname);

    /// <summary>Merges duplicate driver records (same name), preferring the record that has an ID
    /// number, repointing all FK references to the survivor and hard-deleting the duplicates.</summary>
    Task<DriverDeduplicationResult> DeduplicateAsync();

    /// <summary>Permanently (hard) deletes a driver. Clears nullable FK references on weighings,
    /// cases and parties and removes the driver's demerit records first so the delete succeeds.
    /// Returns false if the driver does not exist.</summary>
    Task<bool> HardDeleteAsync(Guid id);
}
