using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HSEQRCode.Domain.Entities
{
    /// <summary>
    /// Configured SQL Server view data source (flat or EAV) representing an asset category/form.
    /// </summary>
    public class DataSource
    {
        public DataSource()
        {
            CustomFields = new HashSet<CustomField>();
            AssetIndicators = new HashSet<AssetIndicator>();
            FormPersonnel = new HashSet<FormPersonnel>();
            InspectionRecords = new HashSet<InspectionRecord>();
        }

        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        [Required]
        [MaxLength(256)]
        public string ViewName { get; set; }

        [Required]
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

        public string IndicatorColumnsJson { get; set; }

        [MaxLength(10)]
        public string IndicatorSeparator { get; set; }

        [MaxLength(50)]
        public string IndicatorPrefix { get; set; }

        public string PublicBaseFieldsJson { get; set; }

        public string LabelCaptionFieldsJson { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public virtual ICollection<CustomField> CustomFields { get; set; }

        public virtual ICollection<AssetIndicator> AssetIndicators { get; set; }

        public virtual ICollection<FormPersonnel> FormPersonnel { get; set; }

        public virtual ICollection<InspectionRecord> InspectionRecords { get; set; }
    }
}
