using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using RideShare_Connect.DTOs;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using RideShareConnect.Data;
using Microsoft.AspNetCore.Identity;
using RideShare_Connect.Models.VehicleManagement;
using Microsoft.EntityFrameworkCore;

namespace RideShare_Connect.Controllers
{
    public class DriverAccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<Driver> _passwordHasher;
        private readonly ILogger<DriverAccountController> _logger;

        public DriverAccountController(ApplicationDbContext context, IPasswordHasher<Driver> passwordHasher, ILogger<DriverAccountController> logger)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(DriverRegisterDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (await _context.Driver.AnyAsync(d => d.Email == model.Email))
            {
                ModelState.AddModelError("Email", "Driver with this email already exists.");
                return View(model);
            }

            var driver = new Driver
            {
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                FullName = model.FullName
            };
            driver.PasswordHash = _passwordHasher.HashPassword(driver, model.Password);

            _context.Driver.Add(driver);
            await _context.SaveChangesAsync();

            var driverProfile = new DriverProfile
            {
                DriverId = driver.DriverId,
                LicenseNumber = model.LicenseNumber,
                BackgroundCheckStatus = "Pending",
                DrivingExperienceYears = model.DrivingExperienceYears,
                DOB = model.DOB
            };
            _context.DriverProfiles.Add(driverProfile);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Driver registered successfully! You can now log in.";
            return RedirectToAction("Login");
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginDto model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var driver = await _context.Driver.FirstOrDefaultAsync(d => d.Email == model.Email);
            if (driver == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(model);
            }

            var result = _passwordHasher.VerifyHashedPassword(driver, driver.PasswordHash, model.Password);
            if (result == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, driver.DriverId.ToString()),
                new Claim(ClaimTypes.Email, driver.Email),
                new Claim(ClaimTypes.Role, "Driver"),
                new Claim(ClaimTypes.Name, driver.FullName)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(60)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            if (returnUrl != null && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Driver", "DriverDashboard");
        }

        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
        {
            if (!ModelState.IsValid) return View(dto);
            // Mock implementation
            TempData["SuccessMessage"] = "If the email exists, a reset link has been sent (Mock).";
            return RedirectToAction("Login");
        }

        public IActionResult ResetPassword()
        {
             // Mock implementation
            return View(new ResetPasswordDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
        {
             if (!ModelState.IsValid) return View(dto);
             // Mock implementation
             TempData["SuccessMessage"] = "Password reset successful (Mock).";
             return RedirectToAction("Login");
        }
    }
}
