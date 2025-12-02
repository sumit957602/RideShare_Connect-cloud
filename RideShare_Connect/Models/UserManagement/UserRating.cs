using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RideShare_Connect.Models.RideManagement;
using RideShare_Connect.Models.UserManagement;
using RideShare_Connect.Models.VehicleManagement;

namespace RideShare_Connect.Models.UserManagement
{
    public class UserRating
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int RideId { get; set; }
        [ForeignKey("RideId")]
        public Ride Ride { get; set; }

        [Required]
        public int DriverId { get; set; }
        [ForeignKey("DriverId")]
        public Driver Driver { get; set; }

        [Required]
        public int PassengerId { get; set; }
        [ForeignKey("PassengerId")]
        public User User { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(500)]
        public string Review { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
