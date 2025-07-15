using System.ComponentModel.DataAnnotations;

namespace BraamBowlApp.Models
{
    public class Payment
    {
        [Key]
        public int Payment_ID { get; set; }

        public int? Order_ID { get; set; }
        public virtual Order Order { get; set; }

        [Required]
        public string Payment_Method { get; set; } 

        public DateTime Payment_Date { get; set; } = DateTime.Now;

        public bool IsSuccessful { get; set; }

        public int Employee_ID { get; set; }
        public virtual User User { get; set; }

        [Required]
        public decimal DepositAmount { get; set; }

        public decimal CompanyCredit { get; set; }
    }
}
