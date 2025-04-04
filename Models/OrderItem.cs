using System.ComponentModel.DataAnnotations;
using Agromarket.Models;

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; }

    public int ProductId { get; set; }
    public string ProductName { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string Unit { get; set; }

    public bool IsPreorder { get; set; } = false;
    
    [DataType(DataType.DateTime)]
    [DisplayFormat(DataFormatString = "{0:u}", ApplyFormatInEditMode = true)]
    public DateTime? PreorderDate { get; set; }

}