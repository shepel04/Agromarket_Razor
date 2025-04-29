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
            int currentQuantityInCart = existingItem?.Quantity ?? 0;
            int totalRequestedQuantity = currentQuantityInCart + quantity;

            if (!entry.IsAvailableForPreorder && totalRequestedQuantity > entry.Quantity)
            {
                TempData["StockError"] = $"Неможливо додати {quantity} одиниць товару '{entry.Product.Name}'. Доступно тільки {entry.Quantity - currentQuantityInCart}.";
                return RedirectToAction("Index");
            }

            // НОВА ЛОГІКА:
            if (entry.IsAvailableForPreorder && totalRequestedQuantity > entry.Quantity)
            {
                TempData["StockError"] = $"Ви запитали більше товару, ніж є на складі. Ви зможете оформити передзамовлення на '{entry.Product.Name}' після завершення поточного замовлення.";
                return RedirectToAction("Index");
            }

            // Додаємо товар у кошик як зазвичай (далі йде твоя існуюча логіка)
            decimal basePrice = entry.HasDiscount == true && entry.DiscountedPrice.HasValue
                ? entry.DiscountedPrice.Value
                : (entry.SellingPrice ?? 0);

            bool isBulkOrder = totalRequestedQuantity >= 50;
            bool isPrivileged = User.IsInRole("admin") || User.IsInRole("enterpriseclient");
            bool isClient = User.IsInRole("client");

            decimal bulkPrice = Math.Round(basePrice * 0.95m, 2);
            decimal finalPrice = (isBulkOrder && isPrivileged) ? bulkPrice : basePrice;
            decimal deposit = (isBulkOrder && isClient && !isPrivileged)
                ? Math.Round(finalPrice * totalRequestedQuantity * 0.2m, 2)
                : 0;

            if (existingItem != null)
            {
                existingItem.Quantity = totalRequestedQuantity;
                existingItem.Price = finalPrice;
                existingItem.DepositAmount = deposit;
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
        public JsonResult UpdateQuantityAjax(int entryId, int quantity, string mode = null)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(p => p.EntryId == entryId);
            var entry = _context.WarehouseEntries
                .Include(e => e.Product)
                .FirstOrDefault(e => e.Id == entryId);

            if (item == null || entry == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Товар не знайдено у кошику або на складі."
                });
            }

            int available = entry.Quantity;
            bool isPreorderAllowed = entry.IsAvailableForPreorder;
            bool isPrivileged = User.IsInRole("admin") || User.IsInRole("enterpriseclient");
            bool isClient = User.IsInRole("client");

            decimal basePrice = entry.HasDiscount == true && entry.DiscountedPrice.HasValue
                ? entry.DiscountedPrice.Value
                : (entry.SellingPrice ?? 0);

            decimal bulkPrice = Math.Round(basePrice * 0.95m, 2);

            // Видаляємо стару позицію
            cart.Remove(item);

            if (quantity <= available || string.IsNullOrEmpty(mode))
            {
                // Оновлення звичайної кількості
                var isBulk = quantity >= 50;
                cart.Add(new CartItem
                {
                    EntryId = entry.Id,
                    ProductId = entry.ProductId,
                    Name = entry.Product.Name,
                    Unit = entry.Product.Unit,
                    ImageBase64 = entry.Product.ImageData != null ? Convert.ToBase64String(entry.Product.ImageData) : null,
                    Quantity = quantity,
                    MaxQuantity = available,
                    Price = (isBulk && isPrivileged) ? bulkPrice : basePrice,
                    DepositAmount = (isBulk && isClient && !isPrivileged)
                        ? Math.Round(((isBulk && isPrivileged) ? bulkPrice : basePrice) * quantity * 0.2m, 2)
                        : 0,
                    IsPreorder = false
                });
            }
            else if (mode == "split" && isPreorderAllowed && quantity > available)
            {
                int preorderQuantity = quantity - available;

                // Додаємо залишок як звичайне замовлення
                if (available > 0)
                {
                    var isBulkAvailable = available >= 50;
                    cart.Add(new CartItem
                    {
                        EntryId = entry.Id,
                        ProductId = entry.ProductId,
                        Name = entry.Product.Name,
                        Unit = entry.Product.Unit,
                        ImageBase64 = entry.Product.ImageData != null ? Convert.ToBase64String(entry.Product.ImageData) : null,
                        Quantity = available,
                        MaxQuantity = available,
                        Price = (isBulkAvailable && isPrivileged) ? bulkPrice : basePrice,
                        DepositAmount = (isBulkAvailable && isClient && !isPrivileged)
                            ? Math.Round(((isBulkAvailable && isPrivileged) ? bulkPrice : basePrice) * available * 0.2m, 2)
                            : 0,
                        IsPreorder = false
                    });
                }

                // Додаємо залишок як передзамовлення
                var isBulkPreorder = preorderQuantity >= 50;
                cart.Add(new CartItem
                {
                    EntryId = entry.Id,
                    ProductId = entry.ProductId,
                    Name = entry.Product.Name,
                    Unit = entry.Product.Unit,
                    ImageBase64 = entry.Product.ImageData != null ? Convert.ToBase64String(entry.Product.ImageData) : null,
                    Quantity = preorderQuantity,
                    MaxQuantity = 0,
                    Price = (isBulkPreorder && isPrivileged) ? bulkPrice : basePrice,
                    DepositAmount = (isBulkPreorder && isClient && !isPrivileged)
                        ? Math.Round(((isBulkPreorder && isPrivileged) ? bulkPrice : basePrice) * preorderQuantity * 0.2m, 2)
                        : 0,
                    IsPreorder = true,
                    PreorderDate = DateTime.UtcNow.AddDays(7) // Або іншу дату очікуваного надходження
                });
            }
            else if (mode == "preorder" && isPreorderAllowed)
            {
                // Замовити все як передзамовлення
                var isBulkPreorder = quantity >= 50;
                cart.Add(new CartItem
                {
                    EntryId = entry.Id,
                    ProductId = entry.ProductId,
                    Name = entry.Product.Name,
                    Unit = entry.Product.Unit,
                    ImageBase64 = entry.Product.ImageData != null ? Convert.ToBase64String(entry.Product.ImageData) : null,
                    Quantity = quantity,
                    MaxQuantity = 0,
                    Price = (isBulkPreorder && isPrivileged) ? bulkPrice : basePrice,
                    DepositAmount = (isBulkPreorder && isClient && !isPrivileged)
                        ? Math.Round(((isBulkPreorder && isPrivileged) ? bulkPrice : basePrice) * quantity * 0.2m, 2)
                        : 0,
                    IsPreorder = true,
                    PreorderDate = DateTime.UtcNow.AddDays(7)
                });
            }
            else
            {
                return Json(new
                {
                    success = false,
                    message = "Неможливо оформити замовлення у вказаній кількості."
                });
            }

            SaveCart(cart);

            decimal totalPrice = cart.Sum(p => p.Price * p.Quantity);
            decimal totalDeposit = cart.Sum(p => p.DepositAmount);

            return Json(new
            {
                success = true,
                totalPrice,
                totalDeposit
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
