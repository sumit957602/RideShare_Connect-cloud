using System.ComponentModel.DataAnnotations;

namespace RideShare_Connect.ViewModels
{
    public class AdminProfileViewModel
    {
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Display(Name = "Profile Picture URL")]
        [Url(ErrorMessage = "Please enter a valid URL")]
        public string ProfilePicUrl { get; set; }

        public string ExistingProfilePicUrl { get; set; }
    }
}
