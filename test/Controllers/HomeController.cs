using Microsoft.AspNetCore.Mvc;
using ProjectNet;
using ProjectNet.Models;

namespace ProjectNet.Controllers
{
    public class HomeController : Controller
    {
        [HttpPost]
        public IActionResult ToggleTheme(string theme)
        {
            Response.Cookies.Append("theme", theme, new CookieOptions
            {
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddYears(1)
            });
            return Redirect(Request.Headers["Referer"].ToString() ?? "/");
        }

        [HttpGet]
        public IActionResult Index()
        {
            // Allow either a valid session OR the fallback auth cookie set on login.
            var hasSession = HttpContext.Session.Keys.Contains("UserId");
            var hasAuthCookie = Request.Cookies.ContainsKey("AuthUser");

            if (!hasSession && !hasAuthCookie)
                return RedirectToAction("Login", "Account");

            // Load recent chat history from session so messages persist on reload
            var history = HttpContext.Session.GetObject<List<ChatMessage>>("ChatHistory")
                           ?? new List<ChatMessage>();

            return View(history);
        }
    }
}


