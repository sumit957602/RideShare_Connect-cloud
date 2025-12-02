using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace RideShare_Connect.ViewModels
{
    public class AdminSettingsViewModel
    {
        public AdminProfileViewModel Profile { get; set; } = new AdminProfileViewModel();
        public AdminPasswordViewModel Password { get; set; } = new AdminPasswordViewModel();
    }
}
