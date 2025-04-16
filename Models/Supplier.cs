using System.Collections.Generic;

namespace Agromarket.Models
{
    public class Supplier
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ContactEmail { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }

        public string? TelegramBotToken { get; set; } 
        public string? TelegramChannelId { get; set; } 

        public List<SupplyOrder> SupplyOrders { get; set; }
        public List<SupplyProduct> SupplyProducts { get; set; }
    }
}