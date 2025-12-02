using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideShareConnect.Data;
using RideShare_Connect.Models.VehicleManagement;
using RideShare_Connect.ViewModels;
using System.Security.Claims;
using System.Threading.Tasks;
using RideShare_Connect.Models.UserManagement;
using RideShare_Connect.Models.AdminManagement;
using RideShare_Connect.DTOs;
using RideShare_Connect.Models.RideManagement;
using RideShare_Connect.Models.PaymentManagement;

namespace RideShare_Connect.Controllers
{
    public class AdminDashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        public AdminDashboardController(ApplicationDbContext db)
        {
            _db = db;
        }

        public IActionResult Admin()
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            if (!User.Identity.IsAuthenticated || role != "Admin")
            {
                return RedirectToAction("AdminLogin", "AdminAccount");
            }

            // Ensure Platform Wallet exists (Singleton)
            var platformWallet = _db.PlatformWallets.FirstOrDefault();
            if (platformWallet == null)
            {
                // Initialize with existing commissions if wallet is new
                var totalCommission = _db.Commissions.Sum(c => (decimal?)c.PlatformFee) ?? 0;
                platformWallet = new RideShare_Connect.Models.PaymentManagement.PlatformWallet
                {
                    Balance = totalCommission,
                    LastUpdated = DateTime.UtcNow
                };
                _db.PlatformWallets.Add(platformWallet);
                _db.SaveChanges();
            }

            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var admin = _db.Admins.FirstOrDefault(a => a.Id == adminId);

            var viewModel = new AdminDashboardViewModel
            {
                AdminName = !string.IsNullOrEmpty(admin?.FullName) ? admin.FullName : admin?.Username,
                AdminProfilePicUrl = !string.IsNullOrEmpty(admin?.ProfilePicUrl) ? admin.ProfilePicUrl : "https://images.pexels.com/photos/1681010/pexels-photo-1681010.jpeg?auto=compress&cs=tinysrgb&w=1260&h=750&dpr=1",
                TotalRevenue = _db.Payments.Where(p => p.Status == "Completed").Sum(p => (decimal?)p.Amount) ?? 0,
                PlatformWalletBalance = platformWallet.Balance,
                TotalUsers = _db.Users.Count(),
                RidesCompleted = _db.Rides.Count(r => r.Status == "Completed"),
                OpenReports = _db.UserReports.Count(r => r.Status == "Pending"),
                Users = _db.Users.Include(u => u.UserProfile).ToList(),
                Drivers = _db.Driver.Include(d => d.DriverProfile).ToList(),
                Vehicles = _db.Vehicles.Include(v => v.Driver).ToList(),
                Rides = _db.Rides.Include(r => r.Driver).ToList(),
                RideBookings = _db.RideBookings
                    .Include(rb => rb.User)
                    .ThenInclude(u => u.UserProfile)
                    .ToList(),
                Reports = _db.UserReports.Include(r => r.ReportingUser).Include(r => r.ReportedUser).OrderByDescending(r => r.CreatedAt).ToList(),
                Payments = _db.Payments.OrderByDescending(p => p.PaymentDate).Take(50).ToList(),
                Refunds = _db.Refunds.Include(r => r.Payment).ThenInclude(p => p.User).ThenInclude(u => u.UserProfile).OrderByDescending(r => r.ProcessedAt).ToList(),
                SystemConfig = new SystemConfigViewModel() // Placeholder for now
            };

            // Prepare Chart Data (Last 7 Months)
            var chartLabels = new List<string>();
            var userGrowthData = new List<int>();
            var revenueData = new List<decimal>();

            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.UtcNow.AddMonths(-i);
                var monthStart = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

                chartLabels.Add(date.ToString("MMM"));

                // User Growth
                var userCount = _db.Users.Count(u => u.CreatedAt >= monthStart && u.CreatedAt <= monthEnd);
                userGrowthData.Add(userCount);

