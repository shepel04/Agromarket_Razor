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
            if (!cart.Any()) return RedirectToAction("Index", "Cart");

            if (!ModelState.IsValid)
            {
                ViewBag.CartItems = cart;
                ViewBag.TotalAmount = cart.Sum(i => i.Price * i.Quantity);
                return View(order);
            }

            var entryIds = cart.Select(c => c.EntryId).ToList();
            var entries = await _context.WarehouseEntries
                .Include(e => e.Product)
                .Where(e => entryIds.Contains(e.Id))
                .ToListAsync();

            List<string> insufficient = new();
            List<WarehouseEntry> depletedEntries = new();

            foreach (var item in cart)
            {
                var entry = entries.FirstOrDefault(e => e.Id == item.EntryId);
                if (entry == null || (!entry.IsAvailableForPreorder && entry.Quantity < item.Quantity))
                {
                    insufficient.Add($"{item.Name} (доступно: {entry?.Quantity ?? 0}, потрібно: {item.Quantity})");
                }
            }

            if (insufficient.Any())
            {
                TempData["StockError"] = "Недостатньо залишків:<br>" + string.Join("<br>", insufficient);
                return RedirectToAction("Checkout");
            }

            foreach (var item in cart)
            {
                var entry = entries.First(e => e.Id == item.EntryId);
            
                if (entry.Quantity >= item.Quantity)
                {
                    // є достатньо товару — зменшуємо
                    entry.Quantity -= item.Quantity;
                    _context.WarehouseEntries.Update(entry);
            
                    if (entry.Quantity == 0)
                    {
                        depletedEntries.Add(entry);
                    }
                }
                else if (entry.IsAvailableForPreorder && entry.Quantity == 0)
                {
                    // Заглушка: логіка для передзамовлення, коли товару немає
                    // TODO: Реалізувати створення Preorder
                    Console.WriteLine($"Передзамовлення на товар: {entry.Product.Name} (x{item.Quantity})");
            
                }
                else
                {
                    throw new Exception($"Недостатньо товару \"{entry.Product.Name}\" на складі.");
                }
            }


            order.OrderItems = cart.Select(item => new OrderItem
            {
                ProductId = item.ProductId,
                ProductName = item.Name,
                Price = item.Price,
                Quantity = item.Quantity,
                Unit = item.Unit
            }).ToList();

            order.TotalAmount = cart.Sum(i => i.Price * i.Quantity);
            order.OrderDate = DateTime.UtcNow;
            order.Status = OrderStatus.Виконується;

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            var managers = new List<IdentityUser>();
            managers.AddRange(await _userManager.GetUsersInRoleAsync("clientmanager"));
            managers.AddRange(await _userManager.GetUsersInRoleAsync("admin"));

            foreach (var user in managers.Distinct())
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = user.Id,
                    Message = $"Нове замовлення #{order.Id} від {order.CustomerEmail}",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                    RedirectUrl = Url.Action("EditOrder", "Order", new { id = order.Id })
                });
            }

            if (depletedEntries.Any())
            {
                var warehouseManagers = await _userManager.GetUsersInRoleAsync("warehousemanager");
                var admins = await _userManager.GetUsersInRoleAsync("admin");
                var recipients = warehouseManagers.Concat(admins).Distinct();

                foreach (var user in recipients)
                {
                    foreach (var entry in depletedEntries)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            UserId = user.Id,
                            Message = $"Товар \"{entry.Product.Name}\" (в партії #{entry.Id}) закінчився.",
                            CreatedAt = DateTime.UtcNow,
                            IsRead = false
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            ClearCart();
            return RedirectToAction("OrderSuccess");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, OrderStatus newStatus)
        {
            var order = await _context.Orders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

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

            order.Status = newStatus;
            await _context.SaveChangesAsync();
            return RedirectToAction("OrderList");
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

            return View("OrderDetails", order);
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
    }
}
