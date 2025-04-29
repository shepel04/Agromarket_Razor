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
        private readonly RoleManager<IdentityRole> _roleManager;

        public OrderController(ApplicationDbContext context, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var cart = GetCart();
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

            foreach (var item in cart.Where(c => !c.IsPreorder)) // Перевіряємо лише звичайні товари
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
                    {
                        ProductId = firstProblemItem.EntryId,
                        ProductName = firstProblemItem.Name,
                        IsAvailableForPreorder = isAvailableForPreorder
                    };

                    await transaction.RollbackAsync();
                    return View("OutOfStock", viewModel);
                }

                await transaction.RollbackAsync();
                return RedirectToAction("Checkout");
            }

            // ======= Нове: Розділяємо на звичайні замовлення і передзамовлення =======

            var regularItems = cart.Where(c => !c.IsPreorder).ToList();
            var preorderItems = cart.Where(c => c.IsPreorder).ToList();

            // --- 1. Створення основного замовлення (звичайне)
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
                    TotalAmount = regularItems.Sum(c => c.Price * c.Quantity),
                    OrderItems = regularItems.Select(c => new OrderItem
                    {
                        ProductId = c.ProductId,
                        ProductName = c.Name,
                        Quantity = c.Quantity,
                        Price = c.Price,
                        Unit = c.Unit,
                        IsPreorder = false
                    }).ToList()
                };

                // Списання залишків
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

            // --- 2. Створення передзамовлення
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
                        PreorderDate = c.PreorderDate ?? DateTime.UtcNow.AddDays(7) // якщо треба додати очікувану дату
                    }).ToList()
                };

                _context.Orders.Add(preorderOrder);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            ClearCart();

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


        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, OrderStatus newStatus)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound();

            if (order.Status != OrderStatus.Скасовано && newStatus == OrderStatus.Скасовано)
            {
                foreach (var item in order.OrderItems)
                {
                    var entry = await _context.WarehouseEntries.FirstOrDefaultAsync(e => e.ProductId == item.ProductId);
                    if (entry != null)
                    {
                        entry.Quantity += item.Quantity;
                        _context.WarehouseEntries.Update(entry);
                    }
                }
            }

            if (newStatus == OrderStatus.Виконано)
            {
                foreach (var item in order.OrderItems.Where(i => i.IsPreorder))
                {
                    var entry = await _context.WarehouseEntries.FirstOrDefaultAsync(e => e.ProductId == item.ProductId);

                    if (entry == null || entry.Quantity < item.Quantity)
                    {
                        TempData["StatusError"] = $"Недостатньо товару \"{item.ProductName}\" на складі для виконання передзамовлення.";
                        var orders = await _context.Orders.Include(o => o.OrderItems).ToListAsync();
                        return View("OrderList", orders);
                    }

                    entry.Quantity -= item.Quantity;
                    _context.WarehouseEntries.Update(entry);
                }
            }

            order.Status = newStatus;
            await _context.SaveChangesAsync();
            
            await NotifyLowStockAsync();

            var updatedOrders = await _context.Orders.Include(o => o.OrderItems).ToListAsync();
            return View("OrderList", updatedOrders);
        }



        [HttpPost]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var order = await _context.Orders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            if (order.Status != OrderStatus.Скасовано)
            {
                foreach (var item in order.OrderItems)
                {
                    var entry = await _context.WarehouseEntries.FirstOrDefaultAsync(e => e.ProductId == item.ProductId);
                    if (entry != null)
                    {
                        entry.Quantity += item.Quantity;
                        _context.WarehouseEntries.Update(entry);
                    }
                }
            }

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
            return RedirectToAction("OrderList");
        }

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

        private List<CartItem> GetCart()
        {
            var cart = HttpContext.Session.GetString("Cart");
            return cart != null ? JsonConvert.DeserializeObject<List<CartItem>>(cart) : new();
        }

        private void ClearCart()
        {
            HttpContext.Session.Remove("Cart");
        }
        
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
    }
}
