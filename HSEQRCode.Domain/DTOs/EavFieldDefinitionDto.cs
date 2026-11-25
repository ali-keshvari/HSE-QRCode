using HSEQRCode.Domain.Enums;

namespace HSEQRCode.Domain.DTOs
{
    /// <summary>
    /// Field definition extracted from an EAV base asset view.
    /// </summary>
    public class EavFieldDefinitionDto
    {
        public int CategoryFieldId { get; set; }

        public string FieldName { get; set; }

        public BaseFieldType FieldType { get; set; }

        public bool IsRequired { get; set; }

        public int DisplayOrder { get; set; }
    }
}
