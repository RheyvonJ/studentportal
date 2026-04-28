using System.ComponentModel.DataAnnotations;

namespace StudentPortal.Models
{
    public class TeacherChangePasswordViewModel
    {
        [Required(ErrorMessage = "Current password is required.")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "New password is required.")]
        [MinLength(8, ErrorMessage = "New password must be at least 8 characters.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your new password.")]
        [Compare(nameof(NewPassword), ErrorMessage = "Password confirmation does not match.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
