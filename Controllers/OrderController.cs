using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Agromarket.Data;
using Agromarket.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Agromarket.Controllers
{
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public OrderController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var cart = GetCart();
<<<<<<< Updated upstream
            if (!cart.Any())
=======
            if (!cart.Any()) return RedirectToAction("Index", "Cart");

            var user = await _userManager.GetUserAsync(User);
            var order = new Order { CustomerEmail = user?.Email ?? "" };

            ViewBag.CartItems = cart;
            ViewBag.TotalAmount = cart.Sum(i => i.Price * i.Quantity);
            return View(order);
        }
        
        [HttpPost]
public async Task<IActionResult> Checkout(Order order)
{
    var cart = GetCart();
    if (!cart.Any())
        return RedirectToAction("Index", "Cart");

    if (!ModelState.IsValid)
    {
        ViewBag.CartItems = cart;
        ViewBag.TotalAmount = cart.Sum(i => i.Price * i.Quantity);
        return View(order);
    }

    var productIds = cart.Select(c => c.ProductId).ToList();

    using var transaction = await _context.Database.BeginTransactionAsync();

    var warehouseEntries = await _context.WarehouseEntries
        .Include(e => e.Product)
        .Where(e => productIds.Contains(e.ProductId))
        .ToListAsync();

    List<string> insufficient = new();

    var availableStock = warehouseEntries
        .GroupBy(e => e.ProductId)
        .ToDictionary(g => g.Key, g => g.Sum(e => e.Quantity));

    foreach (var item in cart.Where(c => !c.IsPreorder))
    {
        availableStock.TryGetValue(item.ProductId, out int stockQuantity);
        if (stockQuantity < item.Quantity)
        {
            insufficient.Add($"{item.Name} (доступно: {stockQuantity}, потрібно: {item.Quantity})");
        }
    }

    if (insufficient.Any())
    {
        var firstProblemItem = cart.FirstOrDefault(c => insufficient.Any(ins => ins.Contains(c.Name)));
        if (firstProblemItem != null)
        {
            var entries = await _context.WarehouseEntries
                .Where(e => e.ProductId == firstProblemItem.ProductId)
                .ToListAsync();

            bool isAvailableForPreorder = entries.Any(e => e.IsAvailableForPreorder);

            var viewModel = new OutOfStockViewModel
>>>>>>> Stashed changes
            {
                return RedirectToAction("Index", "Cart");
            }

            // Отримуємо email авторизованого користувача
            var user = await _userManager.GetUserAsync(User);
            var userEmail = user?.Email ?? "";

            var order = new Order
            {
                CustomerEmail = userEmail // Передаємо email у форму
            };

            ViewBag.CartItems = cart;
            ViewBag.TotalAmount = cart.Sum(item => item.Price * item.Quantity);
            return View(order);
        }

<<<<<<< Updated upstream
=======
        await transaction.RollbackAsync();
        return RedirectToAction("Checkout");
    }

    var regularItems = cart.Where(c => !c.IsPreorder).ToList();
    var preorderItems = cart.Where(c => c.IsPreorder).ToList();

    var user = await _userManager.GetUserAsync(User);
    var isClient = await _userManager.IsInRoleAsync(user, "client");
    
    

    if (isClient && preorderItems.Any())
    {
        var expectedDeposit = preorderItems.Sum(p => p.Price * p.Quantity * 0.2m);
        var paid = HttpContext.Session.GetString("PreorderPaid");

        if (paid != "true")
        {
            HttpContext.Session.SetString("PreorderDepositAmount", expectedDeposit.ToString("0.00"));
            
            return RedirectToAction("Preorder", "Payment", new { amount = expectedDeposit });
        }
    }


    // --- Звичайне замовлення
    if (regularItems.Any())
    {
        var regularOrder = new Order
        {
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            CustomerPhone = order.CustomerPhone,
            DeliveryAddress = order.DeliveryAddress,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Виконується,
            TotalAmount = regularItems.Sum(c =>
                c.Quantity > 50 ? c.Price * c.Quantity * 0.95m : c.Price * c.Quantity),
            OrderItems = regularItems.Select(c => new OrderItem
            {
                ProductId = c.ProductId,
                ProductName = c.Name,
                Quantity = c.Quantity,
                Price = c.Quantity > 50 ? c.Price * 0.95m : c.Price,
                Unit = c.Unit,
                IsPreorder = false
            }).ToList()
        };


        foreach (var item in regularItems)
        {
            int remainingToDeduct = item.Quantity;

            var productEntries = warehouseEntries
                .Where(e => e.ProductId == item.ProductId && e.Quantity > 0)
                .OrderBy(e => e.ReceivedDate)
                .ToList();

            foreach (var entry in productEntries)
            {
                if (remainingToDeduct <= 0)
                    break;

                int deduction = Math.Min(entry.Quantity, remainingToDeduct);
                entry.Quantity -= deduction;
                remainingToDeduct -= deduction;
                _context.WarehouseEntries.Update(entry);
            }
        }

        _context.Orders.Add(regularOrder);
    }

    // --- Передзамовлення
    if (preorderItems.Any())
    {
        var preorderOrder = new Order
        {
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            CustomerPhone = order.CustomerPhone,
            DeliveryAddress = order.DeliveryAddress,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Виконується,
            TotalAmount = preorderItems.Sum(c => c.Price * c.Quantity),
            OrderItems = preorderItems.Select(c => new OrderItem
            {
                ProductId = c.ProductId,
                ProductName = c.Name,
                Quantity = c.Quantity,
                Price = c.Price,
                Unit = c.Unit,
                IsPreorder = true,
                PreorderDate = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(7), DateTimeKind.Utc)
            }).ToList()
        };

        _context.Orders.Add(preorderOrder);
    }

    await _context.SaveChangesAsync();
    await transaction.CommitAsync();

    ClearCart();
    HttpContext.Session.Remove("PreorderPaid");

    return RedirectToAction("OrderSuccess");
}




        private async Task NotifyLowStockAsync()
        {
            var lowStockEntries = await _context.WarehouseEntries
                .Include(e => e.Product)
                .Where(e => e.Quantity < 10)
                .ToListAsync();

            if (!lowStockEntries.Any()) return;

            var warehouseManagers = await _userManager.GetUsersInRoleAsync("warehousemanager");
            var admins = await _userManager.GetUsersInRoleAsync("admin");
            var recipients = warehouseManagers.Concat(admins).Distinct();

            foreach (var user in recipients)
            {
                foreach (var entry in lowStockEntries)
                {
                    bool alreadyNotified = _context.Notifications.Any(n =>
                            n.UserId == user.Id &&
                            n.Message.Contains($"\"{entry.Product.Name}\"") &&
                            n.CreatedAt > DateTime.UtcNow.AddHours(-6) 
                    );

                    if (!alreadyNotified)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            UserId = user.Id,
                            Message = $"Низький рівень запасів: товар \"{entry.Product.Name}\" (в партії #{entry.Id}) — лише {entry.Quantity} од.",
                            CreatedAt = DateTime.UtcNow,
                            IsRead = false,
                            RedirectUrl = $"/Warehouse/Details/{entry.Id}"
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
        }


>>>>>>> Stashed changes
        [HttpPost]
        public async Task<IActionResult> Checkout(Order order)
        {
            var cart = GetCart();
            if (!cart.Any())
            {
                return RedirectToAction("Index", "Cart");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.CartItems = cart;
                ViewBag.TotalAmount = cart.Sum(item => item.Price * item.Quantity);
                return View(order);
            }

            order.OrderItems = cart.Select(item => new OrderItem
            {
                ProductId = item.ProductId,
                ProductName = item.Name,
                Price = item.Price,
                Quantity = item.Quantity,
                Unit = item.Unit
            }).ToList();

            order.TotalAmount = cart.Sum(item => item.Price * item.Quantity);
    
            // Примусове перетворення дати у UTC
            order.OrderDate = DateTime.UtcNow;

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            ClearCart();
            return RedirectToAction("OrderSuccess");
        }


        public IActionResult OrderSuccess()
        {
            return View();
        }

<<<<<<< Updated upstream
=======
        [HttpGet]
        public async Task<IActionResult> OrderList(string statusFilter)
        {
            var orders = _context.Orders
                .Include(o => o.OrderItems)
                .AsQueryable();

            if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse(statusFilter, out OrderStatus status))
            {
                orders = orders.Where(o => o.Status == status);
            }

            ViewBag.StatusFilter = statusFilter;
            return View(await orders.ToListAsync());
        }
        
        [HttpGet]
        public async Task<IActionResult> FinalizePreorderAfterPayment()
        {
            var cartJson = HttpContext.Session.GetString("Cart");
            var preorderPaid = HttpContext.Session.GetString("PreorderPaid");

            if (string.IsNullOrEmpty(cartJson) || preorderPaid != "true")
                return RedirectToAction("Checkout");

            var cart = JsonConvert.DeserializeObject<List<CartItem>>(cartJson);
            var preorderItems = cart.Where(c => c.IsPreorder).ToList();
            if (!preorderItems.Any())
                return RedirectToAction("Checkout");

            var user = await _userManager.GetUserAsync(User);

            var preorderOrder = new Order
            {
                CustomerName = user?.UserName ?? "Клієнт",
                CustomerEmail = user?.Email ?? "—",
                CustomerPhone = "-", 
                DeliveryAddress = "—",
                OrderDate = DateTime.UtcNow,
                Status = OrderStatus.Виконується,
                TotalAmount = preorderItems.Sum(c => c.Price * c.Quantity),
                OrderItems = preorderItems.Select(c => new OrderItem
                {
                    ProductId = c.ProductId,
                    ProductName = c.Name,
                    Quantity = c.Quantity,
                    Price = c.Price,
                    Unit = c.Unit,
                    IsPreorder = true,
                    PreorderDate = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(7), DateTimeKind.Utc)
                }).ToList()
            };

            _context.Orders.Add(preorderOrder);
            await _context.SaveChangesAsync();

            ClearCart();
            HttpContext.Session.Remove("PreorderPaid");

            return RedirectToAction("OrderSuccess");
        }

    

        [HttpGet]
        public async Task<IActionResult> GetOrderDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            return PartialView("_OrderDetails", order);
        }

        public IActionResult OrderSuccess() => View();

