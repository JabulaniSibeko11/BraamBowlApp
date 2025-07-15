using System.ComponentModel.DataAnnotations;

namespace BraamBowlApp.Models
{
    public class OrderItem
    {
        [Key]
        public int Order_Item_ID { get; set; }
        public int Order_Id { get; set; }
        public int Menu_Item_Id { get; set; }
        public int Menu_Item_Name { get; set; }
        public int Quantity { get; set; }
        public decimal Unit_Price_At_Time_Of_Order { get; set; }
        public decimal TotalPrice => Quantity * Unit_Price_At_Time_Of_Order;
    }
}
