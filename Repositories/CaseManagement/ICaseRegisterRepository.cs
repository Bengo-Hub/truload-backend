using TruLoad.Backend.Models.CaseManagement;

namespace TruLoad.Backend.Repositories.CaseManagement;

public interface ICaseRegisterRepository
{
    Task<CaseRegister?> GetByIdAsync(Guid id);
    Task<CaseRegister?> GetByCaseNoAsync(string caseNo);
    Task<CaseRegister?> GetByWeighingIdAsync(Guid weighingId);
    Task<CaseRegister?> GetByProhibitionOrderIdAsync(Guid prohibitionOrderId);
    Task<IEnumerable<CaseRegister>> GetAllAsync(int pageNumber = 1, int pageSize = 50);
    Task<IEnumerable<CaseRegister>> SearchAsync(
        string? caseNo = null,
        string? vehicleRegNumber = null,
        Guid? violationTypeId = null,
        Guid? caseStatusId = null,
        Guid? dispositionTypeId = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        bool? escalatedToCaseManager = null,
        Guid? caseManagerId = null,
        int pageNumber = 1,
        int pageSize = 50);
    Task<int> GetTotalCountAsync();
    Task<int> GetCountByStatusAsync(Guid caseStatusId);
    Task<int> GetCountByDispositionAsync(Guid dispositionTypeId);
    Task<CaseRegister> CreateAsync(CaseRegister caseRegister);
    Task<CaseRegister> UpdateAsync(CaseRegister caseRegister);
    Task<bool> DeleteAsync(Guid id);
    Task<string> GenerateNextCaseNumberAsync(string stationPrefix);
    Task<bool> CaseNumberExistsAsync(string caseNo);
}
