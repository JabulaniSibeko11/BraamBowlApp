using System.ComponentModel.DataAnnotations;

namespace BraamBowlApp.Models
{
    public class DashboardViewModel
    {
        public int EmployeeId { get; set; }
        public decimal? Balance { get; set; }
        public decimal? LastDepositAmount { get; set; }
        public DateTime? LastDepositDate { get; set; }
        public decimal TotalDeposited { get; set; }
        public decimal CompanyMatch { get; set; }
        public decimal TotalSpent { get; set; }
        public int OrdersPlaced { get; set; }
        public decimal MonthlyDeposits { get; set; }
        public decimal MonthlyCompanyMatch { get; set; }
        public decimal AverageOrderAmount { get; set; }
        public decimal DepositAmount { get; set; }
        public DateTime DepositDate { get; set; } = DateTime.Today;
        public bool ShowWelcomeModal { get; set; } = true;
        public List<Restuarants> Restuarant { get; set; } = new List<Restuarants>();
        public List<OrderView> OrderHistory { get; set; } = new List<OrderView>();
        public List<CurrentOrder> CurrentOrders { get; set; } = new List<CurrentOrder>();
        public List<Deposit> depositModels { get; set; } = new List<Deposit>();

        public string PaymentMethod { get; set; }

        public List<string> PaymentMethods => new List<string> { "Bank Transfer", "Payroll Deduction", "Credit Card" };
        public string PaymentToken { get; set; }
        public string OrderId { get; set; }

    }


    public class Restuarants
    {
        public int ShopId { get; set; }
        public string ShopName { get; set; }
        public string Type { get; set; } // Not in DB; we'll map based on data or add to Shops table
        public List<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
        public string OrderId { get; set; }
    }

    public class MenuItemView
    {
        public int MenuItemId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
        public string OrderId { get; set; }
    }

    public class OrderView
    {
        public DateTime OrderDate { get; set; }
        public string ItemName { get; set; }
        public string ShopName { get; set; }
        public decimal Amount { get; set; }
        public string OrderId { get; set; }
    }

    public class CurrentOrder
    {
        public string OrderId { get; set; }
        public string ItemName { get; set; }
        public string ShopName { get; set; }
        public string Status { get; set; }
        public int EstimatedDeliveryMinutes { get; set; }
    }
    public class Deposit
    {
        [Required(ErrorMessage = "Please enter a deposit amount.")]
        [Range(250, double.MaxValue, ErrorMessage = "Minimum deposit is R250")]
        public decimal DepositAmount { get; set; }

        [Required(ErrorMessage = "Please select a payment method.")]
        public string PaymentMethod { get; set; }

        public List<string> PaymentMethods => new List<string> { "Bank Transfer", "Payroll Deduction", "Credit Card" };

        public string PaymentToken { get; set; }

    }
}
