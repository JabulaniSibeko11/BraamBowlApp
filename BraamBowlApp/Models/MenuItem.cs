using System.ComponentModel.DataAnnotations;

namespace BraamBowlApp.Models
{
    public class MenuItem
    {
        [Key]
        public int MenuItem_ID { get; set; }

        public int Order_ID { get; set; }
        public virtual Order Order { get; set; }

        [Required]
        public string Item_Name { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        public decimal Price { get; set; } 
    }
}
