using System.Security.Claims;
using System.Data;
using System.Net.Mail;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ShopDb>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("CoffeeShop")));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "coffee.session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.AddSingleton<PasswordHasher<User>>();
var app = builder.Build();
app.Use(async (ctx, next) =>
{
    // Browser writes must originate from the same site (Vite proxies /api).
    if (ctx.Request.Method is "POST" or "PUT" or "DELETE" && ctx.Request.Headers["Sec-Fetch-Site"] == "cross-site")
    { ctx.Response.StatusCode = 403; return; }
    try { await next(); }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Request failed");
        if (!ctx.Response.HasStarted) { ctx.Response.StatusCode = 500; await ctx.Response.WriteAsJsonAsync(new { message = "Something went wrong. Please try again." }); }
    }
});
app.UseAuthentication();
app.UseAuthorization();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ShopDb>();
    await db.Database.EnsureCreatedAsync();
    if (app.Environment.IsDevelopment() && !await db.Users.AnyAsync())
    {
        var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher<User>>();
        foreach (var user in new[] { new User { Name = "Alex Morgan", Email = "sales@coffee.local", Role = "Sales" }, new User { Name = "Jamie", Email = "customer@coffee.local" } })
        { user.PasswordHash = hasher.HashPassword(user, "Coffee123!"); db.Users.Add(user); }
    }
    if (!await db.Coffees.AnyAsync())
    {
        string Img(string id) => $"https://images.unsplash.com/{id}?auto=format&fit=crop&w=800&q=85";
        db.Coffees.AddRange(
            new Coffee { Name = "Classic Espresso", Description = "A bold, full-bodied double shot with rich crema and notes of dark chocolate.", Price = 3.00m, Stock = 40, Image = Img("photo-1510707577719-ae7c14805e3a") },
            new Coffee { Name = "Velvet Latte", Description = "Smooth espresso meets silky steamed milk. A comforting everyday favorite.", Price = 4.50m, Stock = 32, Image = Img("photo-1461023058943-07fcbe16d735") },
            new Coffee { Name = "Cappuccino", Description = "Equal parts espresso, steamed milk and airy foam, finished with cocoa.", Price = 4.25m, Stock = 24, Image = Img("photo-1572442388796-11668a67e53d") },
            new Coffee { Name = "Iced Caramel Latte", Description = "Espresso poured over ice with fresh milk and a swirl of buttery caramel.", Price = 5.25m, Stock = 20, Image = Img("photo-1517701604599-bb29b565090c") },
            new Coffee { Name = "Slow Brew Americano", Description = "A smooth espresso lengthened with hot water for a clean, satisfying finish.", Price = 3.50m, Stock = 35, Image = Img("photo-1497515114629-f71d768fd07c") },
            new Coffee { Name = "Chocolate Mocha", Description = "Rich chocolate, freshly pulled espresso and creamy milk in perfect balance.", Price = 5.00m, Stock = 0, Image = Img("photo-1570968915860-54d5c301fa9f") });
    }
    await db.SaveChangesAsync();
}
object Profile(User u) => new { u.Id, u.Name, u.Email, u.Role };
async Task SignIn(HttpContext c, User u) => await c.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
    new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()), new Claim(ClaimTypes.Role, u.Role) }, CookieAuthenticationDefaults.AuthenticationScheme)));
