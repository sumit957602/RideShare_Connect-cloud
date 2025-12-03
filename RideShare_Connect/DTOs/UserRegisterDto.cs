// DTOs/UserRegisterDto.cs (for user registration input)
using System.ComponentModel.DataAnnotations;

namespace RideShare_Connect.DTOs
{
    public class UserRegisterDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [Phone(ErrorMessage = "Invalid phone number")]
        [RegularExpression(@"^\+?[0-9]{10,15}$", ErrorMessage = "Phone number must be between 10 and 15 digits.")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$", ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Confirm Password is required")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "Full Name is required")]
        [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters")]
        [RegularExpression(@"^[a-zA-Z\s]*$", ErrorMessage = "Full Name can only contain letters and spaces.")]
        public string FullName { get; set; }

        public string UserType { get; set; } = "Rider";

        public string? Otp { get; set; }
    }
}