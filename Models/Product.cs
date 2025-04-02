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
        [Display(Name = "Категорія")]
        public string Category { get; set; }

        [Required]
        [Display(Name = "Початок сезону")]
        public string SeasonStartMonth { get; set; }

        [Required]
        [Display(Name = "Кінець сезону")]
        public string SeasonEndMonth { get; set; }

        [Display(Name = "Фото товару")]
        public byte[]? ImageData { get; set; }

        [Display(Name = "Одиниця виміру")]
        public string Unit { get; set; } = "кг";

        /// <summary>
        /// Перевіряє, чи товар є актуальним у поточний місяць
        /// </summary>
        public bool IsInSeason()
        {
            string[] months = {
                "Січень", "Лютий", "Березень", "Квітень", "Травень", "Червень",
                "Липень", "Серпень", "Вересень", "Жовтень", "Листопад", "Грудень"
            };

            int startIdx = Array.IndexOf(months, SeasonStartMonth);
            int endIdx = Array.IndexOf(months, SeasonEndMonth);
            int currentIdx = DateTime.UtcNow.Month - 1;

            if (startIdx == -1 || endIdx == -1 || currentIdx == -1)
                return false;

            if (startIdx <= endIdx)
                return currentIdx >= startIdx && currentIdx <= endIdx;
            else
                return currentIdx >= startIdx || currentIdx <= endIdx;
        }
    }
}