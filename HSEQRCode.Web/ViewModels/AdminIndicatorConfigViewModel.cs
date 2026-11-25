using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HSEQRCode.Web.ViewModels
{
    public class AdminIndicatorConfigViewModel
    {
        public AdminIndicatorConfigViewModel()
        {
            AvailableColumns = new List<string>();
            SelectedIndicatorColumns = new List<string>();
            Indicators = new List<AssetIndicatorItemViewModel>();
        }

        public int DataSourceId { get; set; }

        public string DataSourceName { get; set; }

        public List<string> AvailableColumns { get; set; }

        public List<string> SelectedIndicatorColumns { get; set; }

        [MaxLength(10)]
        public string IndicatorSeparator { get; set; }

        [MaxLength(50)]
        public string IndicatorPrefix { get; set; }

        public int GeneratedCount { get; set; }

        public List<AssetIndicatorItemViewModel> Indicators { get; set; }
    }

    public class AssetIndicatorItemViewModel
    {
        public int Id { get; set; }

        public string AssetKey { get; set; }

        public string Indicator { get; set; }

        public string QrPayloadUrl { get; set; }

        public string QrPngBase64 { get; set; }
    }
}
