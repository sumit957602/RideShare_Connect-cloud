using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RideShare_Connect.Models.UserManagement;

namespace RideShare_Connect.Models.PaymentManagement
{
    public class PaymentMethod
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string CardType { get; set; }  // e.g., "Credit Card", "Debit Card", "UPI", "NetBanking"

        [Required]
        [StringLength(100)]
        public string? CardholderName { get; set; }

        [Required(ErrorMessage = "Card Number is required")]
        [StringLength(16, MinimumLength = 16, ErrorMessage = "Card Number must be 16 digits")]
        [RegularExpression(@"^[0-9]{16}$", ErrorMessage = "Invalid Card Number")]
        public string? CardNumber { get; set; }

        [Required(ErrorMessage = "Expiry Date is required")]
        [StringLength(7)]
        [Column(TypeName = "nvarchar(7)")]
        [RegularExpression(@"^(0[1-9]|1[0-2])\/?([0-9]{4}|[0-9]{2})$", ErrorMessage = "Invalid Expiry Date (MM/YYYY)")]
        public string? ExpiryDate { get; set; }

        [Required(ErrorMessage = "CVV is required")]
        [StringLength(4, MinimumLength = 3, ErrorMessage = "CVV must be 3 or 4 digits")]
        [RegularExpression(@"^[0-9]{3,4}$", ErrorMessage = "Invalid CVV")]
        public string? CVV { get; set; }

        [StringLength(100)]
        public string? CardTokenNo { get; set; }

        [StringLength(4)]
        public string? CardLast4Digit { get; set; }

        [StringLength(20)]
        public string? CardBrand { get; set; }

        // Navigation Property
        public virtual User User { get; set; }
    }
}
