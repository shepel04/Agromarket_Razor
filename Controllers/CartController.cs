using Microsoft.AspNetCore.Mvc;
using Agromarket.Models;
using Agromarket.Data;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

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

            foreach (var item in cart)
            {
                var entry = _context.WarehouseEntries.FirstOrDefault(e => e.Id == item.EntryId);
                item.MaxQuantity = entry?.Quantity ?? 0;
            }

            ViewBag.TotalPrice = cart.Sum(p => p.Price * p.Quantity);
            ViewBag.TotalDeposit = cart.Sum(p => p.DepositAmount);

            SaveCart(cart);
            return View(cart);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddToCart(int entryId, int quantity = 1, bool isPreorder = false, DateTime? preorderDate = null)
        {
            var entry = _context.WarehouseEntries
                .Include(e => e.Product)
                .FirstOrDefault(e => e.Id == entryId);

            if (entry == null || (entry.Quantity <= 0 && !entry.IsAvailableForPreorder))
            {
                TempData["StockError"] = "Товар більше недоступний.";
                return RedirectToAction("Index");
            }

            var cart = GetCart();

            bool cartContainsPreorder = cart.Any(i => i.IsPreorder);
            bool addingPreorder = entry.Quantity == 0 && entry.IsAvailableForPreorder;

            if (cart.Any())
            {
                if (cartContainsPreorder && !addingPreorder)
                {
                    TempData["StockError"] = "У кошику вже є передзамовлення. Завершіть його перед додаванням інших товарів.";
                    return RedirectToAction("Index");
                }

                if (!cartContainsPreorder && addingPreorder)
                {
                    TempData["StockError"] = "У кошику вже є звичайні товари. Завершіть замовлення перед додаванням передзамовлення.";
                    return RedirectToAction("Index");
                }
            }

            var existingItem = cart.FirstOrDefault(p => p.EntryId == entryId);

            decimal basePrice = entry.HasDiscount == true && entry.DiscountedPrice.HasValue
                ? entry.DiscountedPrice.Value
                : (entry.SellingPrice ?? 0);

            bool isBulkOrder = quantity >= 50;
            bool isPrivileged = User.IsInRole("admin") || User.IsInRole("enterpriseclient");
            bool isClient = User.IsInRole("client");

            decimal bulkPrice = Math.Round(basePrice * 0.95m, 2);

            decimal finalPrice = (isBulkOrder && isPrivileged) ? bulkPrice : basePrice;

            decimal deposit = (isBulkOrder && isClient && !isPrivileged)
                ? Math.Round(finalPrice * quantity * 0.2m, 2)
                : 0;
            
            if (isBulkOrder && isClient && !isPrivileged)
            {
                deposit = Math.Round(finalPrice * quantity * 0.2m, 2);
            }

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;

                bool newBulk = existingItem.Quantity >= 50;
                bool isClientNow = User.IsInRole("client");

                bool nowPrivileged = User.IsInRole("admin") || User.IsInRole("enterpriseclient");
                existingItem.Price = (newBulk && nowPrivileged)
                    ? Math.Round(basePrice * 0.95m, 2)
                    : basePrice;

                existingItem.DepositAmount = (newBulk && isClientNow && !nowPrivileged)
                    ? Math.Round(existingItem.Price * existingItem.Quantity * 0.2m, 2)
                    : 0;
            }
            else
            {
                cart.Add(new CartItem
                {
                    EntryId = entry.Id,
                    ProductId = entry.ProductId,
                    Name = entry.Product.Name,
                    Price = finalPrice,
                    Quantity = quantity,
                    Unit = entry.Product.Unit,
                    ImageBase64 = entry.Product.ImageData != null ? Convert.ToBase64String(entry.Product.ImageData) : null,
                    MaxQuantity = entry.Quantity,
                    IsPreorder = addingPreorder,
                    PreorderDate = preorderDate,
                    DepositAmount = deposit
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
                var entry = _context.WarehouseEntries.FirstOrDefault(e => e.Id == item.EntryId);
                if (entry == null || (!entry.IsAvailableForPreorder && entry.Quantity < item.Quantity))
                {
                    insufficientStock.Add(new
                    {
                        name = item.Name,
                        requested = item.Quantity,
                        available = entry?.Quantity ?? 0
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
        public JsonResult UpdateQuantityAjax(int entryId, int quantity)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(p => p.EntryId == entryId);
            var entry = _context.WarehouseEntries.FirstOrDefault(e => e.Id == entryId);

            if (item != null && entry != null)
            {
                item.MaxQuantity = entry.Quantity;

                if (quantity > 0 && (entry.IsAvailableForPreorder || quantity <= entry.Quantity))
                {
                    item.Quantity = quantity;

                    bool isBulk = quantity >= 50;
                    bool isPrivileged = User.IsInRole("admin") || User.IsInRole("enterpriseclient");
                    bool isClient = User.IsInRole("client");

                    decimal basePrice = entry.HasDiscount == true && entry.DiscountedPrice.HasValue
                        ? entry.DiscountedPrice.Value
                        : (entry.SellingPrice ?? 0);

                    item.Price = (isBulk && isPrivileged)
                        ? Math.Round(basePrice * 0.95m, 2)
                        : basePrice;

                    item.DepositAmount = (isBulk && isClient && !isPrivileged)
                        ? Math.Round(item.Price * quantity * 0.2m, 2)
                        : 0;

                    item.IsPreorder = entry.Quantity == 0 && entry.IsAvailableForPreorder;
                }
                else if (quantity > entry.Quantity && !entry.IsAvailableForPreorder)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Максимальна кількість '{item.Name}' у кошику: {entry.Quantity}."
                    });
                }
                else
                {
                    cart.Remove(item);
                }

                SaveCart(cart);
            }

            decimal totalPrice = cart.Sum(p => p.Price * p.Quantity);
            decimal totalDeposit = cart.Sum(p => p.DepositAmount);

            return Json(new
            {
                success = true,
                totalItemPrice = item != null ? item.Price * item.Quantity : 0,
                totalPrice,
                totalDeposit,
                price = item?.Price,
                deposit = item?.DepositAmount,
                isPreorder = item?.IsPreorder ?? false
            });
        }


        [HttpPost]
        public JsonResult RemoveFromCartAjax(int entryId)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(p => p.EntryId == entryId);

            if (item != null)
            {
                cart.Remove(item);
                SaveCart(cart);
            }

            decimal totalPrice = cart.Sum(p => p.Price * p.Quantity);
            decimal totalDeposit = cart.Sum(p => p.DepositAmount);

            return Json(new { success = true, totalPrice, totalDeposit });
        }

        public IActionResult RemoveFromCart(int entryId)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(p => p.EntryId == entryId);
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
