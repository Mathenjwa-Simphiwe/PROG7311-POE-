using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;


// using Microsoft.OpenApi; -- removed: not needed, types are in Microsoft.OpenApi.Models
using Microsoft.OpenApi.Models;
using PROGPOE.Data;
using PROGPOE.Models;
using PROGPOE.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ─── DATABASE ──────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var useInMemory = builder.Configuration.GetValue<bool>("UseInMemoryDatabase")
                       || string.IsNullOrEmpty(connectionString);

if (useInMemory)
{
    builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("GLMS_DB"));
    Console.WriteLine("USING IN-MEMORY DATABASE");
}
else
{
    builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));
    Console.WriteLine("USING SQL SERVER DATABASE");
}

// ─── IDENTITY ──────────────────────────────────────────────────────────────
builder.Services.AddIdentity<Client, IdentityRole>(o =>
{
    o.Password.RequireDigit = true;
    o.Password.RequiredLength = 6;
    o.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddIdentityCore<Admin>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ─── JWT AUTHENTICATION ────────────────────────────────────────────────────
var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);
builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

// ─── SERVICES ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<CurrencyConverter>();
builder.Services.AddHttpClient<ExchangeRateService>();
builder.Services.AddScoped<Billing>();
builder.Services.AddScoped<EmailObserver>();
builder.Services.AddScoped<FileStorageService>();
builder.Services.AddScoped<ContractService>();

// ─── MVC + API + SWAGGER ───────────────────────────────────────────────────
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TechMove GLMS API",
        Version = "v1",
        Description = "Global Logistics Management System REST API. " +
                      "Authenticate via POST /api/account/login to obtain a Bearer token, " +
                      "then click 'Authorize' and enter: Bearer {token}"
    });

    // Enable JWT auth in Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token. Example: Bearer eyJhbGci..."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ─── BUILD ─────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TechMove GLMS API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "TechMove API";
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ─── SEED DATABASE ─────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var rm = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var am = scope.ServiceProvider.GetRequiredService<UserManager<Admin>>();
    var cm = scope.ServiceProvider.GetRequiredService<UserManager<Client>>();

    await db.Database.EnsureCreatedAsync();

    foreach (var role in new[] { "Admin", "Client" })
        if (!await rm.RoleExistsAsync(role))
            await rm.CreateAsync(new IdentityRole(role));

    if (await am.FindByEmailAsync("admin@glms.com") == null)
    {
        var admin = new Admin { UserName = "admin@glms.com", Email = "admin@glms.com", FullName = "System Admin", Department = "IT" };
        await am.CreateAsync(admin, "Admin@123");
        await am.AddToRoleAsync(admin, "Admin");
    }

    if (await cm.FindByEmailAsync("client@test.com") == null)
    {
        var client = new Client { UserName = "client@test.com", Email = "client@test.com", FullName = "Test Client", Region = "North America" };
        await cm.CreateAsync(client, "Client@123");
        await cm.AddToRoleAsync(client, "Client");
    }
}

app.Run();

public partial class Program { }