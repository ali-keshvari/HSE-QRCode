using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HSEQRCode.Domain.Enums;

namespace HSEQRCode.Domain.Entities
{
    /// <summary>
    /// Admin-defined custom inspection field attached to a DataSource.
    /// </summary>
    public class CustomField
    {
        public CustomField()
        {
            InspectionRecordValues = new HashSet<InspectionRecordValue>();
        }

        [Key]
        public int Id { get; set; }

        public int DataSourceId { get; set; }

        [Required]
        [MaxLength(128)]
        public string Name { get; set; }

        [Required]
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

        public bool IsDeleted { get; set; }

        public virtual DataSource DataSource { get; set; }

        public virtual ICollection<InspectionRecordValue> InspectionRecordValues { get; set; }
    }
}
