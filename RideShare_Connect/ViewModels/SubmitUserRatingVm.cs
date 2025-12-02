using System.ComponentModel.DataAnnotations;

namespace RideShare_Connect.ViewModels
{
    public class SubmitUserRatingVm
    {
        public int BookingId { get; set; }
        public int RideId { get; set; }
        public int DriverId { get; set; }
        public int PassengerId { get; set; }
        public string PassengerName { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Please select a rating between 1 and 5.")]
        public int Rating { get; set; }

        [StringLength(500, ErrorMessage = "Review cannot exceed 500 characters.")]
        public string Review { get; set; }
    }
}
