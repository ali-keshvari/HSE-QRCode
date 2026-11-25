using System.Threading.Tasks;
using HSEQRCode.Domain.Entities;
using HSEQRCode.Services.Models;

namespace HSEQRCode.Services.Interfaces
{
    /// <summary>
    /// Provides public scan views, authorized personnel inspection views, and inspection submission.
    /// </summary>
    public interface IInspectionService
    {
        Task<PublicScanResult> GetPublicScanDataAsync(string indicator);

        Task<PersonnelInspectionResult> GetPersonnelInspectionDataAsync(string indicator, string username);

        Task<InspectionRecord> SubmitInspectionAsync(InspectionSubmissionRequest request);

        Task<InspectionRecordValue> GetAttachmentForDownloadAsync(int recordValueId, string username);
    }
}
