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
        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Phone number must be 10 digits and start with 6, 7, 8, or 9.")]
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

        [Required(ErrorMessage = "Secret Key is required")]
        [StringLength(50, MinimumLength = 6, ErrorMessage = "Secret Key must be between 6 and 50 characters.")]
        public string SecretKey { get; set; }

        public string UserType { get; set; } = "Rider";

        [RideShare_Connect.Attributes.MustBeTrue(ErrorMessage = "You must agree to the Terms and Conditions.")]
        public bool TermsAccepted { get; set; }

        public string? Otp { get; set; }
    }
}