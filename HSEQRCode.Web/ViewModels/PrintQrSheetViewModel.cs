using System.Collections.Generic;

namespace HSEQRCode.Web.ViewModels
{
    public class PrintQrSheetViewModel
    {
        public PrintQrSheetViewModel()
        {
            LabelCaptionFields = new List<string>();
            Items = new List<PrintQrItemViewModel>();
        }

        public int DataSourceId { get; set; }

        public string DataSourceName { get; set; }

        /// <summary>
        /// "A4" or "Thermal".
        /// </summary>
        public string LayoutMode { get; set; }

        public List<string> LabelCaptionFields { get; set; }

        public List<PrintQrItemViewModel> Items { get; set; }
    }

    public class PrintQrItemViewModel
    {
        public PrintQrItemViewModel()
        {
            Captions = new Dictionary<string, string>();
        }

        public string Indicator { get; set; }

        public string AssetKey { get; set; }

        public string QrPayloadUrl { get; set; }

        public string QrPngBase64 { get; set; }

        public Dictionary<string, string> Captions { get; set; }
    }
}
