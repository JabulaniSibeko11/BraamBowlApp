namespace BraamBowlApp.Models
{
    public class CheckoutViewModel
    {
        public int ShopId { get; set; }
        public decimal EmployeeBalance { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal RemainingBalance { get; set; }
        public List<OrderItemModel> Items { get; set; }
        public List<CartItem> CartItems { get; set; } = new List<CartItem>();
        public string EmployeeName { get; set; }

        public string Shop_Name { get; set; }

    }
}
