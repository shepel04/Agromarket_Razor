using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agromarket.Controllers
{
    public class PaymentController : Controller
    {
        /// <summary>
        /// Сторінка передплати для передзамовлення.
        /// </summary>
        [HttpGet]
        public IActionResult Preorder(decimal amount)
        {
            if (amount <= 0)
            {
                TempData["PaymentError"] = "Сума передплати недійсна.";
                return RedirectToAction("Checkout", "Order");
            }

            ViewBag.Amount = amount;
            return View();
        }

        /// <summary>
        /// Симуляція обробки оплати
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProcessPreorderPayment(decimal amount)
        {
            if (amount <= 0)
            {
                TempData["PaymentError"] = "Сума передплати недійсна.";
                return RedirectToAction("Checkout", "Order");
            }

            // TODO: Інтеграція з платіжною системою
            TempData["PaymentSuccess"] = $"Передплату на суму {amount:0.00} грн успішно оплачено.";
            return RedirectToAction("OrderSuccess", "Order");
        }
    }
}