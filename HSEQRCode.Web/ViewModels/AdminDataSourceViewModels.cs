using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HSEQRCode.Domain.DTOs;

namespace HSEQRCode.Web.ViewModels
{
    public class AdminDataSourceListViewModel
    {
        public AdminDataSourceListViewModel()
        {
            DataSources = new List<DataSourceSummaryItem>();
        }

        public List<DataSourceSummaryItem> DataSources { get; set; }
    }

    public class DataSourceSummaryItem
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string ViewName { get; set; }

        public bool IsEav { get; set; }

        public bool IsActive { get; set; }

        public int AssetIndicatorCount { get; set; }

        public int CustomFieldCount { get; set; }

        public int PersonnelCount { get; set; }

        public string CreatedAtJalali { get; set; }
    }

    public class AdminDataSourceCreateViewModel
    {
        public AdminDataSourceCreateViewModel()
        {
            AvailableViews = new List<ViewMetadataDto>();
        }

        [Required(ErrorMessage = "نام منبع داده الزامی است.")]
        [MaxLength(200)]
        public string Name { get; set; }

        [Required(ErrorMessage = "انتخاب نمای SQL الزامی است.")]
        [MaxLength(256)]
        public string ViewName { get; set; }

        [Required(ErrorMessage = "ستون کلید یکتا الزامی است.")]
        [MaxLength(128)]
        public string UniqueKeyColumn { get; set; }

        public bool IsEav { get; set; }

        [MaxLength(128)]
        public string EavAssetIdColumn { get; set; }

        [MaxLength(128)]
        public string EavFieldIdColumn { get; set; }

        [MaxLength(128)]
        public string EavFieldNameColumn { get; set; }

        [MaxLength(128)]
        public string EavFieldTypeColumn { get; set; }

        [MaxLength(128)]
        public string EavDisplayOrderColumn { get; set; }

        public List<ViewMetadataDto> AvailableViews { get; set; }
    }

    public class AdminDataSourceConfigViewModel
    {
        public AdminDataSourceConfigViewModel()
        {
            AvailableBaseFields = new List<string>();
            SelectedPublicBaseFields = new List<string>();
            SelectedLabelCaptionFields = new List<string>();
        }

        public int DataSourceId { get; set; }

        public string DataSourceName { get; set; }

        public string ViewName { get; set; }

        public bool IsEav { get; set; }

        public bool IsActive { get; set; }

        public List<string> AvailableBaseFields { get; set; }

        public List<string> SelectedPublicBaseFields { get; set; }

        public List<string> SelectedLabelCaptionFields { get; set; }
    }
}
