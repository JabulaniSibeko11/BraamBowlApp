namespace BraamBowlApp.Models
{
    public class ReceiptViewModel
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public string ShopName { get; set; }
        public string EmployeeName { get; set; }
        public string EmployeeSurname { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime EstimatedDelivery { get; set; }
        public int ShopId { get; set; }
        public List<OrderItemModel> Items { get; set; }

        public string DeliveryAddress { get; set; }

        public string DriverName { get; set; }

        public string OTP { get; set; }

    }
}
