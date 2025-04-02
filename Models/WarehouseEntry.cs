
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

    }
}