using HSEQRCode.Domain.Enums;

namespace HSEQRCode.Domain.DTOs
{
    /// <summary>
    /// Typed value wrapper for a single field in a pivoted base asset row.
    /// </summary>
    public class AssetFieldValueDto
    {
        public string FieldName { get; set; }

        public BaseFieldType FieldType { get; set; }

        public string DisplayValue { get; set; }

        public string RawValue { get; set; }

        public bool IsGregorian { get; set; }

        public string StoredFileName { get; set; }

        public string OriginalFileName { get; set; }

        public string ContentType { get; set; }
    }
}
