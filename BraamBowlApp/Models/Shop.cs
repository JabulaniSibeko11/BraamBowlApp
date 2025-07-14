using System.ComponentModel.DataAnnotations;

namespace BraamBowlApp.Models
{
    public class Shop
    {
        [Key]
        public int Shop_ID { get; set; }

        [Required(ErrorMessage = "Shop name is required")]
        [Display(Name = "Shop Name")]
        public string Shop_Name { get; set; }

        [Required(ErrorMessage = "Shop address is required")]
        [Display(Name = "Address")]
        public string Address { get; set; }

        [Phone]
        [Display(Name = "Contact Number")]
        public string Contact_Number { get; set; }

        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

       
        public virtual ICollection<Order> Orders { get; set; }
        public virtual ICollection<MenuItem> MenuItems { get; set; }
    }
}
