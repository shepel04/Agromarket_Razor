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
