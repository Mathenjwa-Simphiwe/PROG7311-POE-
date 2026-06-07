using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using TechMoveAPI.Data;
using TechMoveAPI.Models;
using TechMoveAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── DATABASE ──────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var useInMemory = builder.Configuration.GetValue<bool>("UseInMemoryDatabase")
                       || string.IsNullOrEmpty(connectionString);

if (useInMemory)
{
    builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("GLMS_API_DB"));
    Console.WriteLine("[TechMoveAPI] USING IN-MEMORY DATABASE");
}
else
{
    builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));
    Console.WriteLine("[TechMoveAPI] USING SQL SERVER DATABASE");
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
var jwtKey = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);
builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer              = builder.Configuration["Jwt:Issuer"],
        ValidAudience            = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey         = new SymmetricSecurityKey(jwtKey)
    };
});

// ─── CORS (allows PROGPOE MVC frontend to call this API) ──────────────────
builder.Services.AddCors(o => o.AddPolicy("MvcFrontend", p =>
    p.WithOrigins("https://localhost:7000", "http://localhost:5000", "https://localhost:7244")
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

// ─── SERVICES ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<CurrencyConverter>();
builder.Services.AddHttpClient<ExchangeRateService>();
builder.Services.AddScoped<Billing>();
builder.Services.AddScoped<EmailObserver>();
builder.Services.AddScoped<FileStorageService>();
builder.Services.AddScoped<ContractService>();

// ─── CONTROLLERS + SWAGGER ────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "TechMove GLMS API",
        Version     = "v1",
        Description = "Global Logistics Management System REST API.\n\n" +
                      "**How to authenticate:**\n" +
                      "1. POST `/api/auth/login` with `{\"email\":\"admin@glms.com\",\"password\":\"Admin@123\"}`\n" +
                      "2. Copy the `token` from the response.\n" +
                      "3. Click **Authorize** and enter: `Bearer {token}`"
    });

    // JWT Bearer button in Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name        = "Authorization",
        Type        = SecuritySchemeType.Http,
        Scheme      = "Bearer",
        BearerFormat = "JWT",
        In          = ParameterLocation.Header,
        Description = "Enter your JWT token. Example: Bearer eyJhbGci..."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ─── BUILD ─────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TechMove GLMS API v1");
    c.RoutePrefix    = "swagger";
    c.DocumentTitle  = "TechMove API";
});

app.UseHttpsRedirection();
app.UseCors("MvcFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

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
        var admin = new Admin
        {
            UserName   = "admin@glms.com",
            Email      = "admin@glms.com",
            FullName   = "System Admin",
            Department = "IT"
        };
        await am.CreateAsync(admin, "Admin@123");
        await am.AddToRoleAsync(admin, "Admin");
    }

    if (await cm.FindByEmailAsync("client@test.com") == null)
    {
        var client = new Client
        {
            UserName = "client@test.com",
            Email    = "client@test.com",
            FullName = "Test Client",
            Region   = "North America"
        };
        await cm.CreateAsync(client, "Client@123");
        await cm.AddToRoleAsync(client, "Client");
    }
}

app.Run();
