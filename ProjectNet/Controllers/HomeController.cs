using Microsoft.AspNetCore.Mvc; 

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
            if (!HttpContext.Session.Keys.Contains("UserId"))
                return RedirectToAction("Login", "Account");

            return View();
        } 
    }
}
