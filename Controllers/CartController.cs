using Microsoft.AspNetCore.Mvc;
using Agromarket.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Agromarket.Controllers
{
    public class CartController : Controller
    {
        private const string CartSessionKey = "Cart";

        public IActionResult Index()
        {
            var cart = GetCart();
            return View(cart);
        }
        
        public IActionResult AddToCart(int productId, string name, decimal price, string unit, string imageBase64)
        {
            var cart = GetCart();
            var existingItem = cart.FirstOrDefault(p => p.ProductId == productId);

            if (existingItem != null)
            {
                existingItem.Quantity++;
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

        [HttpPost]
        public JsonResult UpdateQuantityAjax(int productId, int quantity)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(p => p.ProductId == productId);

            if (item != null)
            {
                if (quantity > 0)
                {
                    item.Quantity = quantity;
                }
                else
                {
                    cart.Remove(item);
                }

                SaveCart(cart);
            }

            decimal totalPrice = cart.Sum(p => p.Price * p.Quantity);

            return Json(new { success = true, totalItemPrice = item?.Price * item?.Quantity, totalPrice });
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
            return cart != null ? JsonConvert.DeserializeObject<List<CartItem>>(cart) : new List<CartItem>();
        }

        private void SaveCart(List<CartItem> cart)
        {
            HttpContext.Session.SetString(CartSessionKey, JsonConvert.SerializeObject(cart));
        }
    }
}
