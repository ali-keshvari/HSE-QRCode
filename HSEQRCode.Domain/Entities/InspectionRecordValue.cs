using System;
using System.ComponentModel.DataAnnotations;

namespace HSEQRCode.Domain.Entities
{
    /// <summary>
    /// Typed value submitted for a specific CustomField in an InspectionRecord.
    /// </summary>
    public class InspectionRecordValue
    {
        [Key]
        public int Id { get; set; }

        public int InspectionRecordId { get; set; }

        public int CustomFieldId { get; set; }

        public string TextValue { get; set; }

        public decimal? NumberValue { get; set; }

        public DateTime? DateValue { get; set; }

        public bool? BooleanValue { get; set; }

        public string SelectedValuesJson { get; set; }

        [MaxLength(260)]
        public string StoredFileName { get; set; }

        [MaxLength(260)]
        public string OriginalFileName { get; set; }

        [MaxLength(150)]
        public string ContentType { get; set; }

        public virtual InspectionRecord InspectionRecord { get; set; }

        public virtual CustomField CustomField { get; set; }
    }
}
