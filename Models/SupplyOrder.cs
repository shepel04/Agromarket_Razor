namespace Agromarket.Models;

public class SupplyOrder
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; }

    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string Status { get; set; } // Наприклад: "Очікується", "Доставлено", "Скасовано"

    public List<SupplyOrderItem> Items { get; set; }
}