                // Revenue
                var revenue = _db.Payments
                    .Where(p => p.Status == "Completed" && p.PaymentDate >= monthStart && p.PaymentDate <= monthEnd)
                    .Sum(p => (decimal?)p.Amount) ?? 0;
                revenueData.Add(revenue);
            }

            viewModel.ChartLabels = chartLabels;
            viewModel.UserGrowthData = userGrowthData;
            viewModel.RevenueData = revenueData;

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            if (!User.Identity.IsAuthenticated || role != "Admin")
            {
                return RedirectToAction("AdminLogin", "AdminAccount");
            }

            var user = await _db.Users.Include(u => u.UserProfile).FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            var model = new EditUserViewModel
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.UserProfile?.FullName,
                UserType = user.UserType,
                AccountStatus = user.AccountStatus
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            if (!User.Identity.IsAuthenticated || role != "Admin")
            {
                return RedirectToAction("AdminLogin", "AdminAccount");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _db.Users.Include(u => u.UserProfile).FirstOrDefaultAsync(u => u.Id == model.Id);
            if (user == null)
            {
                return NotFound();
            }

            user.Email = model.Email;
            user.UserType = model.UserType;
            user.AccountStatus = model.AccountStatus;

            if (user.UserProfile != null)
            {
                user.UserProfile.FullName = model.FullName;
            }
            else if (!string.IsNullOrWhiteSpace(model.FullName))
            {
                user.UserProfile = new UserProfile
                {
                    FullName = model.FullName,
                    UserId = user.Id
                };
            }

            await _db.SaveChangesAsync();

            return RedirectToAction("Admin");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            if (!User.Identity.IsAuthenticated || role != "Admin")
            {
                return RedirectToAction("AdminLogin", "AdminAccount");
            }

            var user = await _db.Users.Include(u => u.UserProfile).FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            if (user.UserProfile != null)
            {
                _db.UserProfiles.Remove(user.UserProfile);
            }

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            return RedirectToAction("Admin");
        }

        [HttpGet]
        public async Task<IActionResult> EditDriver(int id)
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            if (!User.Identity.IsAuthenticated || role != "Admin")
            {
                return RedirectToAction("AdminLogin", "AdminAccount");
            }

            var driver = await _db.Driver.Include(d => d.DriverProfile)
                                .FirstOrDefaultAsync(d => d.DriverId == id);
            if (driver == null)
            {
                return NotFound();
            }

            var model = new EditDriverViewModel
            {
                DriverId = driver.DriverId,
                FullName = driver.FullName,
                Email = driver.Email,
                PhoneNumber = driver.PhoneNumber,
                LicenseNumber = driver.DriverProfile?.LicenseNumber,
                BackgroundCheckStatus = driver.DriverProfile?.BackgroundCheckStatus,
                DrivingExperienceYears = driver.DriverProfile?.DrivingExperienceYears ?? 0,
                DOB = driver.DriverProfile?.DOB ?? DateTime.MinValue
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDriver(EditDriverViewModel model)
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            if (!User.Identity.IsAuthenticated || role != "Admin")
            {
                return RedirectToAction("AdminLogin", "AdminAccount");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var driver = await _db.Driver.Include(d => d.DriverProfile)
                                .FirstOrDefaultAsync(d => d.DriverId == model.DriverId);
            if (driver == null)
            {
                return NotFound();
            }

            driver.FullName = model.FullName;
            driver.Email = model.Email;
            driver.PhoneNumber = model.PhoneNumber;

            if (driver.DriverProfile != null)
            {
                driver.DriverProfile.LicenseNumber = model.LicenseNumber;
                driver.DriverProfile.BackgroundCheckStatus = model.BackgroundCheckStatus;
                driver.DriverProfile.DrivingExperienceYears = model.DrivingExperienceYears;
                driver.DriverProfile.DOB = model.DOB;
            }
            else if (!string.IsNullOrWhiteSpace(model.LicenseNumber))
            {
                driver.DriverProfile = new DriverProfile
                {
                    DriverId = driver.DriverId,
                    LicenseNumber = model.LicenseNumber,
                    BackgroundCheckStatus = model.BackgroundCheckStatus,
                    DrivingExperienceYears = model.DrivingExperienceYears,
                    DOB = model.DOB
                };
            }

            await _db.SaveChangesAsync();

            return RedirectToAction("Admin");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDriver(int id)
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            if (!User.Identity.IsAuthenticated || role != "Admin")
            {
                return RedirectToAction("AdminLogin", "AdminAccount");
            }

            var driver = await _db.Driver.Include(d => d.DriverProfile)
                                 .FirstOrDefaultAsync(d => d.DriverId == id);
            if (driver == null)
            {
                return NotFound();
            }

            var vehicles = await _db.Vehicles.Where(v => v.DriverId == id).ToListAsync();
            if (vehicles.Any())
            {
                _db.Vehicles.RemoveRange(vehicles);
            }

            if (driver.DriverProfile != null)
            {
                _db.DriverProfiles.Remove(driver.DriverProfile);
            }

            _db.Driver.Remove(driver);
            await _db.SaveChangesAsync();

            return RedirectToAction("Admin");
        }

        [HttpGet]
        public async Task<IActionResult> EditVehicle(int id)
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            if (!User.Identity.IsAuthenticated || role != "Admin")
            {
                return RedirectToAction("AdminLogin", "AdminAccount");
            }

            var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id);
            if (vehicle == null)
            {
                return NotFound();
            }

            var model = new EditVehicleViewModel
            {
                Id = vehicle.Id,
                DriverId = vehicle.DriverId,
                CarMaker = vehicle.CarMaker,
                Model = vehicle.Model,
                VehicleType = vehicle.VehicleType,
                LicensePlate = vehicle.LicensePlate,
                VerificationStatus = vehicle.VerificationStatus,
                Year = vehicle.Year
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditVehicle(EditVehicleViewModel viewModel)
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            if (!User.Identity.IsAuthenticated || role != "Admin")
            {
                return RedirectToAction("AdminLogin", "AdminAccount");
            }

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == viewModel.Id);
            if (vehicle == null)
            {
                return NotFound();
            }

            vehicle.DriverId = viewModel.DriverId;
            vehicle.CarMaker = viewModel.CarMaker;
            vehicle.Model = viewModel.Model;
            vehicle.VehicleType = viewModel.VehicleType;
            vehicle.LicensePlate = viewModel.LicensePlate;
            vehicle.VerificationStatus = viewModel.VerificationStatus;
            vehicle.Year = viewModel.Year;

            await _db.SaveChangesAsync();

            return RedirectToAction("Admin");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteVehicle(int id)
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            if (!User.Identity.IsAuthenticated || role != "Admin")
            {
                return RedirectToAction("AdminLogin", "AdminAccount");
            }

            var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id);
            if (vehicle == null)
            {
                return NotFound();
            }

            _db.Vehicles.Remove(vehicle);
            await _db.SaveChangesAsync();

            return RedirectToAction("Admin");
        }

        [HttpGet]
        public async Task<IActionResult> RideDetails(int id)
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            if (!User.Identity.IsAuthenticated || role != "Admin")
            {
                return RedirectToAction("AdminLogin", "AdminAccount");
            }

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRide(int id)
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            if (!User.Identity.IsAuthenticated || role != "Admin")
            {
                return RedirectToAction("AdminLogin", "AdminAccount");
            }

            var ride = await _db.Rides.FirstOrDefaultAsync(r => r.Id == id);
            if (ride == null)
            {
                return NotFound();
            }

            ride.Status = "Cancelled";
            await _db.SaveChangesAsync();

            return RedirectToAction("Admin");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRefund(int id)
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            if (!User.Identity.IsAuthenticated || role != "Admin")
            {
                return RedirectToAction("AdminLogin", "AdminAccount");
            }

            var refund = await _db.Refunds.Include(r => r.Payment).FirstOrDefaultAsync(r => r.Id == id);
            if (refund == null)
            {
                return NotFound();
            }

            if (refund.RefundStatus != "Processing")
            {
                TempData["ErrorMessage"] = "Refund request is not pending.";
                return RedirectToAction("Admin");
            }

            // 1. Update Refund Status
            refund.RefundStatus = "Completed";
            _db.Refunds.Update(refund);

            // 2. Refund to User Wallet
            var userWallet = await _db.Wallets.FirstOrDefaultAsync(w => w.UserId == refund.Payment.UserId);
            if (userWallet != null)
            {
                userWallet.Balance += refund.Amount;
                _db.Wallets.Update(userWallet);

                _db.WalletTransactions.Add(new WalletTransaction
                {
                    WalletId = userWallet.Id,
                    Amount = refund.Amount,
                    TxnType = "Credit",
                    TxnDate = DateTime.UtcNow,
                    Description = $"Refund Approved for Request REF{refund.Id + 1000}",
                    TransactionId = Guid.NewGuid().ToString(),
                    PaymentMethod = "Wallet",
                    Status = "Completed"
                });
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Refund approved and amount credited to user wallet.";
            return RedirectToAction("Admin");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRefund(int id, string reason)
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            if (!User.Identity.IsAuthenticated || role != "Admin")
            {
                return RedirectToAction("AdminLogin", "AdminAccount");
            }

            var refund = await _db.Refunds.FirstOrDefaultAsync(r => r.Id == id);
            if (refund == null)
            {
                return NotFound();
            }

            if (refund.RefundStatus != "Processing")
            {
                TempData["ErrorMessage"] = "Refund request is not pending.";
                return RedirectToAction("Admin");
            }

            refund.RefundStatus = "Rejected";
            // Append rejection reason if provided? Or replace? 
            // User asked to "reject with reason". I'll append it to the existing reason or just rely on status.
            // The Refund model has a 'Reason' field which is the *request* reason.
            // I should probably not overwrite the user's reason.
            // But I don't have a 'RejectionReason' field.
            // I'll append it to the Reason field for now: "User Reason... [Admin Rejected: Reason]"
            if (!string.IsNullOrWhiteSpace(reason))
            {
                refund.Reason += $" [Admin Rejected: {reason}]";
            }

            _db.Refunds.Update(refund);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Refund request rejected.";
            return RedirectToAction("Admin");
        }
    }
}
