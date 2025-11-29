using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideShare_Connect.DTOs;
using RideShare_Connect.ViewModels;
using RideShareConnect.Data;
using System.Security.Claims;
using RideShare_Connect.Models.RideManagement;
using RideShare_Connect.Models.VehicleManagement;

namespace RideShare_Connect.Controllers
{
    public class ReviewController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<ReviewController> _logger;

        public ReviewController(ApplicationDbContext db, ILogger<ReviewController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Review(int rideId, int driverId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "UserAccount");

            var ride = await _db.Rides
                .Include(r => r.Driver)
                .FirstOrDefaultAsync(r => r.Id == rideId && r.DriverId == driverId);

            if (ride == null) return NotFound();

            var vm = new SubmitDriverRatingVm
            {
                RideId = rideId,
                DriverId = driverId,
                DriverName = ride.Driver.FullName,
                From = ride.Origin,
                To = ride.Destination,
                DepartureTime = ride.DepartureTime
            };

            return View("Review", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(SubmitDriverRatingVm vm)
        {
            if (!ModelState.IsValid)
                return View("Review", vm);

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return RedirectToAction("Login", "UserAccount");

            try
            {
                var rating = new DriverRating
                {
                    RideId = vm.RideId,
                    DriverId = vm.DriverId,
                    PassengerId = userId,
                    Rating = vm.Rating,
                    Review = vm.Review?.Trim(),
                    Timestamp = DateTime.UtcNow
                };

                _db.DriverRatings.Add(rating);
                await _db.SaveChangesAsync();

                TempData["Toast"] = "Thanks for your review!";
                return RedirectToAction("User", "UserDashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting review");
                ModelState.AddModelError(string.Empty, "Failed to submit review.");
                return View("Review", vm);
            }
        }
    }
}
