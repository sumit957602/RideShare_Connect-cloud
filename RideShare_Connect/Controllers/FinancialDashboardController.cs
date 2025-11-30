using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideShareConnect.Data;
using RideShare_Connect.ViewModels;
using RideShare_Connect.Models.PaymentManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace RideShare_Connect.Controllers
{
    [Authorize]
    public class FinancialDashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;
        public FinancialDashboardController(ApplicationDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public async Task<IActionResult> Finance()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return RedirectToAction("Login", "UserAccount");
            }
            var userId = int.Parse(userIdClaim);

            var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            var balance = wallet?.Balance ?? 0;

            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var paymentQuery = _db.Payments
                .Include(p => p.PaymentMethod)
                .Where(p => p.UserId == userId);

            var totalSpent = await paymentQuery
                .Where(p => p.Status == "Completed" && p.PaymentDate >= startOfMonth)
                .SumAsync(p => p.Amount);

            var walletTransactions = new List<WalletTransaction>();
            if (wallet != null)
            {
                walletTransactions = await _db.WalletTransactions
                    .Where(t => t.WalletId == wallet.Id)
                    .OrderByDescending(t => t.TxnDate)
                    .ToListAsync();
            }

            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

            var model = new FinanceViewModel
            {
                WalletBalance = balance,
                TotalSpentThisMonth = totalSpent,
                TotalSavedOnRides = 0,
                PaymentMethods = await _db.PaymentMethods.Where(pm => pm.UserId == userId).ToListAsync(),
                TransactionHistory = walletTransactions,
                RecentTransactions = walletTransactions.Take(3).ToList(),
                Profile = profile
            };

            ViewBag.RazorpayKey = _config["PaymentGateway:ApiKey"];

            return View(model);
        }

        [HttpGet]
        public IActionResult AddPaymentMethod()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPaymentMethod(PaymentMethod method)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return RedirectToAction("Login", "UserAccount");
            }
            var userId = int.Parse(userIdClaim);

            ModelState.Remove("UserId");
            ModelState.Remove("User");
            if (!ModelState.IsValid)
            {
                return View(method);
            }

            method.UserId = userId;
            method.CardType ??= "Credit Card";

            method.CardTokenNo = Guid.NewGuid().ToString();

            if (!string.IsNullOrEmpty(method.CardNumber))
            {
                var digits = method.CardNumber.Replace(" ", "");
                if (digits.Length >= 4)
                {
                    method.CardLast4Digit = digits[^4..];
                }
            }

            method.CardBrand = "VISA";

            _db.PaymentMethods.Add(method);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Finance));
        }

        [HttpGet]
        public async Task<IActionResult> CardDetails(int id)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return RedirectToAction("Login", "UserAccount");
            }
            var userId = int.Parse(userIdClaim);

            var method = await _db.PaymentMethods
                .FirstOrDefaultAsync(pm => pm.Id == id && pm.UserId == userId);

            if (method == null)
            {
                return NotFound();
            }

            return View(method);
        }

        [HttpGet]
        public IActionResult TopUpWallet()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> EditPaymentMethod(int id)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return RedirectToAction("Login", "UserAccount");
            }
            var userId = int.Parse(userIdClaim);

            var method = await _db.PaymentMethods
                .FirstOrDefaultAsync(pm => pm.Id == id && pm.UserId == userId);

            if (method == null)
            {
                return NotFound();
            }

            return View(method);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPaymentMethod(int id, PaymentMethod method)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return RedirectToAction("Login", "UserAccount");
            }
            var userId = int.Parse(userIdClaim);

            if (id != method.Id)
            {
                return NotFound();
            }

            ModelState.Remove("UserId");
            ModelState.Remove("User");

            if (ModelState.IsValid)
            {
                try
                {
                    var existingMethod = await _db.PaymentMethods
                        .FirstOrDefaultAsync(pm => pm.Id == id && pm.UserId == userId);

                    if (existingMethod == null)
                    {
                        return NotFound();
                    }

                    existingMethod.CardType = method.CardType;
                    existingMethod.CardholderName = method.CardholderName;
                    existingMethod.CardNumber = method.CardNumber;
                    existingMethod.ExpiryDate = method.ExpiryDate;
                    existingMethod.CVV = method.CVV;

                    if (!string.IsNullOrEmpty(method.CardNumber))
                    {
                        var digits = method.CardNumber.Replace(" ", "");
                        if (digits.Length >= 4)
                        {
                            existingMethod.CardLast4Digit = digits[^4..];
                        }
                    }

                    _db.Update(existingMethod);
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PaymentMethodExists(method.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Finance));
            }
            return View(method);
        }

        [HttpPost, ActionName("DeletePaymentMethod")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePaymentMethodConfirmed(int id)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return RedirectToAction("Login", "UserAccount");
            }
            var userId = int.Parse(userIdClaim);

            var method = await _db.PaymentMethods
                .FirstOrDefaultAsync(pm => pm.Id == id && pm.UserId == userId);

            if (method != null)
            {
                _db.PaymentMethods.Remove(method);
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Finance));
        }

        private bool PaymentMethodExists(int id)
        {
            return _db.PaymentMethods.Any(e => e.Id == id);
        }

        [HttpGet]
        public IActionResult PaymentHistory()
        {
            return View();
        }
    }
}
