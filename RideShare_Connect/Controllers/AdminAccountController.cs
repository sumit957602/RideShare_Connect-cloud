using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using RideShare_Connect.DTOs;
using System.Security.Claims;
using System.Text.Json;
using RideShareConnect.Data;
using Microsoft.AspNetCore.Identity;
using RideShare_Connect.Models.AdminManagement;
using Microsoft.EntityFrameworkCore;

namespace RideShare_Connect.Controllers
{
    public class AdminAccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<Admin> _passwordHasher;
        private readonly ILogger<AdminAccountController> _logger;

        public AdminAccountController(ApplicationDbContext context, IPasswordHasher<Admin> passwordHasher, ILogger<AdminAccountController> logger)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        public IActionResult AdminLogin()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminLogin(AdminLoginDto model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var admin = await _context.Admins.FirstOrDefaultAsync(a => a.Username == model.Username && a.Status == "Active");
            if (admin == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(model);
            }

            var result = _passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, model.Password);
            if (result == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                new Claim(ClaimTypes.Name, admin.Username),
                new Claim(ClaimTypes.Role, admin.Role ?? "Admin")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(60)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), authProperties);

            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Admin", "AdminDashboard");
        }
    }
}
