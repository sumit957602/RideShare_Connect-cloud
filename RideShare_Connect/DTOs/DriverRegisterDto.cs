using System;
using System.ComponentModel.DataAnnotations;
using RideShare_Connect.Attributes;

namespace RideShare_Connect.DTOs
{
    public class DriverRegisterDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Phone number must be 10 digits and start with 6, 7, 8, or 9.")]
        public string PhoneNumber { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$", ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character.")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        [Required]
        [StringLength(100)]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Name should contain only alphabets.")]
        public string FullName { get; set; }

        [Required]
        [StringLength(16, MinimumLength = 16, ErrorMessage = "License Number must be exactly 16 characters long.")]
        [RegularExpression(@"^([A-Z]{2}[ -]\d{13}|[A-Z]{2}\d{2}[ -]\d{11})$", ErrorMessage = "Invalid License Number format. Example: HR-0619850034761 or HR06 19850034761")]
        public string LicenseNumber { get; set; }

        [Range(1, 50, ErrorMessage = "Driving Experience must be at least 1 year.")]
        [ValidDrivingExperience("DOB")]
        public int DrivingExperienceYears { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [MinimumAge(18)]
        public DateTime DOB { get; set; }
    }
}
