namespace BraamBowlApp.Models
{
    public class CartItem
    {
        public int MenuItemId { get; set; }
        public string Item_Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; } 

    }
}
