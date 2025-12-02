using System.Collections.Generic;
using RideShare_Connect.Models.UserManagement;
using RideShare_Connect.Models.VehicleManagement;
using RideShare_Connect.Models.RideManagement;
using RideShare_Connect.Models.PaymentManagement;
using RideShare_Connect.Models.AdminManagement;

namespace RideShare_Connect.ViewModels
{
    public class AdminDashboardViewModel
    {
        public decimal TotalRevenue { get; set; }
        public decimal PlatformWalletBalance { get; set; }
        public int TotalUsers { get; set; }
        public int RidesCompleted { get; set; }
        public int OpenReports { get; set; }

        public string AdminName { get; set; }
        public string AdminProfilePicUrl { get; set; }

        public List<User> Users { get; set; } = new();
        public List<Driver> Drivers { get; set; } = new();
        public List<Vehicle> Vehicles { get; set; } = new();

        public List<Ride> Rides { get; set; } = new();
        public List<RideBooking> RideBookings { get; set; }
        public List<UserReport> Reports { get; set; }
        public List<Payment> Payments { get; set; }
        public List<RideShare_Connect.Models.PaymentManagement.Refund> Refunds { get; set; }
        public SystemConfigViewModel SystemConfig { get; set; }

        // Chart Data
        public List<string> ChartLabels { get; set; } = new();
        public List<int> UserGrowthData { get; set; } = new();
        public List<decimal> RevenueData { get; set; } = new();
    }

    public class SystemConfigViewModel
    {
        public decimal CommissionRate { get; set; } = 10.0m;
        public bool MaintenanceMode { get; set; } = false;
        public string SupportEmail { get; set; } = "support@rideshareconnect.com";
        public string SupportPhone { get; set; } = "+91 1234567890";
    }
}
