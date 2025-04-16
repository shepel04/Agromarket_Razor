namespace Agromarket.Models;

public class SupplyOrderItem
{
    public int Id { get; set; }

    public int SupplyOrderId { get; set; }
    public SupplyOrder SupplyOrder { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; }

    public int Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
}
