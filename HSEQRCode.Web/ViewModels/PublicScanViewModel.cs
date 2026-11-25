using System.Collections.Generic;
using HSEQRCode.Services.Models;

namespace HSEQRCode.Web.ViewModels
{
    public class PublicScanViewModel
    {
        public PublicScanViewModel()
        {
            PublicBaseFields = new List<BaseFieldDisplayItem>();
        }

        public int AssetIndicatorId { get; set; }

        public int DataSourceId { get; set; }

        public string DataSourceName { get; set; }

        public string Indicator { get; set; }

        public string AssetKey { get; set; }

        public string QrPayloadUrl { get; set; }

        public List<BaseFieldDisplayItem> PublicBaseFields { get; set; }

        public InspectionRecordSummary LatestInspection { get; set; }

        public bool IsUserAuthenticated { get; set; }

        public bool CanInspect { get; set; }
    }
}
