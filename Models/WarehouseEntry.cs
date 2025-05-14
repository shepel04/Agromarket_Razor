
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Agromarket.Models;

    namespace Agromarket.Models
    {
        public class WarehouseEntry
        {
            public int Id { get; set; }

            public int ProductId { get; set; }
            public Product Product { get; set; }

            public int Quantity { get; set; }

            public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;

            public int ShelfLifeWeeks { get; set; }

            public decimal PurchasePrice { get; set; }
            public decimal? SellingPrice { get; set; }

            public bool IsAvailableForPreorder { get; set; } = false;

            public DateTime? ExpirationDate { get; set; }
            
            public DateTime? ExpectedRestockDate { get; set; }
            
            public bool HasDiscount { get; set; } = false;

            [Range(0, 100)]
            public decimal? DiscountPercent { get; set; }

            public DateTime? DiscountStartDate { get; set; }
            public DateTime? DiscountEndDate { get; set; }

        public decimal? DiscountedPrice
        {
            get
            {
                if (HasDiscount && DiscountPercent.HasValue && SellingPrice.HasValue)
                {
                    return Math.Round(SellingPrice.Value * (1 - DiscountPercent.Value / 100), 2);
                }
                return null;
            }
        }

    }
}