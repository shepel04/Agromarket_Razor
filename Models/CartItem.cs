namespace Agromarket.Models
{
    public class CartItem
    {
        public int EntryId { get; set; } 
        public int ProductId { get; set; }

        public string Name { get; set; }

        public decimal Price { get; set; }

        public int Quantity { get; set; }

        public string Unit { get; set; }

        public string? ImageBase64 { get; set; }
        
        public int MaxQuantity { get; set; }

    }
}