>>>>>>> Stashed changes
        private List<CartItem> GetCart()
        {
            var cart = HttpContext.Session.GetString("Cart");
            return cart != null ? JsonConvert.DeserializeObject<List<CartItem>>(cart) : new List<CartItem>();
        }

        private void ClearCart()
        {
            HttpContext.Session.Remove("Cart");
        }
<<<<<<< Updated upstream
=======
        
        [HttpGet]
        public IActionResult EditOrder(int id)
        {
            var order = _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefault(o => o.Id == id);

            if (order == null)
                return NotFound();

            ViewBag.Entries = _context.WarehouseEntries
                .Include(e => e.Product)
                .Where(e => e.Quantity > 0)
                .ToList();

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditOrder(Order updatedOrder)
        {
            var existingOrder = _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefault(o => o.Id == updatedOrder.Id);

            if (existingOrder == null)
                return NotFound();

            // Оновлення полів замовлення
            existingOrder.CustomerName = updatedOrder.CustomerName;
            existingOrder.CustomerEmail = updatedOrder.CustomerEmail;
            existingOrder.CustomerPhone = updatedOrder.CustomerPhone;
            existingOrder.DeliveryAddress = updatedOrder.DeliveryAddress;
            existingOrder.OrderDate = updatedOrder.OrderDate;
            existingOrder.Status = updatedOrder.Status;

            // Оновлення товарів замовлення
            _context.OrderItems.RemoveRange(existingOrder.OrderItems);
            existingOrder.OrderItems = updatedOrder.OrderItems;

            _context.SaveChanges();
            return RedirectToAction("OrderList");
        }
        
        [HttpPost]
        public async Task<IActionResult> RepeatOrder(int orderId)
        {
            var originalOrder = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (originalOrder == null)
                return Json(new { success = false, message = "Замовлення не знайдено." });

            var newCart = new List<CartItem>();

            foreach (var item in originalOrder.OrderItems)
            {
                var entry = await _context.WarehouseEntries
                    .Where(e => e.ProductId == item.ProductId)
                    .OrderByDescending(e => e.Quantity > 0)
                    .ThenBy(e => e.ExpirationDate)
                    .FirstOrDefaultAsync();

                if (entry == null)
                {
                    return Json(new { success = false, message = $"Товар \"{item.ProductName}\" наразі недоступний." });
                }

                newCart.Add(new CartItem
                {
                    EntryId = entry.Id,
                    ProductId = item.ProductId,
                    Name = item.ProductName,
                    Quantity = item.Quantity,
                    Price = (entry.HasDiscount && entry.DiscountedPrice.HasValue)
                        ? entry.DiscountedPrice.Value
                        : entry.SellingPrice ?? 0,
                    Unit = item.Unit,
                    IsPreorder = entry.Quantity == 0 && entry.IsAvailableForPreorder,
                    MaxQuantity = entry.Quantity,
                    PreorderDate = DateTime.UtcNow.AddDays(7),
                    DepositAmount = entry.Quantity == 0
                        ? ((entry.SellingPrice ?? 0) * item.Quantity * 0.2m)
                        : 0
                });
            }

            HttpContext.Session.SetString("Cart", JsonConvert.SerializeObject(newCart));

            return Json(new { success = true });
        }

>>>>>>> Stashed changes
    }
}
