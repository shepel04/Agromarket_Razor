namespace Agromarket.Models
{
    public class OutOfStockViewModel
    {
        public int ProductId { get; set; }
        public int Id { get; set; }
        public string ProductName { get; set; }
        public bool IsAvailableForPreorder { get; set; }
    }
}