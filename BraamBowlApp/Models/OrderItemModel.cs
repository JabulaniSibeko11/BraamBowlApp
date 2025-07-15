using System.ComponentModel.DataAnnotations;

namespace BraamBowlApp.Models
{
    public class OrderItemModel
    {
        [Required]
        public int MenuItemId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        public string Name { get; set; } 
        public decimal Price { get; set; } 

        public string? Category { get; set; }

        public string? Description { get; set; }

        public string Tags { get; set; }
    }
}
