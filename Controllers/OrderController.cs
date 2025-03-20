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
            if (!cart.Any())
            {
                return RedirectToAction("Index", "Cart");
            }

            var user = await _userManager.GetUserAsync(User);
            var userEmail = user?.Email ?? "";

            var order = new Order
            {
                CustomerEmail = userEmail
            };

            ViewBag.CartItems = cart;
            ViewBag.TotalAmount = cart.Sum(item => item.Price * item.Quantity);

            return View(order); // Переконайтеся, що це повертає Views/Order/Checkout.cshtml
        }


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

    // Отримуємо список товарів з бази даних для перевірки залишків
    var productIds = cart.Select(c => c.ProductId).ToList();
    var products = await _context.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();

    List<string> insufficientStockItems = new List<string>();

    foreach (var item in cart)
    {
        var product = products.FirstOrDefault(p => p.Id == item.ProductId);
        if (product == null || product.StockQuantity < item.Quantity)
        {
            insufficientStockItems.Add($"{item.Name} (доступно: {product?.StockQuantity ?? 0}, потрібно: {item.Quantity})");
        }
    }

    // Якщо недостатньо товарів – повертаємо помилку і не оформлюємо замовлення
    if (insufficientStockItems.Any())
    {
        TempData["StockError"] = "Недостатньо залишків для наступних товарів:<br>" + string.Join("<br>", insufficientStockItems);
        return RedirectToAction("Checkout");
    }

    // Зменшуємо кількість товарів на складі
    foreach (var item in cart)
    {
        var product = products.FirstOrDefault(p => p.Id == item.ProductId);
        if (product != null)
        {
            product.StockQuantity -= item.Quantity; // Віднімаємо кількість товарів на складі
        }
    }

    // Створюємо нове замовлення
    order.OrderItems = cart.Select(item => new OrderItem
    {
        ProductId = item.ProductId,
        ProductName = item.Name,
        Price = item.Price,
        Quantity = item.Quantity,
        Unit = item.Unit
    }).ToList();

    order.TotalAmount = cart.Sum(item => item.Price * item.Quantity);
    order.OrderDate = DateTime.UtcNow;
    order.Status = OrderStatus.Виконується;

    _context.Orders.Add(order);

    await _context.SaveChangesAsync();

    ClearCart();

    return RedirectToAction("OrderSuccess");
}

        
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
        public IActionResult EditOrder(int id)
        {
            var order = _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefault(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            ViewBag.Products = _context.Products.ToList();
            return View(order);
        }

        [HttpPost]
        public IActionResult EditOrder(Order order)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Products = _context.Products.ToList();
                return View(order);
            }

            var existingOrder = _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefault(o => o.Id == order.Id);

            if (existingOrder == null)
            {
                return NotFound();
            }

            // 🔹 Перетворення дати у UTC
            existingOrder.OrderDate = DateTime.SpecifyKind(order.OrderDate, DateTimeKind.Utc);

            existingOrder.CustomerName = order.CustomerName;
            existingOrder.CustomerEmail = order.CustomerEmail;
            existingOrder.CustomerPhone = order.CustomerPhone;
            existingOrder.DeliveryAddress = order.DeliveryAddress;
            existingOrder.Status = order.Status;

            existingOrder.OrderItems.Clear();

            if (order.OrderItems != null && order.OrderItems.Any())
            {
                foreach (var item in order.OrderItems)
                {
                    var product = _context.Products.FirstOrDefault(p => p.Id == item.ProductId);
                    if (product != null)
                    {
                        existingOrder.OrderItems.Add(new OrderItem
                        {
                            ProductId = item.ProductId,
                            ProductName = product.Name,
                            Price = product.SellingPrice ?? 0,
                            Quantity = item.Quantity,
                            Unit = product.Unit
                        });
                    }
                }
            }

            existingOrder.TotalAmount = existingOrder.OrderItems.Sum(i => i.Quantity * i.Price);

            _context.SaveChanges();

            return RedirectToAction("OrderList");
        }


        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, OrderStatus newStatus)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            var productIds = order.OrderItems.Select(i => i.ProductId).ToList();
            var products = await _context.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();

            // Якщо замовлення скасовується і раніше не було скасованим – повертаємо товари на склад
            if (newStatus == OrderStatus.Скасовано && order.Status != OrderStatus.Скасовано)
            {
                foreach (var item in order.OrderItems)
                {
                    var product = products.FirstOrDefault(p => p.Id == item.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity += item.Quantity; // Повертаємо товари на склад
                    }
                }
            }
            // Якщо замовлення переводиться з "Скасовано" назад у "Виконується" або "Виконано" – знову віднімаємо товари зі складу
            else if ((newStatus == OrderStatus.Виконується || newStatus == OrderStatus.Виконано) && order.Status == OrderStatus.Скасовано)
            {
                List<string> insufficientStockItems = new List<string>();

                foreach (var item in order.OrderItems)
                {
                    var product = products.FirstOrDefault(p => p.Id == item.ProductId);
                    if (product != null)
                    {
                        if (product.StockQuantity >= item.Quantity)
                        {
                            product.StockQuantity -= item.Quantity; // Віднімаємо товари зі складу
                        }
                        else
                        {
                            insufficientStockItems.Add($"{item.ProductName} (доступно: {product.StockQuantity}, потрібно: {item.Quantity})");
                        }
                    }
                }

                // Якщо товарів недостатньо – повідомляємо користувача і не змінюємо статус
                if (insufficientStockItems.Any())
                {
                    TempData["StockError"] = "Недостатньо залишків для відновлення замовлення:<br>" + string.Join("<br>", insufficientStockItems);
                    return RedirectToAction("OrderList");
                }
            }

            order.Status = newStatus;
            await _context.SaveChangesAsync();

            return RedirectToAction("OrderList");
        }

        
        [HttpGet]
        public IActionResult GetOrderDetails(int id)
        {
            var order = _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefault(o => o.Id == id);

            if (order == null)
            {
                return NotFound("Замовлення не знайдено.");
            }

            return PartialView("_OrderDetails", order);
        }





        public IActionResult OrderSuccess()
        {
            return View();
        }

        private List<CartItem> GetCart()
        {
            var cart = HttpContext.Session.GetString("Cart");
            return cart != null ? JsonConvert.DeserializeObject<List<CartItem>>(cart) : new List<CartItem>();
        }

        private void ClearCart()
        {
            HttpContext.Session.Remove("Cart");
        }
    }
}
