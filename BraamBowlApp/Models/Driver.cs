using System.ComponentModel.DataAnnotations;

namespace BraamBowlApp.Models
{
    public class Driver
    {
        [Key]
        public int Driver_ID { get; set; }

        [Required(ErrorMessage = "Driver name is required")]
        [Display(Name = "Full Name")]
        public string Full_Name { get; set; }

        [Required(ErrorMessage = "Contact number is required")]
        [Phone]
        [Display(Name = "Mobile Number")]
        public string Contact_Number { get; set; }

        [Display(Name = "Email Address")]
        [EmailAddress]
        public string Email { get; set; }

        [Display(Name = "License Number")]
        public string License_Number { get; set; }

        [Display(Name = "Vehicle Details")]
        public string Vehicle_Info { get; set; } 

        [Display(Name = "Status")]
        public string Status { get; set; } = "Available"; 

        // Navigation property
        public virtual ICollection<Delivery> Deliveries { get; set; }
    }
}
