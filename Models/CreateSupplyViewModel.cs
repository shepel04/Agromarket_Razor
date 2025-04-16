using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Agromarket.Models;

namespace Agromarket.Models
{
    public class CreateSupplyViewModel
    {
        [Required]
        public int SupplierId { get; set; }

        public List<Supplier> Suppliers { get; set; }
        public List<ProductDto> Products { get; set; }

        public List<SupplyOrderItemDto> Items { get; set; } = new();
    }

    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class SupplyOrderItemDto
    {
        public int ProductId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Кількість має бути більше нуля")]
        public int Quantity { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Ціна має бути позитивною")]
        public decimal PurchasePrice { get; set; }
    }
}