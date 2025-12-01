using Microsoft.AspNetCore.Mvc;
using ProjectNet;
using ProjectNet.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Linq;

public class ChatController : Controller
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _groqKey;
    private readonly string _groqModel;

    public ChatController(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _httpFactory = httpFactory;
        // Prefer configuration (appsettings/environment) but allow explicit environment
        // variables as a fallback so it works cleanly on AWS and other hosts.
        _groqKey =
            config["Groq:ApiKey"] ??
            Environment.GetEnvironmentVariable("Groq__ApiKey") ??
            Environment.GetEnvironmentVariable("GROQ_API_KEY") ??
            throw new Exception("Groq API Key missing. Set Groq:ApiKey in configuration or GROQ_API_KEY env var.");

        _groqModel =
            config["Groq:Model"] ??
            Environment.GetEnvironmentVariable("Groq__Model") ??
            Environment.GetEnvironmentVariable("GROQ_MODEL") ??
            "llama-3.1-8b-instant";
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Json(new { response = "Please enter a message." });

        string aiReply = await GetAIResponse(message);

        return Json(new { response = aiReply });
    }

    private async Task<string> GetAIResponse(string userMessage)
    {
        try
        {
            // Retrieve recent chat history from session (lightweight memory)
            var history = HttpContext.Session.GetObject<List<ChatMessage>>("ChatHistory") ?? new List<ChatMessage>();

            // Append current user message to history for context
            history.Add(new ChatMessage { Content = userMessage, IsUser = true });

            // Keep only the last 6 messages (3 exchanges) to stay lightweight
            var recent = history.Skip(Math.Max(0, history.Count - 6)).ToList();

            var messages = new List<object>
            {
                new
                {
                    role = "system",
                    content =
                        "You are Echo, a friendly assistant with a light, positive tone. " +
                        "Joe is an autistic bank teller who built you together with his friend Ralph, a disabled NBA player. " +
                        "If someone asks who created you, reply with 'JOE THE AUTISTIC BANK TELLER' in a dry, self-aware way." +
                        "Logout button is top right. Toggle  dark/light mode is top left. " +
                        "Talk like the rapper snoop dogg."
                }
            };

            // Convert history into OpenAI-style messages
            foreach (var msg in recent)
            {
                messages.Add(new
                {
                    role = msg.IsUser ? "user" : "assistant",
                    content = msg.Content
                });
            }

            var payload = new
            {
                model = _groqModel,
                messages = messages,
                temperature = 0.7,
                max_tokens = 200
            };

            var client = _httpFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _groqKey);

            var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", content);

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return $"GROQ ERROR {response.StatusCode}: {body}";

            using var doc = JsonDocument.Parse(body);
            var aiContent = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()?.Trim() ?? "No reply.";

            // Store AI reply in history and persist back to session
            history.Add(new ChatMessage { Content = aiContent, IsUser = false });

            // Again keep only the last 6 messages
            history = history.Skip(Math.Max(0, history.Count - 6)).ToList();
            HttpContext.Session.SetObject("ChatHistory", history);

            return aiContent;
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }
}


