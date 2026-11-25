using System;
using System.ComponentModel.DataAnnotations;

namespace HSEQRCode.Domain.Entities
{
    /// <summary>
    /// Application user account for admin and inspection personnel authentication.
    /// </summary>
    public class AppUser
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Username { get; set; }

        [Required]
        [MaxLength(256)]
        public string PasswordHash { get; set; }

        [Required]
        [MaxLength(200)]
        public string FullName { get; set; }

        public bool IsAdmin { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }
}
