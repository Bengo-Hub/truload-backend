using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Data.Repositories.Weighing;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(Guid id);
    Task<Vehicle?> GetByRegNoAsync(string regNo);
    Task<IEnumerable<Vehicle>> SearchAsync(string query);
    Task<(IEnumerable<Vehicle> Items, int TotalCount)> SearchPagedAsync(string? search, int page, int pageSize, Guid? transporterId = null);
    Task<Vehicle> CreateAsync(Vehicle vehicle);
    Task UpdateAsync(Vehicle vehicle);
}
