using Microsoft.AspNetCore.Mvc;
using RideShare_Connect.DTOs;
using RideShare_Connect.Models.UserManagement;
using RideShareConnect.Data;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace RideShare_Connect.Controllers
{
    public class UserAccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly ILogger<UserAccountController> _logger;

        public UserAccountController(ApplicationDbContext context, IPasswordHasher<User> passwordHasher, ILogger<UserAccountController> logger)
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
        public async Task<IActionResult> Register(UserRegisterDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "User with this email already exists.");
                return View(model);
            }

            var newUser = new User
            {
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                UserType = model.UserType ?? "Rider",
                AccountStatus = "Active",
                CreatedAt = DateTime.UtcNow,
                SecretKey = model.SecretKey
            };

            newUser.PasswordHash = _passwordHasher.HashPassword(newUser, model.Password);

            newUser.UserProfile = new UserProfile
            {
                FullName = model.FullName,
                Bio = "Default bio message for new users.",
                ProfilePicture = "https://avatar.iran.liara.run/public/boy?username=Ash",
                EmergencyContact = "Nan",
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "User registered successfully! You can now log in.";
            return RedirectToAction("Login");
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginDto model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var normalizedEmail = model.Email?.Trim().ToLower();
            var user = await _context.Users
                                     .Include(u => u.UserProfile)
                                     .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(model);
            }

            var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.UserType ?? "Rider"),
                new Claim(ClaimTypes.Name, user.UserProfile?.FullName ?? user.Email)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe
            };

            if (model.RememberMe)
            {
                authProperties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7);
            }

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            _logger.LogInformation("User {Email} logged in successfully.", model.Email);

            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("User", "UserDashboard");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult GoogleLogin()
        {
            var redirectUrl = Url.Action("GoogleResponse");
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleResponse()
        {
            var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
            if (!result.Succeeded)
            {
                return RedirectToAction("Login");
            }

            var email = result.Principal.FindFirstValue(ClaimTypes.Email);
            var name = result.Principal.FindFirstValue(ClaimTypes.Name);
            
            if (string.IsNullOrEmpty(email))
            {
                 return RedirectToAction("Login");
            }

            var user = await _context.Users.Include(u => u.UserProfile).FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                // Create new user
                user = new User
                {
                    Email = email,
                    UserType = "Rider",
                    AccountStatus = "Active",
                    CreatedAt = DateTime.UtcNow,
                    PhoneNumber = "9000000000", // Placeholder valid phone number
                    SecretKey = Guid.NewGuid().ToString().Substring(0, 8) // Generate a random secret key
                };
                
                // Set a dummy password hash as it's required, but user logs in via Google
                user.PasswordHash = _passwordHasher.HashPassword(user, Guid.NewGuid().ToString());

                 user.UserProfile = new UserProfile
                {
                    FullName = name ?? "Google User",
                    Bio = "Google Account",
                    ProfilePicture = "https://avatar.iran.liara.run/public/boy?username=Ash",
                    EmergencyContact = "Nan",
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Ensure existing users have required fields (SecretKey, PhoneNumber)
                bool changed = false;
                if (string.IsNullOrEmpty(user.SecretKey))
                {
                    user.SecretKey = Guid.NewGuid().ToString().Substring(0, 8);
                    changed = true;
                }
                // Check if phone number is valid (10 digits, starts with 6-9)
                if (string.IsNullOrEmpty(user.PhoneNumber) || !System.Text.RegularExpressions.Regex.IsMatch(user.PhoneNumber, @"^[6-9]\d{9}$"))
                {
                    user.PhoneNumber = "9000000000"; // Placeholder
                    changed = true;
                }

                if (changed)
                {
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                }
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.UserType ?? "Rider"),
                 new Claim(ClaimTypes.Name, user.UserProfile?.FullName ?? user.Email)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("User", "UserDashboard");
        }

         [HttpGet]
        [AllowAnonymous]
        public IActionResult FacebookLogin()
        {
            var redirectUrl = Url.Action("FacebookResponse");
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, FacebookDefaults.AuthenticationScheme);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> FacebookResponse()
        {
            var result = await HttpContext.AuthenticateAsync(FacebookDefaults.AuthenticationScheme);
            if (!result.Succeeded)
            {
                return RedirectToAction("Login");
            }
             var email = result.Principal.FindFirstValue(ClaimTypes.Email);
             // Similar logic to GoogleResponse for user creation if needed
             // For now just sign in
            var claimsIdentity = new ClaimsIdentity(result.Principal.Claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("User", "UserDashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("User logged out.");
            return RedirectToAction("Login", "UserAccount");
        }
        
        // Forgot Password and other methods can be implemented similarly or kept as stubs for now
         public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
            {
                ModelState.AddModelError("Email", "User not found.");
                return View(dto);
            }

            return RedirectToAction("ResetPassword", new { email = dto.Email });
        }

        [HttpGet]
        public IActionResult ResetPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("ForgotPassword");
            }
            return View(new UserResetPasswordDto { Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(UserResetPasswordDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
            {
                ModelState.AddModelError("Email", "User not found.");
                return View(dto);
            }

            if (user.SecretKey != dto.SecretKey)
            {
                ModelState.AddModelError("SecretKey", "Invalid Secret Key.");
                return View(dto);
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.NewPassword);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Password reset successfully! You can now log in.";
            return RedirectToAction("Login");
        }
    }
}
