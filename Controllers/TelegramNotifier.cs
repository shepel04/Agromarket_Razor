using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

public class TelegramNotifier
{
    private readonly string _botToken;
    private readonly string _chatId;

    public TelegramNotifier(string botToken, string chatId)
    {
        _botToken = botToken;
        _chatId = chatId;
    }

    public async Task SendMessageAsync(string message)
    {
        Console.WriteLine("📤 Надсилаємо у Telegram: " + message); // DEBUG

        using var client = new HttpClient();
        var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";

        var content = new StringContent(
            $"{{\"chat_id\":\"{_chatId}\",\"text\":\"{message}\"}}",
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync(url, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        Console.WriteLine("📥 Відповідь Telegram: " + responseBody); // DEBUG
    }

}