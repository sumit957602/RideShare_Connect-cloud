using System.ComponentModel.DataAnnotations;

namespace RideShare_Connect.ViewModels
{
    public class AdminPasswordViewModel
    {
        [Display(Name = "Current Password")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; }

        [Display(Name = "New Password")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters long.")]
        public string NewPassword { get; set; }

        [Display(Name = "Confirm New Password")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmNewPassword { get; set; }
    }
}
