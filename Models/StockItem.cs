namespace Agromarket.Models;


public class StockItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public string Unit{ get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public int ShelfLifeWeeks { get; set; }
}
