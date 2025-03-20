using Microsoft.AspNetCore.Mvc;
using Agromarket.Models;
using Agromarket.Data;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Agromarket.Controllers
{
    public class CartController : Controller
    {
        private const string CartSessionKey = "Cart";
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var cart = GetCart();
            ViewBag.TotalPrice = cart.Sum(p => p.Price * p.Quantity);
            return View(cart);
        }

        public IActionResult AddToCart(int productId, string name, decimal price, string unit, string imageBase64)
        {
            var product = _context.Products.FirstOrDefault(p => p.Id == productId);
            if (product == null || product.StockQuantity <= 0)
            {
                TempData["StockError"] = $"Товар '{name}' більше недоступний.";
                return RedirectToAction("Index");
            }

            var cart = GetCart();
            var existingItem = cart.FirstOrDefault(p => p.ProductId == productId);

            if (existingItem != null)
            {
                if (existingItem.Quantity < product.StockQuantity)
                {
                    existingItem.Quantity++;
                }
                else
                {
                    TempData["StockError"] = $"Максимальна кількість '{name}' у кошику: {product.StockQuantity}.";
                    return RedirectToAction("Index");
                }
            }
            else
            {
                cart.Add(new CartItem
                {
                    ProductId = productId,
                    Name = name,
                    Price = price,
                    Quantity = 1,
                    Unit = unit,
                    ImageBase64 = imageBase64
                });
            }

            SaveCart(cart);
            return RedirectToAction("Index");
        }
        
        public IActionResult ProceedToCheckout()
        {
            var cart = GetCart();
            if (!cart.Any())
            {
                TempData["StockError"] = "Ваш кошик порожній.";
                return RedirectToAction("Index");
            }

            return RedirectToAction("Checkout", "Order");   
        }


        [HttpPost]
        public JsonResult CheckStockBeforeCheckout()
        {
            var cart = GetCart();
            var insufficientStock = new List<object>();

            foreach (var item in cart)
            {
                var product = _context.Products.FirstOrDefault(p => p.Id == item.ProductId);
                if (product == null || product.StockQuantity < item.Quantity)
                {
                    insufficientStock.Add(new
                    {
                        name = item.Name,
                        requested = item.Quantity,
                        available = product?.StockQuantity ?? 0
                    });
                }
            }

            if (insufficientStock.Any())
            {
                return Json(new { success = false, insufficientStock });
            }

            return Json(new { success = true, redirectUrl = Url.Action("Checkout", "Order") });
        }


        [HttpPost]
        public JsonResult UpdateQuantityAjax(int productId, int quantity)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(p => p.ProductId == productId);
            var product = _context.Products.FirstOrDefault(p => p.Id == productId);

            if (item != null && product != null)
            {
                if (quantity > 0 && quantity <= product.StockQuantity)
                {
                    item.Quantity = quantity;
                }
                else if (quantity > product.StockQuantity)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Максимальна кількість '{item.Name}' у кошику: {product.StockQuantity}."
                    });
                }
                else
                {
                    cart.Remove(item);
                }

                SaveCart(cart);
            }

            decimal totalPrice = cart.Sum(p => p.Price * p.Quantity);

            return Json(new
            {
                success = true,
                totalItemPrice = item != null ? item.Price * item.Quantity : 0,
                totalPrice
            });
        }

        [HttpPost]
        public JsonResult RemoveFromCartAjax(int productId)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(p => p.ProductId == productId);

            if (item != null)
            {
                cart.Remove(item);
                SaveCart(cart);
            }

            decimal totalPrice = cart.Sum(p => p.Price * p.Quantity);

            return Json(new { success = true, totalPrice });
        }

        public IActionResult RemoveFromCart(int productId)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(p => p.ProductId == productId);
            if (item != null)
            {
                cart.Remove(item);
                SaveCart(cart);
            }
            return RedirectToAction("Index");
        }

        public IActionResult ClearCart()
        {
            HttpContext.Session.Remove(CartSessionKey);
            return RedirectToAction("Index");
        }

        private List<CartItem> GetCart()
        {
            var cart = HttpContext.Session.GetString(CartSessionKey);
            return string.IsNullOrEmpty(cart) ? new List<CartItem>() : JsonConvert.DeserializeObject<List<CartItem>>(cart);
        }

        private void SaveCart(List<CartItem> cart)
        {
            HttpContext.Session.SetString(CartSessionKey, JsonConvert.SerializeObject(cart));
        }
    }
}
