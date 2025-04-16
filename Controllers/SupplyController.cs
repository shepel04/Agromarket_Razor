using Agromarket.Data;
using Agromarket.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Agromarket.Controllers;

[Authorize(Roles = "admin,warehousemanager")]
public class SupplyController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;

    public SupplyController(ApplicationDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> Index()
    {
        var suppliers = await _context.Suppliers
            .Include(s => s.SupplyProducts)
            .ThenInclude(sp => sp.Product)
            .ToListAsync();

        var allProducts = await _context.Products.ToListAsync();

        var supplyPrices = await _context.SupplyProducts
            .ToDictionaryAsync(p => p.ProductId, p => p.Price);

            var supplyHistory = await _context.SupplyOrders
            .Include(o => o.Supplier)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .ToListAsync();

        ViewBag.AllProducts = allProducts;
        ViewBag.SupplyPrices = supplyPrices;
        ViewBag.SupplyHistory = supplyHistory;
        
        Console.WriteLine("Знайдено замовлень: " + supplyHistory.Count);
        foreach (var order in supplyHistory)
        {
            Console.WriteLine($"→ {order.Supplier?.Name} - {order.OrderDate:yyyy-MM-dd}, товарів: {order.Items?.Count}");
        }


        return View(suppliers);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateSupplier(string name, string contactEmail, string phone, string address, string? telegramBotToken, string? telegramChannelId)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest();

        var supplier = new Supplier
        {
            Name = name,
            ContactEmail = contactEmail,
            Phone = phone,
            Address = address,
            TelegramBotToken = telegramBotToken,
            TelegramChannelId = telegramChannelId
        };

        _context.Suppliers.Add(supplier);
        _context.SaveChanges();

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditProducts(int id, Dictionary<int, SupplyProductUpdateItem> items)
    {
        var supplier = _context.Suppliers
            .Include(s => s.SupplyProducts)
            .FirstOrDefault(s => s.Id == id);

        if (supplier == null || items == null)
            return NotFound();

        supplier.SupplyProducts.Clear();

        foreach (var item in items.Values)
        {
            if (item.IsSelected && item.Price > 0)
            {
                supplier.SupplyProducts.Add(new SupplyProduct
                {
                    SupplierId = id,
                    ProductId = item.ProductId,
                    Price = item.Price
                });
            }
        }

        _context.SaveChanges();
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var supplier = _context.Suppliers
            .Include(s => s.SupplyProducts)
            .Include(s => s.SupplyOrders)
            .FirstOrDefault(s => s.Id == id);

        if (supplier == null)
            return NotFound();

        _context.SupplyProducts.RemoveRange(supplier.SupplyProducts);
        _context.SupplyOrders.RemoveRange(supplier.SupplyOrders);

        _context.Suppliers.Remove(supplier);
        _context.SaveChanges();

        return RedirectToAction("Index");
    }

    [HttpGet]
    public IActionResult CreateRequest(int supplierId)
    {
        var supplier = _context.Suppliers.FirstOrDefault(s => s.Id == supplierId);
        if (supplier == null) return NotFound();

        var supplyProducts = _context.SupplyProducts
            .Include(sp => sp.Product) 
            .Where(sp => sp.SupplierId == supplierId)
            .ToList();

        ViewBag.SelectedSupplier = supplier;
        ViewBag.SupplierProducts = supplyProducts;

        return View(new SupplyOrder());
    }



    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRequest(int supplierId, List<SupplyOrderItem> items)
    {
        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId);
        if (supplier == null || items == null)
            return BadRequest("Постачальник або список товарів відсутній");

        // Фільтруємо лише ті товари, де кількість > 0
        var filteredItems = items.Where(i => i.Quantity > 0).ToList();
        if (!filteredItems.Any())
        {
            TempData["TelegramWarning"] = "⚠️ Ви не вибрали жодного товару для замовлення. Вкажіть кількість хоча б одного товару.";
            return RedirectToAction("CreateRequest", new { supplierId });
        }

        var deliveryDays = _context.SupplyOrders
            .Where(s => s.SupplierId == supplierId && s.Status == "Доставлено" && s.ExpectedDeliveryDate != null)
            .Select(s => (s.ExpectedDeliveryDate.Value - s.OrderDate).TotalDays)
            .ToList();

        var avgDays = deliveryDays.Any() ? (int)deliveryDays.Average() : 5;
        var now = DateTime.UtcNow;

        var order = new SupplyOrder
        {
            SupplierId = supplierId,
            OrderDate = now,
            ExpectedDeliveryDate = now.AddDays(avgDays),
            Status = "Очікується",
            Items = filteredItems
        };

        _context.SupplyOrders.Add(order);
        await _context.SaveChangesAsync();

        if (string.IsNullOrWhiteSpace(supplier.TelegramBotToken) || string.IsNullOrWhiteSpace(supplier.TelegramChannelId))
        {
            TempData["TelegramWarning"] = "⚠️ Неможливо надіслати повідомлення у Telegram, бо у постачальника не вказано Token або ID каналу.";
        }
        else
        {
            await SendTelegramNotification(supplier, order);
        }

        return RedirectToAction("Index");
    }

    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSupplier(int id, string name, string contactEmail, string phone, string address, string? telegramBotToken, string? telegramChannelId)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier == null) return NotFound();

        supplier.Name = name;
        supplier.ContactEmail = contactEmail;
        supplier.Phone = phone;
        supplier.Address = address;
        supplier.TelegramBotToken = telegramBotToken;
        supplier.TelegramChannelId = telegramChannelId;

        await _context.SaveChangesAsync();

        return RedirectToAction("Index");
    }

    private async Task SendTelegramNotification(Supplier supplier, SupplyOrder order)
    {
        var token = supplier.TelegramBotToken;
        var chatId = supplier.TelegramChannelId;

        var sb = new StringBuilder();
        sb.AppendLine("📦 *НОВЕ ЗАМОВЛЕННЯ НА ПОСТАВКУ*");
        sb.AppendLine();
        sb.AppendLine($"🔹 Постачальник: *{supplier.Name}*");
        sb.AppendLine($"📅 Дата замовлення: {order.OrderDate:dd.MM.yyyy}");
        sb.AppendLine($"📆 Очікується до: ~{order.ExpectedDeliveryDate:dd.MM.yyyy}");
        sb.AppendLine();
        sb.AppendLine("🧾 Товари:");

        foreach (var item in order.Items.Where(i => i.Quantity > 0))
        {
            var product = await _context.Products.FindAsync(item.ProductId);
            if (product != null)
            {
                sb.AppendLine($"• {product.Name} — {item.Quantity} од.");
            }
        }

        sb.AppendLine();
        sb.AppendLine("📞Контактний номер: 0671266240");
        sb.AppendLine();
        sb.AppendLine("🛠 Коментар: Автоматичне замовлення");

        var client = _httpClientFactory.CreateClient();
        var url = $"https://api.telegram.org/bot{token}/sendMessage";

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("chat_id", chatId),
            new KeyValuePair<string, string>("text", sb.ToString()),
            new KeyValuePair<string, string>("parse_mode", "Markdown")
        });

        await client.PostAsync(url, content);
    }


    public class SupplyProductUpdateModel
    {
        public bool IsSelected { get; set; }
        public decimal Price { get; set; }
    }
    
    public class SupplyProductInput
    {
        public int ProductId { get; set; }
        public decimal Price { get; set; }
        public bool IsSelected { get; set; }
    }
    
    public class SupplyProductUpdateItem
    {
        public int ProductId { get; set; }
        public decimal Price { get; set; }
        public bool IsSelected { get; set; }
    }
}
