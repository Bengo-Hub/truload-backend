using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Data.Repositories.Weighing;

public interface IDriverRepository
{
    Task<Driver?> GetByIdAsync(Guid id);
    Task<Driver?> GetByIdNumberAsync(string idNumber);
    Task<Driver?> GetByLicenseAsync(string licenseNo);
    Task<IEnumerable<Driver>> SearchAsync(string query);
    Task<Driver> CreateAsync(Driver driver);
    Task UpdateAsync(Driver driver);
}
