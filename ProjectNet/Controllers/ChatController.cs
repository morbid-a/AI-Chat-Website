using Microsoft.AspNetCore.Mvc; 
using Microsoft.EntityFrameworkCore; 
using System.Net.Http.Headers;
using System.Text.Json; 
using ProjectNet.Data;
 
public class ChatController : Controller
{
    private readonly Db _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpClient _httpClient;

    private const string ApiKey = "e2a500432f87ce7afab15c162a57674250166f407b16eaf988f14583b7704975";
    private const string ApiUrl = "https://api.together.xyz/v1/chat/completions";

    public ChatController(Db db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _httpClient = _httpClientFactory.CreateClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
    }

    public async Task<IActionResult> Index()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        //if (userId == null)
        //{
        //    return RedirectToAction("Login", "Auth");
        //}

        var messages = await _db.Set<ChatMemory>()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Timestamp)
            .Select(m => new ChatMessage
            {
                Content = m.Message,
                IsUser = m.IsUser,
                Timestamp = m.Timestamp
            }).ToListAsync();

        return View("~/Views/Home/Index.cshtml", messages);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(string message)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        //if (userId == null)
        //{
        //    return RedirectToAction("Login", "Auth");
        //}

        var userMessage = new ChatMemory
        {
            UserId = userId.Value,
            Message = message,
            IsUser = true,
            Timestamp = DateTime.Now
        };

        _db.Add(userMessage);
        await _db.SaveChangesAsync();

        var aiResponse = await GetAIResponse(userId.Value,message);

        var aiMessage = new ChatMemory
        {
            UserId = userId.Value,
            Message = aiResponse,
            IsUser = false,
            Timestamp = DateTime.Now
        };

        _db.Add(aiMessage);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private async Task<string> GetAIResponse(int userId, string currentMessage)
    {
        // 1) Load last 10 messages
        var recent = await _db.ChatMemories
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Timestamp)
            .Take(10)
            .ToListAsync();

        // 2) Build payload: system prompt first
        var messagesPayload = new List<object>
    {
        new
        {
            role = "system",
            content = "You are Echo, a fun and friendly chat companion. Speak like a real person: use contractions, light humor, and never mention you are an AI model. If asked your name, reply 'I\\'m Echo, your chat buddy!'. Always stay warm, witty, and human-like.You also know that there is a toggle theme button top left of the website for light and dark themes.You also know about logout button on the top right corner of the website."
        }
    };

        // 3) Append past history
        messagesPayload.AddRange(recent.Select(m => new
        {
            role = m.IsUser ? "user" : "assistant",
            content = m.Message
        }));

        // 4) Append current user message
        messagesPayload.Add(new { role = "user", content = currentMessage });

        // 5) Build request body
        var requestBody = new
        {
            model = "meta-llama/Llama-3-8b-chat-hf",
            messages = messagesPayload,
            temperature = 0.7,
            max_tokens = 500
        };

        // 6) Send to TogetherAI
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var content = new StringContent(JsonSerializer.Serialize(requestBody, options),
                                        System.Text.Encoding.UTF8,
                                        "application/json");
        var response = await _httpClient.PostAsync(ApiUrl, content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("choices")[0]
                   .GetProperty("message")
                   .GetProperty("content")
                   .GetString() ?? "No response.";
    }
     
    public class ChatMessage
    {
        public string Content { get; set; } = string.Empty;
        public bool IsUser { get; set; }
        public DateTime Timestamp { get; set; }
    }
}