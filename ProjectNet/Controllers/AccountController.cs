using Microsoft.AspNetCore.Mvc;
using ProjectNet.Models;
using ProjectNet.Services; 

namespace ProjectNet.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserService _userService;
        private readonly IWebHostEnvironment _env;

        public AccountController(UserService userService, IWebHostEnvironment env)
        {
            _userService = userService;
            _env = env;
        }

        [HttpGet]
        public IActionResult Login()
        {
            // Redirect if already logged in
            if (HttpContext.Session.GetInt32("UserId") != null)
            {
                return RedirectToAction("Index", "Home");
            }
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _userService.ValidateUserAsync(model.Username, model.Password, model.RememberMe);

            if (result.Succeeded)
            {
                var user = await _userService.GetByUsernameAsync(model.Username);
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "User not found.");
                    return View(model);
                }

                HttpContext.Session.SetInt32("UserId", user.Id);
                HttpContext.Session.SetString("Username", user.Username);

                if (model.RememberMe)
                {
                    var token = Guid.NewGuid();
                    var expiry = DateTime.UtcNow.AddDays(7);

                    user.RememberToken = token;
                    user.RememberTokenExpiry = expiry;
                    await _userService.UpdateRememberTokenAsync(user);

                    Response.Cookies.Append("RememberMeToken", token.ToString(), new CookieOptions
                    {
                        Expires = expiry,
                        HttpOnly = true,
                        Secure = !_env.IsDevelopment(),
                        SameSite = SameSiteMode.Lax,
                        Path = "/"
                    });
                }

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, "Invalid username or password");
            return View(model);
        }


        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _userService.CreateUserAsync(model.Username, model.Password, model.Email);
            if (result.Succeeded)
            {
                return RedirectToAction("Login");
            }

            // Add errors from IdentityResult to the model state
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete("RememberMeToken", new CookieOptions
            {
                Secure = !_env.IsDevelopment(), // Environment-aware
                SameSite = SameSiteMode.Lax
            });
            return RedirectToAction("Login");
        }
    }
}