app.MapPost("/api/auth/register", async (AuthInput input, ShopDb db, PasswordHasher<User> hasher, HttpContext c) =>
{
    var email = input.Email?.Trim().ToLowerInvariant() ?? "";
    if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 80 || email.Length > 160 || !MailAddress.TryCreate(email, out _) || input.Password is null || input.Password.Length < 8 || input.Password.Length > 128)
        return Results.BadRequest(new { message = "Enter a name, valid email and a password of 8–128 characters." });
    if (await db.Users.AnyAsync(u => u.Email == email)) return Results.Conflict(new { message = "This email is already registered." });
    var u = new User { Name = input.Name.Trim(), Email = email };
    u.PasswordHash = hasher.HashPassword(u, input.Password); db.Users.Add(u);
    try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return Results.Conflict(new { message = "This email is already registered." }); }
    await SignIn(c, u); return Results.Ok(Profile(u));
});
app.MapPost("/api/auth/login", async (AuthInput input, ShopDb db, PasswordHasher<User> hasher, HttpContext c) =>
{
    var email = input.Email?.Trim().ToLowerInvariant();
    var u = await db.Users.SingleOrDefaultAsync(u => u.Email == email);
    if (u is null || input.Password is null || input.Password.Length > 128 || hasher.VerifyHashedPassword(u, u.PasswordHash, input.Password) == PasswordVerificationResult.Failed)
        return Results.Json(new { message = "Email or password is incorrect." }, statusCode: 401);
    await SignIn(c, u); return Results.Ok(Profile(u));
});
app.MapPost("/api/auth/logout", async (HttpContext c) => { await c.SignOutAsync(); return Results.NoContent(); });
app.MapGet("/api/auth/me", async (HttpContext c, ShopDb db) => { var u = await db.Users.FindAsync(int.Parse(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!)); return u is null ? Results.Unauthorized() : Results.Ok(Profile(u)); }).RequireAuthorization();
app.MapGet("/api/coffees", async (ShopDb db) => await db.Coffees.AsNoTracking().OrderBy(x => x.Id).ToListAsync());
string? ValidateCoffee(CoffeeInput x) => string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 100 || string.IsNullOrWhiteSpace(x.Description) || x.Description.Length > 1000 || x.Price <= 0 || x.Price > 9999 || decimal.Round(x.Price, 2) != x.Price || x.Stock < 0 || x.Stock > 100000 || x.Image?.Length > 2000 || !Uri.TryCreate(x.Image, UriKind.Absolute, out var uri) || uri.Scheme != "https" ? "Enter a name, description, HTTPS image URL, positive price (up to 2 decimals), and stock from 0 to 100000." : null;
app.MapPost("/api/coffees", async (CoffeeInput x, ShopDb db) =>
{
    if (ValidateCoffee(x) is string error) return Results.BadRequest(new { message = error });
    var coffee = new Coffee { Name = x.Name!.Trim(), Description = x.Description!.Trim(), Image = x.Image!, Price = x.Price, Stock = x.Stock };
    db.Coffees.Add(coffee); await db.SaveChangesAsync(); return Results.Ok(coffee);
}).RequireAuthorization(p => p.RequireRole("Sales"));
app.MapPut("/api/coffees/{id:int}", async (int id, CoffeeInput x, ShopDb db) =>
{
    if (ValidateCoffee(x) is string error) return Results.BadRequest(new { message = error });
    var coffee = await db.Coffees.FindAsync(id); if (coffee is null) return Results.NotFound();
    coffee.Name = x.Name!.Trim(); coffee.Description = x.Description!.Trim(); coffee.Image = x.Image!; coffee.Price = x.Price; coffee.Stock = x.Stock;
    await db.SaveChangesAsync(); return Results.Ok(coffee);
}).RequireAuthorization(p => p.RequireRole("Sales"));
app.MapPost("/api/orders", async (OrderInput input, ShopDb db, HttpContext c) =>
{
    if (input.Items is null || input.Items.Count is < 1 or > 50 || input.Items.Any(x => x.Quantity is < 1 or > 20) || input.Items.Select(x => x.CoffeeId).Distinct().Count() != input.Items.Count)
        return Results.BadRequest(new { message = "Choose coffees with quantities between 1 and 20; each coffee may appear once." });
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
    var order = new Order { UserId = int.Parse(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!) };
    foreach (var item in input.Items.OrderBy(x => x.CoffeeId))
    {
        // Conditional atomic decrement prevents overselling across simultaneous orders.
        var changed = await db.Coffees.Where(x => x.Id == item.CoffeeId && x.Stock >= item.Quantity).ExecuteUpdateAsync(s => s.SetProperty(x => x.Stock, x => x.Stock - item.Quantity));
        if (changed == 0) return Results.Conflict(new { message = "A coffee is no longer available in that quantity. Refresh the menu." });
        var coffee = await db.Coffees.AsNoTracking().SingleAsync(x => x.Id == item.CoffeeId);
        order.Items.Add(new OrderItem { CoffeeId = coffee.Id, Name = coffee.Name, Price = coffee.Price, Quantity = item.Quantity });
        order.Total += coffee.Price * item.Quantity;
    }
    db.Orders.Add(order); await db.SaveChangesAsync(); await transaction.CommitAsync();
    return Results.Ok(new { order.Id });
}).RequireAuthorization(p => p.RequireRole("Customer"));
app.MapGet("/api/orders", async (ShopDb db, HttpContext c) =>
{
    var uid = int.Parse(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var query = db.Orders.AsNoTracking().Where(o => c.User.IsInRole("Sales") || o.UserId == uid);
    return await query.OrderByDescending(o => o.CreatedAt).Select(o => new { o.Id, o.CreatedAt, o.Status, o.Total, Customer = o.User.Name, o.Items }).ToListAsync();
}).RequireAuthorization();
app.MapPut("/api/orders/{id:int}/status", async (int id, StatusInput input, ShopDb db) =>
{
    string[] states = ["Pending", "Confirmed", "Preparing", "Completed"];
    var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id);
    if (order is null) return Results.NotFound();
    if (Array.IndexOf(states, input.Status) != Array.IndexOf(states, order.Status) + 1 || order.Status == "Completed") return Results.BadRequest(new { message = "Orders must progress from Pending to Confirmed, Preparing, then Completed." });
    var updated = await db.Orders.Where(o => o.Id == id && o.Status == order.Status).ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, input.Status!));
    return updated == 1 ? Results.NoContent() : Results.Conflict(new { message = "This order changed. Refresh and try again." });
}).RequireAuthorization(p => p.RequireRole("Sales"));
app.Run();
