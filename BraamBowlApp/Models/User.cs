using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace BraamBowlApp.Models
{
    public class User :IdentityUser
    {
        
        public int Employee_ID { get; set; }

        [Required(ErrorMessage = "Surname is required")]
        [Display(Name = "Surname")]
        public string? Surname { get; set; }
        [Required(ErrorMessage = "first Name is required")]
        [Display(Name = "FullName")]
        public string? First_Name { get; set; }
   
        public string? Street_address { get; set; }
        public string? City { get; set; }
        public string? Region { get; set; }
        public string? Postal_Code { get; set; }

        public string? Country { get; set; }
        [Required]
        public string? Mobile_Number { get; set; }

        [Required(ErrorMessage = "Delivery address is required")]
        [Display(Name = "Delivery Address")]
        public string Delivery_Address { get; set; }
        public decimal Balance { get; set; }
        public DateTime? Last_Deposit_Month { get; set; }

    public decimal Monthly_Deposit_Total { get; set; }
}
}
