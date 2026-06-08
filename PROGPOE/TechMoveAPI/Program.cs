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
    builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(
        connectionString,
        sql => sql.EnableRetryOnFailure(10, TimeSpan.FromSeconds(10), null)));
    Console.WriteLine("[TechMoveAPI] USING SQL SERVER DATABASE");
}

// ─── IDENTITY ──────────────────────────────────────────────────────────────
// Single Identity registration using Client as the base user type.
// Admins are seeded as Client users with the "Admin" role assigned.
// This avoids the AddIdentityCore<Admin> conflict that breaks AddToRoleAsync.
builder.Services.AddIdentity<Client, IdentityRole>(o =>
{
    o.Password.RequireDigit = true;
    o.Password.RequiredLength = 6;
    o.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ─── JWT AUTHENTICATION ────────────────────────────────────────────────────
var jwtKey = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);
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
        IssuerSigningKey = new SymmetricSecurityKey(jwtKey)
    };
});

// ─── CORS (allows PROGPOE MVC frontend to call this API) ──────────────────
builder.Services.AddCors(o => o.AddPolicy("MvcFrontend", p =>
    p.AllowAnyOrigin()
     .AllowAnyHeader()
     .AllowAnyMethod()));

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
        Title = "TechMove GLMS API",
        Version = "v1",
        Description = "Global Logistics Management System REST API.\n\n" +
                      "**How to authenticate:**\n" +
                      "1. POST `/api/auth/login` with `{\"email\":\"admin@glms.com\",\"password\":\"Admin@123\"}`\n" +
                      "2. Copy the `token` from the response.\n" +
                      "3. Click **Authorize** and enter: `Bearer {token}`"
    });

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

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TechMove GLMS API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "TechMove API";
});

app.UseHttpsRedirection();
app.UseCors("MvcFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ─── SEED DATABASE ─────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rm = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<Client>>();

        await db.Database.EnsureCreatedAsync();

        // 1. Create roles
        foreach (var role in new[] { "Admin", "Client" })
            if (!await rm.RoleExistsAsync(role))
                await rm.CreateAsync(new IdentityRole(role));

        // 2. Seed or repair admin user (stored as a Client user with "Admin" role)
        var admin = await um.FindByEmailAsync("admin@glms.com");
        if (admin == null)
        {
            admin = new Client
            {
                UserName = "admin@glms.com",
                Email = "admin@glms.com",
                FullName = "System Admin",
                Region = "Admin"
            };
            var result = await um.CreateAsync(admin, "Admin@123");
            if (!result.Succeeded)
                Console.WriteLine("[SEED] Admin create failed: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }
        else
        {
            admin.FullName = "System Admin";
            admin.Region = "Admin";
            admin.EmailConfirmed = true;
            await um.UpdateAsync(admin);

            if (!await um.CheckPasswordAsync(admin, "Admin@123"))
            {
                if (await um.HasPasswordAsync(admin))
                    await um.RemovePasswordAsync(admin);

                var passwordResult = await um.AddPasswordAsync(admin, "Admin@123");
                if (!passwordResult.Succeeded)
                    Console.WriteLine("[SEED] Admin password reset failed: " + string.Join(", ", passwordResult.Errors.Select(e => e.Description)));
            }
        }

        if (admin != null && !await um.IsInRoleAsync(admin, "Admin"))
            await um.AddToRoleAsync(admin, "Admin");

        // 3. Seed test client
        if (await um.FindByEmailAsync("client@test.com") == null)
        {
            var client = new Client
            {
                UserName = "client@test.com",
                Email = "client@test.com",
                FullName = "Test Client",
                Region = "South Africa"
            };
            var result = await um.CreateAsync(client, "Client@123");
            if (result.Succeeded)
                await um.AddToRoleAsync(client, "Client");
            else
                Console.WriteLine("[SEED] Client create failed: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        Console.WriteLine("[SEED] Database seeded successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[SEED ERROR] {ex.Message}");
    }
}

app.Run();

public partial class Program { }
