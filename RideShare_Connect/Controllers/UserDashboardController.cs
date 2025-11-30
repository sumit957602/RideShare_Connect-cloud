using Microsoft.AspNetCore.Mvc;
using RideShare_Connect.Models.UserManagement;
using RideShare_Connect.ViewModels;
using RideShareConnect.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using RideShare_Connect.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using RideShare_Connect.Models.RideManagement;
using RideShare_Connect.Models.PaymentManagement;
using System.Data;

namespace RideShare_Connect.Controllers
{
    public class UserDashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<UserDashboardController> _logger;
        private readonly IConfiguration _config;

        public UserDashboardController(ApplicationDbContext db, ILogger<UserDashboardController> logger, IConfiguration config)
        {
            _db = db;
            _logger = logger;
            _config = config;
        }

        public IActionResult User()
        {
            var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return RedirectToAction("Login", "UserAccount");
            }

            var profile = _db.UserProfiles.FirstOrDefault(u => u.UserId == userId);

            var bookingsQuery = _db.RideBookings
                .Include(rb => rb.Ride)
                    .ThenInclude(r => r.Driver)
                .Include(rb => rb.Ride)
                    .ThenInclude(r => r.Vehicle)
                .Where(rb => rb.PassengerId == userId);

            var bookings = bookingsQuery
                .OrderByDescending(rb => rb.BookingTime)
                .ToList();

            var totalSpent = bookings.Sum(rb => rb.BookedSeats * rb.Ride.PricePerSeat);

            var wallet = _db.Wallets.FirstOrDefault(w => w.UserId == userId);
            var walletBalance = wallet?.Balance ?? 0;

            var rating = _db.DriverRatings
                .Where(r => r.DriverId == userId)
                .Average(r => (double?)r.Rating) ?? 0;

            var availableRides = _db.Rides
                .Include(r => r.Driver)
                .Include(r => r.Vehicle)
                .Where(r => r.AvailableSeats > 0 && r.DepartureTime >= DateTime.Now && r.Status!="Completed")
                .OrderBy(r => r.DepartureTime)
                .ToList();

