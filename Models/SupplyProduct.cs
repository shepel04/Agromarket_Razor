namespace Agromarket.Models;

public class SupplyProduct
{
    public int Id { get; set; }

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; }

    public decimal Price { get; set; }
    public bool InStock { get; set; } 
}
