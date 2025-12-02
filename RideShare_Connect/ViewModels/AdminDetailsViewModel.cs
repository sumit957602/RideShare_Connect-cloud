using RideShare_Connect.Models.UserManagement;
using RideShare_Connect.Models.VehicleManagement;

namespace RideShare_Connect.ViewModels
{
    public class AdminUserDetailsViewModel
    {
        public User User { get; set; }
        public double AverageRating { get; set; }
        public int CompletedRides { get; set; }
    }

    public class AdminDriverDetailsViewModel
    {
        public Driver Driver { get; set; }
        public double AverageRating { get; set; }
        public int CompletedRides { get; set; }
    }
}
