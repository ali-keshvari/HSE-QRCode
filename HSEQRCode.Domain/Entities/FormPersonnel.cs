using System;
using System.ComponentModel.DataAnnotations;

namespace HSEQRCode.Domain.Entities
{
    /// <summary>
    /// Personnel authorized to submit inspections for a specific DataSource.
    /// </summary>
    public class FormPersonnel
    {
        [Key]
        public int Id { get; set; }

        public int DataSourceId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Username { get; set; }

        [Required]
        [MaxLength(200)]
        public string FullName { get; set; }

        public bool IsActive { get; set; }

        public DateTime AssignedAtUtc { get; set; }

        public virtual DataSource DataSource { get; set; }
    }
}
