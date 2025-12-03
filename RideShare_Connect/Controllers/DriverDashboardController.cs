using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideShareConnect.Data;
using RideShare_Connect.Models.RideManagement;
using RideShare_Connect.Models.VehicleManagement;
using RideShare_Connect.ViewModels;
using RideShare_Connect.DTOs;
using RideShare_Connect.Models.PaymentManagement;
using RideShare_Connect.Models.UserManagement;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace RideShare_Connect.Controllers
{
    public class DriverDashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<DriverDashboardController> _logger;

        public DriverDashboardController(ApplicationDbContext db, ILogger<DriverDashboardController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> Driver()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            var driver = await _db.Driver
                .Include(d => d.DriverProfile)
                .FirstOrDefaultAsync(d => d.DriverId == userId);
            var driverProfile = driver?.DriverProfile;
            var vehicles = await _db.Vehicles.Where(v => v.DriverId == userId).ToListAsync();
            var ratings = await _db.DriverRatings.Where(r => r.DriverId == userId)
                .OrderByDescending(r => r.Timestamp)
                .ToListAsync();

            var rideIds = await _db.Rides
                .Where(r => r.DriverId == userId)
                .Select(r => r.Id)
                .ToListAsync();

            var rides = new List<Ride>();
            List<RideBookingDetailsDto> acceptedBookings = new List<RideBookingDetailsDto>();

            if (rideIds.Any())
            {
                rides = await _db.Rides
                    .Where(r => rideIds.Contains(r.Id))
                    .Include(r => r.Vehicle)
                    .OrderByDescending(r => r.DepartureTime)
                    .ToListAsync();

                acceptedBookings = await _db.RideBookings
                    .Include(rb => rb.Ride)
                    .Where(rb => rideIds.Contains(rb.RideId))
                    .Select(rb => new RideBookingDetailsDto
                    {
                        BookingId = rb.Id,
                        RideId = rb.RideId,
                        PassengerId = rb.PassengerId,
                        BookedSeats = rb.BookedSeats,
                        PickupLocation = rb.PickupLocation,
                        DropLocation = rb.DropLocation,
                        DistanceKm = rb.DistanceKm,
                        BookingTime = rb.BookingTime,
                        Status = rb.Status,
                        Origin = rb.Ride.Origin,
                        Destination = rb.Ride.Destination,
                        DepartureTime = rb.Ride.DepartureTime,
                        PricePerSeat = rb.Ride.PricePerSeat
                    })
                    .ToListAsync();
            }
            var average = ratings.Count > 0 ? ratings.Average(r => r.Rating) : 0;

            var wallet = await _db.DriverWallets.FirstOrDefaultAsync(w => w.DriverId == userId);
            var walletBalance = wallet?.Balance ?? 0m;
            decimal totalEarnings = 0m;
            if (wallet != null)
            {
                totalEarnings = await _db.DriverWalletTransactions
                    .Where(t => t.DriverWalletId == wallet.Id &&
                                t.TxnType.ToLower() == "credit" &&
                                t.Status.ToLower() == "successful")
                    .SumAsync(t => (decimal?)t.Amount) ?? 0m;
            }

            // Calculate overview statistics
            var vehicleCount = vehicles.Count;
            var createdRidesCount = rides.Count;
            var acceptedRidesCount = acceptedBookings.Count(b => b.Status == "Pending");
            var activeRidesCount = acceptedBookings.Count(b => b.Status == "Ongoing");
            var cancelledRidesCount = acceptedBookings.Count(b => b.Status == "Cancelled");
            var totalRidesCount = acceptedBookings.Count;

            var bookingIds = acceptedBookings.Select(b => b.BookingId).Distinct().ToList();
            var passengerRatings = _db.UserRatings
                .Where(r => r.DriverId == userId && bookingIds.Contains(r.RideId)) // Note: Using RideId as BookingId reference might be tricky if not 1:1, but UserRating has RideId. 
                                                                                   // Wait, UserRating has RideId, DriverId, PassengerId. 
                                                                                   // We want to check if THIS driver has rated THIS passenger for THIS ride.
                                                                                   // The booking list has RideId.
                .AsEnumerable()
                .GroupBy(r => r.RideId) // Assuming one rating per ride per passenger, but here we might have multiple passengers per ride. 
                                        // Actually, we need to map by BookingId or (RideId, PassengerId).
                                        // The view iterates over bookings. Each booking has a unique ID.
                                        // Let's assume UserRating should probably link to BookingId to be precise, or we use (RideId, PassengerId) as key.
                                        // But the ViewModel uses Dictionary<int, int>. If key is BookingId, that's best.
                                        // However, UserRating currently has RideId. 
                                        // Let's use BookingId as the key in the dictionary, but we need to match it.
                                        // A booking is (RideId, PassengerId).
                .ToDictionary(g => g.Key, g => g.First().Rating); 
            
            // Correction: The UserRating model I created has RideId, DriverId, PassengerId.
            // The bookings list has BookingId, RideId, PassengerId.
            // I should probably fetch all ratings for these rides/passengers and map them to BookingId.
            
            var relevantRatings = _db.UserRatings
                .Where(r => r.DriverId == userId)
                .ToList();

            var passengerRatingsDict = new Dictionary<int, int>();
            foreach(var booking in acceptedBookings)
            {
                var rating = relevantRatings.FirstOrDefault(r => r.RideId == booking.RideId && r.PassengerId == booking.PassengerId);
                if (rating != null)
                {
                    passengerRatingsDict[booking.BookingId] = rating.Rating;
                }
            }

            var viewModel = new DriverDashboardViewModel
            {
                Driver = driver,
                DriverProfile = driverProfile,
                Vehicles = vehicles,
                CreatedRides = rides,
                AcceptedRideBookings = acceptedBookings,
                AverageRating = average,
                RecentRatings = ratings.Take(5),
                WalletBalance = walletBalance,
                TotalEarnings = totalEarnings,
                VehicleCount = vehicleCount,
                CreatedRidesCount = createdRidesCount,
                AcceptedRidesCount = acceptedRidesCount,
                ActiveRidesCount = activeRidesCount,
                CancelledRidesCount = cancelledRidesCount,
                TotalRidesCount = totalRidesCount,
                PassengerRatings = passengerRatingsDict
            };

            return View(viewModel);
        }
        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> CreateRide()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            var vehicles = await _db.Vehicles.Where(v => v.DriverId == userId).ToListAsync();

            var viewModel = new CreateRideViewModel
            {
                Vehicles = vehicles,
                DepartureDate = DateTime.Today
            };

            return View(viewModel);
        }

        [Authorize(Roles = "Driver")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRide(CreateRideViewModel model)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            if (!ModelState.IsValid)
            {
                model.Vehicles = await _db.Vehicles.Where(v => v.DriverId == userId).ToListAsync();
                return View(model);
            }

            var departure = model.DepartureDate.Date + model.DepartureTime;

            if (departure < DateTime.Now)
            {
                ModelState.AddModelError(string.Empty, "Departure time cannot be in the past.");
                model.Vehicles = await _db.Vehicles.Where(v => v.DriverId == userId).ToListAsync();
                return View(model);
            }

            var ride = new Ride
            {
                DriverId = userId,
                VehicleId = model.VehicleId,
                Origin = model.Origin,
                Destination = model.Destination,
                DepartureTime = departure,
                TotalSeats = model.Seats,
                AvailableSeats = model.Seats,
                PricePerSeat = model.PricePerSeat,
                DistanceKm = model.DistanceKm,
                Status = "Scheduled",
                IsRecurring = model.IsRecurring
            };

            _db.Rides.Add(ride);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Driver));
        }

        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> EditRide(int id)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            var ride = await _db.Rides.FirstOrDefaultAsync(r => r.Id == id && r.DriverId == userId);
            if (ride == null)
            {
                return NotFound();
            }

            var vehicles = await _db.Vehicles.Where(v => v.DriverId == userId).ToListAsync();

            var model = new EditRideViewModel
            {
                Id = ride.Id,
                Origin = ride.Origin,
                Destination = ride.Destination,
                DepartureDate = ride.DepartureTime.Date,
                DepartureTime = ride.DepartureTime.TimeOfDay,
                Seats = ride.TotalSeats,
                PricePerSeat = ride.PricePerSeat,
                VehicleId = ride.VehicleId,
                DistanceKm = ride.DistanceKm,
                IsRecurring = ride.IsRecurring,
                Vehicles = vehicles
            };

            return View(model);
        }

        [Authorize(Roles = "Driver")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRide(EditRideViewModel model)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            if (!ModelState.IsValid)
            {
                model.Vehicles = await _db.Vehicles.Where(v => v.DriverId == userId).ToListAsync();
                return View(model);
            }

            var ride = await _db.Rides.FirstOrDefaultAsync(r => r.Id == model.Id && r.DriverId == userId);
            if (ride == null)
            {
                return NotFound();
            }

            var departure = model.DepartureDate.Date + model.DepartureTime;
            if (departure < DateTime.Now)
            {
                ModelState.AddModelError(string.Empty, "Departure time cannot be in the past.");
                model.Vehicles = await _db.Vehicles.Where(v => v.DriverId == userId).ToListAsync();
                return View(model);
            }

            var bookedSeats = ride.TotalSeats - ride.AvailableSeats;
            ride.Origin = model.Origin;
            ride.Destination = model.Destination;
            ride.DepartureTime = departure;
            ride.VehicleId = model.VehicleId;
            ride.TotalSeats = model.Seats;
            ride.AvailableSeats = Math.Max(0, model.Seats - bookedSeats);
            ride.PricePerSeat = model.PricePerSeat;
            ride.DistanceKm = model.DistanceKm;
            ride.IsRecurring = model.IsRecurring;

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Driver));
        }

        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> ViewRide(int id)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            var ride = await _db.Rides.FirstOrDefaultAsync(r => r.Id == id && r.DriverId == userId);
            if (ride == null)
            {
                return NotFound();
            }

            var vehicles = await _db.Vehicles.Where(v => v.DriverId == userId).ToListAsync();

            var model = new EditRideViewModel
            {
                Id = ride.Id,
                Origin = ride.Origin,
                Destination = ride.Destination,
                DepartureDate = ride.DepartureTime.Date,
                DepartureTime = ride.DepartureTime.TimeOfDay,
                Seats = ride.TotalSeats,
                PricePerSeat = ride.PricePerSeat,
                VehicleId = ride.VehicleId,
                DistanceKm = ride.DistanceKm,
                IsRecurring = ride.IsRecurring,
                Vehicles = vehicles
            };

            return View(model);
        }

        [Authorize(Roles = "Driver")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRide(int id)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            var ride = await _db.Rides.FirstOrDefaultAsync(r => r.Id == id && r.DriverId == userId);
            if (ride == null)
            {
                return NotFound();
            }

            _db.Rides.Remove(ride);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Driver));
        }

        [Authorize(Roles = "Driver")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartRide(int id)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            var booking = await _db.RideBookings
                .Include(rb => rb.Ride)
                .FirstOrDefaultAsync(rb => rb.Id == id && rb.Ride.DriverId == userId);
            if (booking == null)
            {
                return NotFound();
            }

            booking.Status = "Ongoing";
            booking.Ride.Status = "Ongoing";

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Driver));
        }

        [Authorize(Roles = "Driver")]
        [HttpGet]
        public async Task<IActionResult> CompleteRide(int id)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            var booking = await _db.RideBookings
                .Include(rb => rb.Ride)
                .FirstOrDefaultAsync(rb => rb.Id == id && rb.Ride.DriverId == userId);
            if (booking == null || booking.Status != "Ongoing")
            {
                return NotFound();
            }

            var driver = await _db.Driver.Include(d => d.DriverProfile).FirstOrDefaultAsync(d => d.DriverId == userId);
            ViewBag.Driver = driver;

            var dto = new RideBookingDetailsDto
            {
                BookingId = booking.Id,
                RideId = booking.RideId,
                PassengerId = booking.PassengerId,
                BookedSeats = booking.BookedSeats,
                PickupLocation = booking.PickupLocation,
                DropLocation = booking.DropLocation,
                DistanceKm = booking.DistanceKm,
                BookingTime = booking.BookingTime,
                Status = booking.Status,
                Origin = booking.Ride.Origin,
                Destination = booking.Ride.Destination,
                DepartureTime = booking.Ride.DepartureTime,
                PricePerSeat = booking.Ride.PricePerSeat
            };

            return View(dto);
        }

        [Authorize(Roles = "Driver")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteRide(int id, bool confirm)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            if (!confirm)
            {
                return RedirectToAction(nameof(Driver));
            }

            var booking = await _db.RideBookings
                .Include(rb => rb.Ride)
                .FirstOrDefaultAsync(rb => rb.Id == id && rb.Ride.DriverId == userId);
            if (booking == null)
            {
                return NotFound();
            }

            // Calculate Fare Distribution
            decimal totalFare = booking.BookedSeats * booking.Ride.PricePerSeat * booking.DistanceKm;
            decimal driverShare = totalFare * 0.90m;
            decimal platformShare = totalFare * 0.10m;

            // Update Driver Wallet
            var driverWallet = await _db.DriverWallets.FirstOrDefaultAsync(w => w.DriverId == userId);
            if (driverWallet == null)
            {
                driverWallet = new DriverWallet
                {
                    DriverId = userId,
                    Balance = 0,
                    LastUpdated = DateTime.UtcNow
                };
                _db.DriverWallets.Add(driverWallet);
            }
            driverWallet.Balance += driverShare;
            driverWallet.LastUpdated = DateTime.UtcNow;

            // Create Transaction Record
            var transaction = new DriverWalletTransaction
            {
                DriverWalletId = driverWallet.Id,
                Amount = driverShare,
                TxnType = "Credit",
                TxnDate = DateTime.UtcNow,
                Description = $"Ride Earning for Booking #{booking.Id}",
                TransactionId = Guid.NewGuid().ToString(),
                PaymentMethod = "Wallet",
                Status = "Completed"
            };
            _db.DriverWalletTransactions.Add(transaction);

            // Update Platform Wallet
            var platformWallet = await _db.PlatformWallets.FirstOrDefaultAsync();
            if (platformWallet == null)
            {
                platformWallet = new PlatformWallet
                {
                    Balance = 0,
                    LastUpdated = DateTime.UtcNow
                };
                _db.PlatformWallets.Add(platformWallet);
            }
            platformWallet.Balance += platformShare;
            platformWallet.LastUpdated = DateTime.UtcNow;

            // Create Commission Record
            var commission = new Commission
            {
                BookingId = booking.Id,
                PlatformFee = platformShare,
                Percentage = 10, // 10%
                PaidOut = true, // Collected into wallet
                PayoutDate = DateTime.UtcNow
            };
            _db.Commissions.Add(commission);

            booking.Status = "Completed";
            booking.Ride.Status = "Completed";

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Driver));
        }

        [Authorize(Roles = "Driver")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRide(int id)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            var booking = await _db.RideBookings
                .Include(rb => rb.Ride)
                .FirstOrDefaultAsync(rb => rb.Id == id && rb.Ride.DriverId == userId);
            if (booking == null)
            {
                return NotFound();
            }

            booking.Status = "Cancelled";
            booking.Ride.AvailableSeats += booking.BookedSeats;

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Driver));
        }

        [Authorize(Roles = "Driver")]
        [HttpGet]
        public IActionResult AddVehicle()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }
            return View();
        }

        [Authorize(Roles = "Driver")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterVehicle(VehicleRegisterDto model)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }
            if (!ModelState.IsValid)
            {
                return RedirectToAction("AddVehicle", "DriverDashboard");
            }

            try
            {
                var vehicle = new Vehicle
                {
                    DriverId = userId,
                    CarMaker = model.CarMaker,
                    Model = model.CarModel,
                    VehicleType = model.VehicleType,
                    LicensePlate = model.LicensePlate,
                    VerificationStatus = "Pending",
                    Year = model.Year
                };

                _db.Vehicles.Add(vehicle);
                await _db.SaveChangesAsync();

                return RedirectToAction(nameof(Driver));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering vehicle");
                ModelState.AddModelError(string.Empty, "An error occurred while registering vehicle.");
            }

            return RedirectToAction("AddVehicle", "DriverDashboard");
        }

        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> EditVehicle(int id)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id && v.DriverId == userId);
            if (vehicle == null)
            {
                return NotFound();
            }

            var model = new DriverEditVehicleViewModel
            {
                Id = vehicle.Id,
                CarMaker = vehicle.CarMaker,
                Model = vehicle.Model,
                VehicleType = vehicle.VehicleType,
                LicensePlate = vehicle.LicensePlate,
                Year = vehicle.Year
            };

            return View(model);
        }

        [Authorize(Roles = "Driver")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditVehicle(DriverEditVehicleViewModel viewModel)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == viewModel.Id && v.DriverId == userId);
            if (vehicle == null)
            {
                return NotFound();
            }

            vehicle.CarMaker = viewModel.CarMaker;
            vehicle.Model = viewModel.Model;
            vehicle.VehicleType = viewModel.VehicleType;
            vehicle.LicensePlate = viewModel.LicensePlate;
            vehicle.Year = viewModel.Year;
            vehicle.VerificationStatus = "Pending";

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Driver));
        }

        [Authorize(Roles = "Driver")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteVehicle(int id)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id && v.DriverId == userId);
            if (vehicle == null)
            {
                return NotFound();
            }

            _db.Vehicles.Remove(vehicle);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Driver));
        }

        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> EditProfile()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            var driver = await _db.Driver
                .Include(d => d.DriverProfile)
                .FirstOrDefaultAsync(d => d.DriverId == userId);
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

        [Authorize(Roles = "Driver")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditDriverViewModel model)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            if (userId != model.DriverId)
            {
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var driver = await _db.Driver
                .Include(d => d.DriverProfile)
                .FirstOrDefaultAsync(d => d.DriverId == userId);
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
            else
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

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Driver));
        }

        [Authorize(Roles = "Driver")]
        [HttpGet]
        public async Task<IActionResult> RateUser(int bookingId)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            var booking = await _db.RideBookings
                .Include(rb => rb.Ride)
                .Include(rb => rb.User)
                    .ThenInclude(u => u.UserProfile)
                .FirstOrDefaultAsync(rb => rb.Id == bookingId && rb.Ride.DriverId == userId);

            if (booking == null)
            {
                return NotFound();
            }

            var existingRating = await _db.UserRatings
                .FirstOrDefaultAsync(r => r.RideId == booking.RideId && r.DriverId == userId && r.PassengerId == booking.PassengerId);

            var vm = new SubmitUserRatingVm
            {
                BookingId = bookingId,
                RideId = booking.RideId,
                DriverId = userId,
                PassengerId = booking.PassengerId,
                PassengerName = booking.User.UserProfile?.FullName ?? booking.User.Email,
                Rating = existingRating?.Rating ?? 0,
                Review = existingRating?.Review
            };

            return View(vm);
        }

        [Authorize(Roles = "Driver")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RateUser(SubmitUserRatingVm vm)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Login", "DriverAccount");
            }

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var existingRating = await _db.UserRatings
                .FirstOrDefaultAsync(r => r.RideId == vm.RideId && r.DriverId == userId && r.PassengerId == vm.PassengerId);

            if (existingRating != null)
            {
                existingRating.Rating = vm.Rating;
                existingRating.Review = vm.Review;
                existingRating.Timestamp = DateTime.UtcNow;
                _db.UserRatings.Update(existingRating);
            }
            else
            {
                var rating = new UserRating
                {
                    RideId = vm.RideId,
                    DriverId = userId,
                    PassengerId = vm.PassengerId,
                    Rating = vm.Rating,
                    Review = vm.Review,
                    Timestamp = DateTime.UtcNow
                };
                _db.UserRatings.Add(rating);
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Driver));
        }
    }
}
