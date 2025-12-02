using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideShareConnect.Data;
using RideShare_Connect.Models.PaymentManagement;
using RideShare_Connect.Models.UserManagement;
using RideShare_Connect.Models.VehicleManagement;
using RideShare_Connect.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace RideShare_Connect.Controllers
{
    [Authorize(Roles = "Driver")]
    public class DriverFinancialDashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;

        public DriverFinancialDashboardController(ApplicationDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public async Task<IActionResult> DriverFinance()
        {
            var driverIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(driverIdClaim))
            {
                return RedirectToAction("Login", "DriverAccount");
            }
            var driverId = int.Parse(driverIdClaim);

            var driver = await _db.Driver
                .Include(d => d.DriverProfile)
                .FirstOrDefaultAsync(d => d.DriverId == driverId);

            var wallet = await _db.DriverWallets.FirstOrDefaultAsync(w => w.DriverId == driverId);
            var balance = wallet?.Balance ?? 0m;

            var walletTransactions = new List<WalletTransaction>();
            if (wallet != null)
            {
                var driverTransactions = await _db.DriverWalletTransactions
                    .Where(t => t.DriverWalletId == wallet.Id)
                    .OrderByDescending(t => t.TxnDate)
                    .ToListAsync();

                walletTransactions = driverTransactions.Select(t => new WalletTransaction
                {
                    Id = t.Id,
                    WalletId = t.DriverWalletId,
                    Amount = t.Amount,
                    TxnType = t.TxnType,
                    TxnDate = t.TxnDate,
                    Description = t.Description,
                    TransactionId = t.TransactionId,
                    PaymentMethod = t.PaymentMethod,
                    Status = t.Status
                }).ToList();
            }

            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var totalEarnings = walletTransactions
                .Where(t => t.Status.ToLower() == "completed" &&
                            t.TxnType.ToLower() == "credit" &&
                            t.TxnDate >= startOfMonth)
                .Sum(t => t.Amount);

            UserProfile profile = null;
            if (driver != null)
            {
                profile = new UserProfile
                {
                    UserId = driver.DriverId,
                    FullName = driver.FullName,
                    ProfilePicture = null
                };
            }

            // Fetch Refunds associated with this driver's rides
            var refunds = await _db.Refunds
                .Include(r => r.Payment)
                    .ThenInclude(p => p.Booking)
                        .ThenInclude(b => b.Ride)
                .Include(r => r.Payment)
                    .ThenInclude(p => p.User) // Passenger
                        .ThenInclude(u => u.UserProfile)
                .Where(r => r.Payment.Booking.Ride.DriverId == driverId)
                .OrderByDescending(r => r.ProcessedAt)
                .ToListAsync();

            var model = new FinanceViewModel
            {
                WalletBalance = balance,
                TotalSpentThisMonth = totalEarnings, // Reusing property for Earning
                TotalSavedOnRides = 0,
                TransactionHistory = walletTransactions,
                RecentTransactions = walletTransactions.Take(3).ToList(),
                Profile = profile,
                Refunds = refunds
            };

            ViewBag.RazorpayKey = _config["PaymentGateway:ApiKey"];

            return View(model);
        }

        public async Task<IActionResult> TransactionDetails(int id)
        {
            var driverIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(driverIdClaim) || !int.TryParse(driverIdClaim, out var driverId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            var transaction = await _db.DriverWalletTransactions
                .Include(t => t.DriverWallet)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transaction == null || transaction.DriverWallet.DriverId != driverId)
            {
                return NotFound();
            }

            var driver = await _db.Driver.Include(d => d.DriverProfile).FirstOrDefaultAsync(d => d.DriverId == driverId);
            ViewBag.Driver = driver;

            // Check if transaction is for a ride earning
            if (transaction.Description != null && transaction.Description.StartsWith("Ride Earning for Booking #"))
            {
                var bookingIdStr = transaction.Description.Replace("Ride Earning for Booking #", "");
                if (int.TryParse(bookingIdStr, out var bookingId))
                {
                    var booking = await _db.RideBookings
                        .Include(b => b.Ride)
                        .Include(b => b.User)
                        .ThenInclude(u => u.UserProfile) // Include UserProfile for FullName
                        .FirstOrDefaultAsync(b => b.Id == bookingId);

                    if (booking != null)
                    {
                        ViewBag.RideBooking = booking;
                        ViewBag.TotalFare = booking.BookedSeats * booking.Ride.PricePerSeat * booking.DistanceKm;
                    }
                }
            }

            return View(transaction);
        }

        public async Task<IActionResult> RefundDetails(int id)
        {
            var driverIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(driverIdClaim) || !int.TryParse(driverIdClaim, out var driverId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            var refund = await _db.Refunds
                .Include(r => r.Payment)
                    .ThenInclude(p => p.Booking)
                        .ThenInclude(b => b.Ride)
                .Include(r => r.Payment)
                    .ThenInclude(p => p.User) // Passenger
                        .ThenInclude(u => u.UserProfile)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (refund == null || refund.Payment.Booking.Ride.DriverId != driverId)
            {
                return NotFound();
            }

            var driver = await _db.Driver.Include(d => d.DriverProfile).FirstOrDefaultAsync(d => d.DriverId == driverId);
            ViewBag.Driver = driver;

            return View(refund);
        }
    }
}
