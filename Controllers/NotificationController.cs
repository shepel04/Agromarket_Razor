using Agromarket.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;

namespace Agromarket.Controllers;

[Authorize]
public class NotificationController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly TelegramNotifier _telegramNotifier;

    public NotificationController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;

        var botToken = "8164953160:AAELQNH5vEWTp7CA32TnGqEf1g2ljpB56Po";
        var chatId = "-1002666519312"; 
        _telegramNotifier = new TelegramNotifier(botToken, chatId);
    }

    [HttpGet]
    public async Task<IActionResult> GetUserNotifications()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Json(new List<object>());

        var notifications = _context.Notifications
            .Where(n => n.UserId == user.Id && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(5)
            .Select(n => new { n.Id, n.Message })
            .ToList();

        return Json(notifications);
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = _userManager.GetUserId(User);
        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> CreateNotification(string userId, string message)
    {
        if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(userId))
            return BadRequest();

        var notification = new Models.Notification
        {
            UserId = userId,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        await _telegramNotifier.SendMessageAsync($"🔔 Сповіщення для користувача {userId}:\n{message}");

        return Ok();
    }

    
}
