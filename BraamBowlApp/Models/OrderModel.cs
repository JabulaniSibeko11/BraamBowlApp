namespace BraamBowlApp.Models
{
    public class OrderModel
    {
        public List<OrderItemModel> Items { get; set; } = new List<OrderItemModel>();
    }
}
