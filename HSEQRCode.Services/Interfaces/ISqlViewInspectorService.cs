using System.Collections.Generic;
using System.Threading.Tasks;
using HSEQRCode.Domain.DTOs;
using HSEQRCode.Domain.Entities;

namespace HSEQRCode.Services.Interfaces
{
    /// <summary>
    /// Safely inspects and queries whitelisted SQL Server views (both Flat and EAV).
    /// </summary>
    public interface ISqlViewInspectorService
    {
        Task<List<ViewMetadataDto>> GetAvailableViewsAsync();

        Task<ViewMetadataDto> InspectViewMetadataAsync(string viewName);

        Task<List<EavFieldDefinitionDto>> DiscoverEavFieldsAsync(DataSource dataSource);

        Task<List<PivotedAssetRowDto>> QueryAssetsAsync(DataSource dataSource);

        Task<PivotedAssetRowDto> QuerySingleAssetAsync(DataSource dataSource, string assetKey);

        Task<List<SelectOptionDto>> LoadSelectOptionsAsync(CustomField field);
    }
}
