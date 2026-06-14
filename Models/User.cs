using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserManagementApp.Models
{
    public enum UserStatus
    {
        Unverified,
        Active,
        Blocked
    }

    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public UserStatus Status { get; set; } = UserStatus.Unverified;

        public DateTime RegistrationTime { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginTime { get; set; }
        [MaxLength(100)]
        public string? JobTitle { get; set; }

        [MaxLength(100)]
        public string? Company { get; set; }

        [NotMapped]
        public string UniqueIdValue => GetUniqIdValue();

        public static string GetUniqIdValue()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
