using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectNet.Data;
using ProjectNet.Models;
using ProjectNet.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC + views
builder.Services.AddControllersWithViews();

// HttpContext + HttpClient
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// Session (HTTP only, non-secure for HTTP hosting)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
});

// SQLite DbContext
builder.Services.AddDbContext<Db>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// Auth helpers
builder.Services.AddScoped<UserService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

var app = builder.Build();

// Ensure DB exists
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Db>();
    db.Database.EnsureCreated();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

// Restore remember-me sessions before controller logic
app.Use(async (context, next) =>
{
    await HandleRememberTokenAsync(context);

    if (context.Request.Path.StartsWithSegments("/Account/Login") &&
        context.Session.GetInt32("UserId") != null)
    {
        context.Response.Redirect("/Home/Index");
        return;
    }

    await next();
});

// --------- Error Handling and HSTS in Production --------- //
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Routes
app.MapControllerRoute(
    name: "home_page",
    pattern: "home-page",
    defaults: new { controller = "Home", action = "Index" }
);

// Friendly routes for auth pages
app.MapControllerRoute(
    name: "login_page",
    pattern: "login",
    defaults: new { controller = "Account", action = "Login" }
);

app.MapControllerRoute(
    name: "register_page",
    pattern: "register",
    defaults: new { controller = "Account", action = "Register" }
);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}"
);

await app.RunAsync();

// Remember-me cookie helpers
static async Task HandleRememberTokenAsync(HttpContext context)
{
    if (context.Session.GetInt32("UserId") != null)
        return;

    var token = context.Request.Cookies["RememberMeToken"];
    if (string.IsNullOrEmpty(token))
        return;

    if (Guid.TryParse(token, out var tokenGuid))
    {
        var userService = context.RequestServices.GetRequiredService<UserService>();
        var user = await userService.GetUserByRememberTokenAsync(tokenGuid);

        if (user?.RememberTokenExpiry > DateTime.UtcNow)
        {
            context.Session.SetInt32("UserId", user.Id);
            context.Session.SetString("Username", user.Username);

            context.Response.Cookies.Append(
                "RememberMeToken",
                token,
                new CookieOptions
                {
                    Expires = DateTime.UtcNow.AddDays(7),
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = !context.RequestServices
                                  .GetRequiredService<IWebHostEnvironment>()
                                  .IsDevelopment()
                }
            );
        }
        else
        {
            ClearInvalidToken(context);
        }
    }
    else
    {
        ClearInvalidToken(context);
    }
}

static void ClearInvalidToken(HttpContext context)
{
    context.Response.Cookies.Delete(//deletes cookie with specified properties otherwise different cookie
        "RememberMeToken",
        new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = !context.RequestServices
                          .GetRequiredService<IWebHostEnvironment>()
                          .IsDevelopment()
        }
    );
}


