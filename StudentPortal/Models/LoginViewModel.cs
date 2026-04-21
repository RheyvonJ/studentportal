using System.ComponentModel.DataAnnotations;

namespace StudentPortal.Models
{
    public class LoginViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        // Optional: used to redirect back to a deep-link after login (e.g. /StudentClass/{classCode})
        public string? ReturnUrl { get; set; }
    }
}
