using System;
using System.Collections.Generic;
using System.IO;
using HSEQRCode.Domain.DTOs;
using HSEQRCode.Domain.Enums;

namespace HSEQRCode.Services.Models
{
    public class BaseFieldDisplayItem
    {
        public string FieldName { get; set; }

        public BaseFieldType FieldType { get; set; }

        public string DisplayValue { get; set; }

        public string RawValue { get; set; }

        public bool IsGregorian { get; set; }
    }

    public class CustomFieldInputDefinition
    {
        public CustomFieldInputDefinition()
        {
            Options = new List<SelectOptionDto>();
        }

        public int CustomFieldId { get; set; }

        public string Name { get; set; }

        public string Label { get; set; }

        public CustomFieldType FieldType { get; set; }

        public bool IsRequired { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsPublicVisible { get; set; }

        public string BooleanTrueLabel { get; set; }

        public string BooleanFalseLabel { get; set; }

        public bool IsGregorianDate { get; set; }

        public List<SelectOptionDto> Options { get; set; }
    }

    public class InspectionFieldValueSummary
    {
        public InspectionFieldValueSummary()
        {
            SelectedValues = new List<string>();
        }

        public int ValueId { get; set; }

        public int CustomFieldId { get; set; }

        public string FieldName { get; set; }

        public string FieldLabel { get; set; }

        public CustomFieldType FieldType { get; set; }

        public string DisplayValue { get; set; }

        public List<string> SelectedValues { get; set; }

        public string StoredFileName { get; set; }

        public string OriginalFileName { get; set; }

        public string ContentType { get; set; }
    }

    public class InspectionRecordSummary
    {
        public InspectionRecordSummary()
        {
            Values = new List<InspectionFieldValueSummary>();
        }

        public int RecordId { get; set; }

        public string InspectorUsername { get; set; }

        public string InspectorFullName { get; set; }

        public DateTime InspectedAtUtc { get; set; }

        public string InspectedAtJalali { get; set; }

        public string Notes { get; set; }

        public List<InspectionFieldValueSummary> Values { get; set; }
    }

    public class PublicScanResult
    {
        public PublicScanResult()
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
    }

    public class PersonnelInspectionResult
    {
        public PersonnelInspectionResult()
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
    }

    public class UploadedFileInput
    {
        public string FileName { get; set; }

        public string ContentType { get; set; }

        public Stream InputStream { get; set; }
    }

    public class InspectionSubmissionRequest
    {
        public InspectionSubmissionRequest()
        {
            ScalarValues = new Dictionary<int, string>();
            MultiSelectValues = new Dictionary<int, List<string>>();
            Files = new Dictionary<int, UploadedFileInput>();
        }

        public string Indicator { get; set; }

        public string Username { get; set; }

        public string Notes { get; set; }

        public string UploadRootPath { get; set; }

        public Dictionary<int, string> ScalarValues { get; set; }

        public Dictionary<int, List<string>> MultiSelectValues { get; set; }

        public Dictionary<int, UploadedFileInput> Files { get; set; }
    }
}
