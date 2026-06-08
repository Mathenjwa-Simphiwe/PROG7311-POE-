using PROGPOE.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── SESSION ──────────────────────────────────────────────────────────────
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromHours(3);
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.None; // allow HTTP in Docker
});
builder.Services.AddHttpContextAccessor();

// ─── HTTP CLIENT → TECH MOVE API ─────────────────────────────────────────
// Read base URL from configuration (environment variable TechMoveApi__BaseUrl)
var apiBaseUrl = builder.Configuration["TechMoveApi:BaseUrl"]
    ?? throw new InvalidOperationException("TechMoveApi:BaseUrl is not configured.");
builder.Services.AddHttpClient<TechMoveApiService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

// ─── MVC ─────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
