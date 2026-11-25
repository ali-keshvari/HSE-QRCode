using System.Threading.Tasks;

namespace HSEQRCode.Services.Interfaces
{
    /// <summary>
    /// Generates unique asset indicators, caches base asset snapshots, and renders QR codes.
    /// </summary>
    public interface IIndicatorQrService
    {
        Task<int> GenerateIndicatorsForDataSourceAsync(int dataSourceId, string baseUrl);

        string GenerateQrPngBase64(string payloadUrl, int pixelsPerModule = 10);

        string GenerateQrSvg(string payloadUrl, int pixelsPerModule = 5);
    }
}
