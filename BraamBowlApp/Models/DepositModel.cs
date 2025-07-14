using System.ComponentModel.DataAnnotations;

namespace BraamBowlApp.Models
{
    public class DepositModel
    {
        [Required(ErrorMessage = "Please enter a deposit amount.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Deposit amount must be positive.")]
        public decimal DepositAmount { get; set; }

        [Required(ErrorMessage = "Please select a payment method.")]
        public string PaymentMethod { get; set; }

        public List<string> PaymentMethods => new List<string> { "Bank Transfer", "Payroll Deduction", "Credit Card" };
    }
}
