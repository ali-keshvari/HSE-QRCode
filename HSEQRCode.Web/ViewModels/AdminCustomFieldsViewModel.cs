using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HSEQRCode.Domain.DTOs;
using HSEQRCode.Domain.Enums;

namespace HSEQRCode.Web.ViewModels
{
    public class AdminCustomFieldsViewModel
    {
        public AdminCustomFieldsViewModel()
        {
            Fields = new List<CustomFieldItemViewModel>();
            NewField = new CustomFieldFormViewModel();
        }

        public int DataSourceId { get; set; }

        public string DataSourceName { get; set; }

        public List<CustomFieldItemViewModel> Fields { get; set; }

        public CustomFieldFormViewModel NewField { get; set; }
    }

    public class CustomFieldItemViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Label { get; set; }

        public CustomFieldType FieldType { get; set; }

        public bool IsRequired { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsPublicVisible { get; set; }

        public OptionsSourceType OptionsSourceType { get; set; }

        public bool IsGregorianDate { get; set; }
    }

    public class CustomFieldFormViewModel
    {
        public CustomFieldFormViewModel()
        {
            AvailableViews = new List<ViewMetadataDto>();
        }

        public int Id { get; set; }

        public int DataSourceId { get; set; }

        [Required(ErrorMessage = "نام سیستمی فیلد الزامی است.")]
        [MaxLength(128)]
        public string Name { get; set; }

        [Required(ErrorMessage = "عنوان نمایشی فیلد الزامی است.")]
        [MaxLength(200)]
        public string Label { get; set; }

        public CustomFieldType FieldType { get; set; }

        public bool IsRequired { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsPublicVisible { get; set; }

        [MaxLength(100)]
        public string BooleanTrueLabel { get; set; }

        [MaxLength(100)]
        public string BooleanFalseLabel { get; set; }

        public OptionsSourceType OptionsSourceType { get; set; }

        public string StaticOptionsJson { get; set; }

        [MaxLength(256)]
        public string OptionsViewName { get; set; }

        [MaxLength(128)]
        public string OptionsValueColumn { get; set; }

        [MaxLength(128)]
        public string OptionsDisplayColumn { get; set; }

        public bool IsGregorianDate { get; set; }

        public List<ViewMetadataDto> AvailableViews { get; set; }
    }
}
