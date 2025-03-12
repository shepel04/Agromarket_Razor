using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Agromarket.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Назва товару")]
        public string Name { get; set; }

        [Required]
        [Display(Name = "Опис товару")]
        public string Description { get; set; }

        [Required]
        [Display(Name = "Початок сезону")]
        public string SeasonStartMonth { get; set; }

        [Required]
        [Display(Name = "Кінець сезону")]
        public string SeasonEndMonth { get; set; }

        [Display(Name = "Фото товару")]
        public byte[]? ImageData { get; set; }
        
        [Display(Name = "Кількість на складі")]
        public int StockQuantity { get; set; }

        [Display(Name = "Ціна закупівлі")]
        public decimal? PurchasePrice { get; set; }

        [Display(Name = "Ціна продажу")]
        public decimal? SellingPrice { get; set; }
        
        [Display(Name = "Одиниця виміру")]
        public string Unit { get; set; } = "кг";
        
        [Required]
        [Display(Name = "Строк придатності (тижні)")]
        public int ShelfLifeWeeks { get; set; } = 4;

        [Display(Name = "Дата надходження")]
        public DateTime? ReceivedDate { get; set; }
        
        public int GetRemainingShelfLife()
        {
            if (!ReceivedDate.HasValue)
                return -1; 

            DateTime expiryDate = ReceivedDate.Value.AddDays(ShelfLifeWeeks * 7);
            int remainingDays = (expiryDate - DateTime.UtcNow).Days;
            int remainingWeeks = remainingDays / 7; 

            return remainingWeeks >= 0 ? remainingWeeks : 0; 
        }

        /// <summary>
        /// Перевіряє, чи товар є актуальним у поточний місяць
        /// </summary>
        public bool IsInSeason()
        {
            string[] months = { "Січень", "Лютий", "Березень", "Квітень", "Травень", "Червень", 
                "Липень", "Серпень", "Вересень", "Жовтень", "Листопад", "Грудень" };

            int startIdx = Array.IndexOf(months, SeasonStartMonth);
            int endIdx = Array.IndexOf(months, SeasonEndMonth);
            int currentIdx = DateTime.UtcNow.Month;

            if (startIdx == -1 || endIdx == -1 || currentIdx == -1)
                return false; 

            if (startIdx <= endIdx)
            {
                return currentIdx >= startIdx && currentIdx <= endIdx;
            }
            else
            {
                return currentIdx >= startIdx || currentIdx <= endIdx;
            }
        }
    }
}