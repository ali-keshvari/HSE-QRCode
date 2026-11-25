using System.Collections.Generic;

namespace HSEQRCode.Domain.DTOs
{
    /// <summary>
    /// Pivoted base asset row keyed by AssetKey with field-name to value dictionary.
    /// </summary>
    public class PivotedAssetRowDto
    {
        public PivotedAssetRowDto()
        {
            BaseValues = new Dictionary<string, AssetFieldValueDto>();
        }

        public string AssetKey { get; set; }

        public Dictionary<string, AssetFieldValueDto> BaseValues { get; set; }
    }
}
