using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BraamBowlApp.Models
{
    public class Delivery
    {
        [Key]
        public int Delivery_ID { get; set; }

        [Required]
        public int Order_ID { get; set; }

        [ForeignKey("OrderId")]
        public virtual Order Order { get; set; }

        [Display(Name = "Driver Name")]
        public string Driver_Name { get; set; }

        public int? DriverId { get; set; } 
        public virtual Driver Driver { get; set; }

        [Display(Name = "Delivery Address")]
        public string Delivery_Address { get; set; }

        [Display(Name = "Dispatched Time")]
        [DataType(DataType.DateTime)]
        public DateTime? Dispatched_Time { get; set; }

        [Display(Name = "Delivered Time")]
        [DataType(DataType.DateTime)]
        public DateTime? Delivered_Time { get; set; }

        [Display(Name = "Delivery Status")]
        public string Status { get; set; } // e.g., "Pending", "In Transit", "Delivered", "Failed"

        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }
        
        [Display(Name = "One-Time Pin (OTP)")]
        [StringLength(6)]
        public string? OTP { get; set; }

        [Display(Name = "OTP Verified")]
        public bool? IsOTPVerified { get; set; } = false;
    }
}
