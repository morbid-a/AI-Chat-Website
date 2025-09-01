// Program.cs - Main entry point for the ASP.NET Core application
// Sets up dependency injection, middleware pipeline, routing, and helper methods.

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectNet.Data;
using ProjectNet.Models;
using ProjectNet.Services;

// Build the web application and configure services
var builder = WebApplication.CreateBuilder(args);

// ========================= SERVICE CONFIGURATION ========================= //

// Enable MVC controllers and Razor views
builder.Services.AddControllersWithViews();

// Allow services and middleware to access HttpContext
builder.Services.AddHttpContextAccessor();

// Register HttpClientFactory for injecting HttpClient
builder.Services.AddHttpClient();

// ------------------------- Session Configuration ------------------------- //
builder.Services.AddSession(options =>
{
    // Session timeout after 30 minutes of inactivity
    options.IdleTimeout = TimeSpan.FromMinutes(30);

    // Cookie settings for security and compliance
    options.Cookie.HttpOnly = true;         
    options.Cookie.IsEssential = true;     // Always sent
    options.Cookie.SameSite = SameSiteMode.Lax; // Mitigates CSRF (web attack)
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.None    // Allow HTTP in development
        : CookieSecurePolicy.Always; // Require HTTPS in production
});

// ----------------------- Database Configuration ------------------------- //
// Register Application DbContext using SQLite and connection string from appsettings.json
builder.Services.AddDbContext<Db>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// ---------------------- Application Services ---------------------------- //
// Register custom services for DI (one instance per HTTP request)
builder.Services.AddScoped<UserService>();
//builder.Services.AddScoped<AIService>();

// Register password hasher as a singleton (shared across requests)
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();   //1st is interface 2nd is implementation

// ============================== BUILD ===================================== //
// Finalize service registrations and build the app
var app = builder.Build();

// ========================= MIDDLEWARE PIPELINE ============================= //

// Redirect HTTP requests to HTTPS
app.UseHttpsRedirection();

// Serve static files (CSS, JS, images) from wwwroot/
app.UseStaticFiles();

// Enable routing
app.UseRouting();

// Enable session state
app.UseSession();

// --------- Custom Remember-Me Middleware (runs before authorization) ------- //
app.Use(async (context, next) =>
{
    // Attempt to restore user session from "RememberMeToken" cookie
    await HandleRememberTokenAsync(context);

    // Redirect authenticated users away from the login page to home
    if (context.Request.Path.StartsWithSegments("/Account/Login") &&
        context.Session.GetInt32("UserId") != null)
    {
        context.Response.Redirect("/Home/Index");
        return; // Stop further pipeline execution
    }

    // Continue processing other middleware
    await next();
});

// --------- Error Handling and HSTS in Production --------- //
if (!app.Environment.IsDevelopment())
{
    // Route unhandled exceptions to custom error page
    app.UseExceptionHandler("/Home/Error");
    // Add HTTP Strict Transport Security header
    app.UseHsts();
}
// ========================= ROUTING ========================================= //
// Map default route: /Account/Login if no controller/action specified
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}"
);

// Start the web application and listen for HTTP requests
await app.RunAsync();

// ========================= HELPER METHODS ================================= //

/// <summary>
/// Checks for a valid "RememberMeToken" cookie and restores the user's session if valid.
/// Otherwise, the token is cleared.
/// </summary>
static async Task HandleRememberTokenAsync(HttpContext context)
{
    // If session already contains UserId, skip
    if (context.Session.GetInt32("UserId") != null)
        return;

    // Read the remember-me token from cookies
    var token = context.Request.Cookies["RememberMeToken"];
    if (string.IsNullOrEmpty(token))
        return;

    // Parse token as GUID
    if (Guid.TryParse(token, out var tokenGuid))
    {
        // Lookup user by token
        var userService = context.RequestServices.GetRequiredService<UserService>();
        var user = await userService.GetUserByRememberTokenAsync(tokenGuid);

        // If token is valid and not expired, restore session and renew cookie
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
            // Clear expired or invalid token
            ClearInvalidToken(context);
        }
    }
    else
    {
        // Clear token if it isn't a valid GUID
        ClearInvalidToken(context);
    }
}

/// <summary>
/// Clears the "RememberMeToken" cookie using the same security settings as when it was set.
/// </summary>
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
