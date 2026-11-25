using System.Collections.Generic;
using HSEQRCode.Services.Models;

namespace HSEQRCode.Web.ViewModels
{
    public class PersonnelInspectionViewModel
    {
        public PersonnelInspectionViewModel()
        {
            BaseFields = new List<BaseFieldDisplayItem>();
            CustomFields = new List<CustomFieldInputDefinition>();
            InspectionHistory = new List<InspectionRecordSummary>();
        }

        public int AssetIndicatorId { get; set; }

        public int DataSourceId { get; set; }

        public string DataSourceName { get; set; }

        public string Indicator { get; set; }

        public string AssetKey { get; set; }

        public string QrPayloadUrl { get; set; }

        public List<BaseFieldDisplayItem> BaseFields { get; set; }

        public List<CustomFieldInputDefinition> CustomFields { get; set; }

        public List<InspectionRecordSummary> InspectionHistory { get; set; }

        public string Notes { get; set; }

        public string ErrorMessage { get; set; }

        public string SuccessMessage { get; set; }
    }
}
