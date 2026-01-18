using TruLoad.Backend.Models.CaseManagement;

namespace TruLoad.Backend.Repositories.CaseManagement;

public interface ISpecialReleaseRepository
{
    Task<SpecialRelease?> GetByIdAsync(Guid id);
    Task<SpecialRelease?> GetByCertificateNoAsync(string certificateNo);
    Task<IEnumerable<SpecialRelease>> GetByCaseRegisterIdAsync(Guid caseRegisterId);
    Task<IEnumerable<SpecialRelease>> GetPendingApprovalsAsync(int pageNumber = 1, int pageSize = 20);
    Task<IEnumerable<SpecialRelease>> GetApprovedReleasesAsync(DateTime? from = null, DateTime? to = null, int pageNumber = 1, int pageSize = 20);
    Task<SpecialRelease> CreateAsync(SpecialRelease specialRelease);
    Task<SpecialRelease> UpdateAsync(SpecialRelease specialRelease);
    Task<string> GenerateNextCertificateNumberAsync();
    Task<bool> CertificateNumberExistsAsync(string certificateNo);
}
