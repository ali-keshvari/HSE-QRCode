using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HSEQRCode.Domain.Entities
{
    /// <summary>
    /// Generated unique QR indicator and cached base snapshot for an asset within a DataSource.
    /// </summary>
    public class AssetIndicator
    {
        public AssetIndicator()
        {
            InspectionRecords = new HashSet<InspectionRecord>();
        }

        [Key]
        public int Id { get; set; }

        public int DataSourceId { get; set; }

        [Required]
        [MaxLength(200)]
        public string AssetKey { get; set; }

        [Required]
        [MaxLength(250)]
        public string Indicator { get; set; }

        [MaxLength(500)]
        public string QrPayloadUrl { get; set; }

        public string CachedBaseFieldsJson { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public virtual DataSource DataSource { get; set; }

        public virtual ICollection<InspectionRecord> InspectionRecords { get; set; }
    }
}