            var bookingIds = bookings.Select(b => b.Id).ToList();
            var paymentModes = _db.Payments
                .Include(p => p.PaymentMethod)
                .Where(p => bookingIds.Contains(p.BookingId))
                .OrderByDescending(p => p.PaymentDate)
                .AsEnumerable()
                .GroupBy(p => p.BookingId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var payment = g.FirstOrDefault();
                        return payment?.PaymentMethod?.CardType ?? "Cash";
                    });

            var activeRidesCount = bookings.Count(b => b.Status == "Ongoing");
            var cancelledRidesCount = bookings.Count(b => b.Status == "Cancelled");
            var completedRidesCount = bookings.Count(b => b.Status == "Completed");
            var totalBookingsCount = bookings.Count;

            var model = new UserDashboardViewModel
            {
                Profile = profile,
                RidesBooked = bookings.Count,
                TotalSpent = totalSpent,
                WalletBalance = walletBalance,
                Rating = rating,
                Bookings = bookings,
                RecentBookings = bookings.Take(5).ToList(),
                AvailableRides = availableRides,
                PaymentModes = paymentModes,
                ActiveRidesCount = activeRidesCount,
                CancelledRidesCount = cancelledRidesCount,
                CompletedRidesCount = completedRidesCount,
                TotalBookingsCount = totalBookingsCount
            };

            return View(model);
        }

        public async Task<IActionResult> UserProfile()
        {
            var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return RedirectToAction("Login", "UserAccount");
            }

            var user = await _db.Users
                                    .Include(u => u.UserProfile)
                                    .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.UserProfile == null)
            {
                return View(new UserProfileDto());
            }

            var profileDto = new UserProfileDto
            {
                Id = user.UserProfile.Id,
                UserId = user.UserProfile.UserId,
                FullName = user.UserProfile.FullName,
                ProfilePicture = user.UserProfile.ProfilePicture,
                EmergencyContact = user.UserProfile.EmergencyContact,
                Bio = user.UserProfile.Bio,
                PhoneNumber = user.PhoneNumber
            };

            return View(profileDto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserProfile(UserProfileDto profile)
        {
            var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return RedirectToAction("Login", "UserAccount");
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return NotFound();
            }

            var userProfile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (userProfile == null)
            {
                userProfile = new UserProfile { UserId = userId };
                _db.UserProfiles.Add(userProfile);
            }

            if (profile.FullName != null) userProfile.FullName = profile.FullName;
            if (profile.ProfilePicture != null) userProfile.ProfilePicture = profile.ProfilePicture;
            if (profile.EmergencyContact != null) userProfile.EmergencyContact = profile.EmergencyContact;
            if (profile.Bio != null) userProfile.Bio = profile.Bio;
            if (profile.PhoneNumber != null) user.PhoneNumber = profile.PhoneNumber;

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(UserProfile));
        }

        public IActionResult PrivacySettings()
        {
            var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return RedirectToAction("Login", "UserAccount");
            }

            var profile = _db.UserProfiles.FirstOrDefault(u => u.UserId == userId);
            return View(profile);
        }

        public IActionResult DeleteAccount()
        {
            var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return RedirectToAction("Login", "UserAccount");
            }
            return View(new DeleteAccountDto());
        }

        public async Task<IActionResult> RideDetails(int id)
        {
            var ride = await _db.Rides
                .Include(r => r.Driver)
                .Include(r => r.Vehicle)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (ride == null)
            {
                return NotFound();
            }

            var riders = await _db.RideBookings
                .Include(rb => rb.User)
                .ThenInclude(u => u.UserProfile)
                .Where(rb => rb.RideId == id)
                .Select(rb => rb.User.UserProfile != null ? rb.User.UserProfile.FullName : rb.User.Email)
                .ToListAsync();

            var viewModel = new RideDetailsViewModel
            {
                Ride = ride,
                Riders = riders
            };

            return View(viewModel);
        }

        public async Task<IActionResult> BookRide(int id)
        {
            var ride = await _db.Rides
                .Include(r => r.Driver)
                .Include(r => r.Vehicle)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (ride == null)
            {
                return NotFound();
            }

            ViewBag.RazorpayKey = _config["PaymentGateway:ApiKey"] ?? "rzp_test_R5NMyugFaCywDS";
            return View(ride);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookRide(int id, [FromBody] RideBookingRequestDto dto)
        {
            try
            {
                System.IO.File.AppendAllText("debug_log.txt", $"\n[{DateTime.Now}] BookRide called. ID: {id}");
                
                var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                {
                    System.IO.File.AppendAllText("debug_log.txt", " - Unauthorized: User ID not found.");
                    return Unauthorized();
                }

                if (dto == null)
                {
                    System.IO.File.AppendAllText("debug_log.txt", " - BadRequest: DTO is null.");
                    return BadRequest("Invalid booking request.");
                }

                if (!ModelState.IsValid)
                {
                    var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                    System.IO.File.AppendAllText("debug_log.txt", $" - Validation Failed: {errors}");
                    return BadRequest($"Validation failed: {errors}");
                }

                System.IO.File.AppendAllText("debug_log.txt", " - Starting Transaction.");

                // Direct DB Logic (Transaction)
                await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

                try
                {
                    var ride = await _db.Rides.FirstOrDefaultAsync(r => r.Id == id);
                    if (ride == null) 
                    {
                        System.IO.File.AppendAllText("debug_log.txt", " - Ride not found.");
                        return NotFound("Ride not found.");
                    }

                    if (ride.AvailableSeats < dto.NumPersons)
                    {
                        System.IO.File.AppendAllText("debug_log.txt", " - Not enough seats.");
                        return BadRequest("Not enough seats available.");
                    }

                    // Calculate Fare
                    var totalFare = ride.PricePerSeat * dto.NumPersons * dto.DistanceKm;

                    // Payment Logic
                    if (dto.PaymentMode == "Wallet")
                    {
                        var userWallet = await _db.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
                        if (userWallet == null || userWallet.Balance < totalFare)
                        {
                            System.IO.File.AppendAllText("debug_log.txt", " - Insufficient wallet balance.");
                            return BadRequest("Insufficient wallet balance.");
                        }
                        userWallet.Balance -= totalFare;
                        _db.Wallets.Update(userWallet);
                        
                        _db.WalletTransactions.Add(new WalletTransaction
                        {
                            Wallet = userWallet,
                            Amount = totalFare,
                            TxnType = "Debit",
                            TxnDate = DateTime.UtcNow,
                            Description = "Ride booking",
                            TransactionId = Guid.NewGuid().ToString(),
                            PaymentMethod = "Wallet",
                            Status = "Completed"
                        });
                    }

                    // Create Booking
                    var booking = new RideBooking
                    {
                        RideId = id,
                        PassengerId = userId,
                        BookedSeats = dto.NumPersons,
                        PickupLocation = dto.PickupLocation,
                        DropLocation = dto.DropLocation,
                        DistanceKm = dto.DistanceKm,
                        BookingTime = DateTime.UtcNow,
                        Status = "Pending"
                    };

                    _db.RideBookings.Add(booking);
                    ride.AvailableSeats -= dto.NumPersons;
                    _db.Rides.Update(ride);
                    
                    // Create Payment Record
                    var payment = new Payment
                    {
                        UserId = userId,
                        Booking = booking,
                        Amount = totalFare,
                        PaymentMode = dto.PaymentMode,
                        PaymentDate = DateTime.UtcNow,
                        Status = dto.PaymentMode == "Wallet" ? "Completed" : "Pending"
                    };
                    _db.Payments.Add(payment);

                    // Update Transaction Summary
                    var transactionSummary = await _db.UserTransactionSummaries
                        .OrderByDescending(t => t.TransactionId)
                        .FirstOrDefaultAsync(t => t.UserId == userId);

                    if (transactionSummary == null)
                    {
                        transactionSummary = new UserTransactionSummary
                        {
                            RideId = ride.Id,
                            DriverId = ride.DriverId,
                            UserId = userId,
                            TotalAmount = totalFare,
                            TotalTransactionAmount = dto.PaymentMode == "Cash" ? 0 : totalFare,
                        };
                        _db.UserTransactionSummaries.Add(transactionSummary);
                    }
                    else
                    {
                        transactionSummary.RideId = ride.Id;
                        transactionSummary.DriverId = ride.DriverId;
                        transactionSummary.TotalAmount += totalFare;
                        if (dto.PaymentMode != "Cash")
                            transactionSummary.TotalTransactionAmount += totalFare;
                    }

                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();
                    
                    System.IO.File.AppendAllText("debug_log.txt", " - Success.");
                    return Ok(new { message = "Ride booked successfully!", bookingId = booking.Id });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    System.IO.File.AppendAllText("debug_log.txt", $" - Transaction Error: {ex}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText("debug_log.txt", $" - Outer Error: {ex}");
                return StatusCode(500, ex.Message);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRide(int id)
        {
            try
            {
                var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized();
                }

                System.IO.File.AppendAllText("debug_log.txt", $"\n[{DateTime.Now}] CancelRide called. BookingID: {id}");

                await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

                try
                {
                    var booking = await _db.RideBookings
                        .Include(b => b.Ride)
                        .FirstOrDefaultAsync(b => b.Id == id && b.PassengerId == userId);

                    if (booking == null)
                    {
                        System.IO.File.AppendAllText("debug_log.txt", " - Booking not found or unauthorized.");
                        return NotFound("Booking not found.");
                    }

                    if (booking.Status == "Cancelled" || booking.Status == "Completed")
                    {
                        System.IO.File.AppendAllText("debug_log.txt", $" - Cannot cancel. Status: {booking.Status}");
                        return BadRequest("Cannot cancel this ride.");
                    }

                    // 1. Update Booking Status
                    booking.Status = "Cancelled";
                    _db.RideBookings.Update(booking);

                    // 2. Refund to Wallet
                    var refundAmount = booking.Ride.PricePerSeat * booking.BookedSeats * booking.DistanceKm; // Calculate based on deduction logic
                    var userWallet = await _db.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
                    
                    if (userWallet != null)
                    {
                        userWallet.Balance += refundAmount;
                        _db.Wallets.Update(userWallet);

                        _db.WalletTransactions.Add(new WalletTransaction
                        {
                            WalletId = userWallet.Id,
                            Amount = refundAmount,
                            TxnType = "Credit",
                            TxnDate = DateTime.UtcNow,
                            Description = $"Refund for Ride #{booking.RideId}",
                            TransactionId = Guid.NewGuid().ToString(),
                            PaymentMethod = "Wallet",
                            Status = "Completed"
                        });
                    }

                    // 3. Release Seats
                    var ride = booking.Ride;
                    if (ride != null)
                    {
                        ride.AvailableSeats += booking.BookedSeats;
                        _db.Rides.Update(ride);
                    }

                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    System.IO.File.AppendAllText("debug_log.txt", " - Cancelled successfully.");
                    return RedirectToAction(nameof(User));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    System.IO.File.AppendAllText("debug_log.txt", $" - Transaction Error: {ex}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling ride");
                return StatusCode(500, "Internal server error.");
            }
        }
    }
}
