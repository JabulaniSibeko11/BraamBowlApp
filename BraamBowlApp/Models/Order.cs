using System.ComponentModel.DataAnnotations;

namespace BraamBowlApp.Models
{
    public class Order
    {
        [Key]
        public int Order_ID { get; set; }

        public int Employee_ID { get; set; }
        public virtual User Employee { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        [Display(Name = "Order Date & Time")]
        public DateTime Order_Date { get; set; } = DateTime.Now;

        [Required]
        [Display(Name = "Order Status")]
        public string Status { get; set; }
        public virtual ICollection<MenuItem> OrderedItems { get; set; }

        public int Shop_ID { get; set; }
        public string? Shop_Name { get; set; }

        public string? Item_Name { get; set; }

        public virtual Shop Shop { get; set; }

        public virtual Delivery Delivery { get; set; }

        public decimal Amount { get; set; }

        public DateTime Payment_Date { get; set; }

    }
}
