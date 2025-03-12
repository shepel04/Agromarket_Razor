using System;
using System.ComponentModel.DataAnnotations;

namespace Agromarket.Models
{
    public class StockTransaction
    {
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        public Product Product { get; set; }

        [Required]
        [Display(Name = "Тип операції")]
        public string TransactionType { get; set; } 

        [Required]
        [Display(Name = "Кількість")]
        public int Quantity { get; set; }

        [Required]
        [Display(Name = "Дата операції")]
        public DateTime TransactionDate { get; set; }

        [Display(Name = "Ціна закупівлі (при надходженні)")]
        public decimal? PurchasePrice { get; set; }

        [Display(Name = "Ціна продажу")]
        public decimal? SellingPrice { get; set; }
    }
}
