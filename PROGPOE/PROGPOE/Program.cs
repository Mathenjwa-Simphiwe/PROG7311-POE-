using PROGPOE.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── SESSION (stores JWT token received from TechMoveAPI) ──────────────────
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.IdleTimeout        = TimeSpan.FromHours(3);
    o.Cookie.HttpOnly    = true;
    o.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();

// ─── HTTP CLIENT → TECHMOVEAPI ─────────────────────────────────────────────
// Set the BaseAddress to wherever TechMoveAPI is running.
// In development both projects share the same solution; TechMoveAPI typically
// runs on https://localhost:7001 (check its launchSettings.json).
builder.Services.AddHttpClient<TechMoveApiService>(client =>
{
    client.BaseAddress = new Uri("https://localhost:7254/");
});

// ─── MVC ──────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ─── BUILD ────────────────────────────────────────────────────────────────
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();         // must come before MapControllerRoute
app.UseAuthorization();

app.MapControllerRoute(
    name:    "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
