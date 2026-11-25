using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HSEQRCode.Domain.Entities
{
    /// <summary>
    /// Header record for a single asset inspection submission.
    /// </summary>
    public class InspectionRecord
    {
        public InspectionRecord()
        {
            Values = new HashSet<InspectionRecordValue>();
        }

        [Key]
        public int Id { get; set; }

        public int DataSourceId { get; set; }

        public int AssetIndicatorId { get; set; }

        [Required]
        [MaxLength(200)]
        public string AssetKey { get; set; }

        [Required]
        [MaxLength(100)]
        public string InspectorUsername { get; set; }

        [Required]
        [MaxLength(200)]
        public string InspectorFullName { get; set; }

        public DateTime InspectedAtUtc { get; set; }

        [MaxLength(2000)]
        public string Notes { get; set; }

        public virtual DataSource DataSource { get; set; }

        public virtual AssetIndicator AssetIndicator { get; set; }

        public virtual ICollection<InspectionRecordValue> Values { get; set; }
    }
}
