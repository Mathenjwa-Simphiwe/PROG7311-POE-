using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TechMoveAPI.Data;
using TechMoveAPI.Models;
using TechMoveAPI.Services;

namespace TechMoveAPI.Controllers
{
    /// <summary>Returns dashboard summary statistics.</summary>
    [ApiController]
    [Route("api/dashboard")]
    [Authorize]
    [Produces("application/json")]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _db;

        public DashboardController(AppDbContext db) => _db = db;

        private string UserId  => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        private bool   IsAdmin => User.IsInRole("Admin");

        /// <summary>Get dashboard statistics for the current user.</summary>
        [HttpGet]
        [ProducesResponseType(200)]
        public async Task<IActionResult> GetStats()
        {
            if (IsAdmin)
            {
                return Ok(new
                {
                    TotalContracts   = await _db.Contracts.CountAsync(),
                    ActiveContracts  = await _db.Contracts.CountAsync(c => c.Status == ContractStatus.Active),
                    PendingRequests  = await _db.ServiceRequests.CountAsync(r => r.Status == ServiceRequestStatus.Pending),
                    TotalClients     = await _db.Clients.CountAsync()
                });
            }

            return Ok(new
            {
                TotalContracts  = await _db.Contracts.CountAsync(c => c.ClientId == UserId),
                ActiveContracts = await _db.Contracts.CountAsync(c => c.ClientId == UserId && c.Status == ContractStatus.Active),
                PendingRequests = await _db.ServiceRequests.CountAsync(r => r.Contract!.ClientId == UserId && r.Status == ServiceRequestStatus.Pending)
            });
        }
    }

    /// <summary>Client management (Admin only).</summary>
    [ApiController]
    [Route("api/clients")]
    [Authorize(Roles = "Admin")]
    [Produces("application/json")]
    public class ClientsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ClientsController(AppDbContext db) => _db = db;

        /// <summary>Get all registered clients.</summary>
        [HttpGet]
        [ProducesResponseType(200)]
        public async Task<IActionResult> GetClients()
        {
            var clients = await _db.Clients
                .Select(c => new { c.Id, c.FullName, c.Email, c.Region })
                .ToListAsync();
            return Ok(clients);
        }
    }

    /// <summary>Currency utilities.</summary>
    [ApiController]
    [Route("api/currency")]
    [Authorize]
    [Produces("application/json")]
    public class CurrencyController : ControllerBase
    {
        private readonly CurrencyConverter  _converter;
        private readonly ExchangeRateService _fx;

        public CurrencyController(CurrencyConverter converter, ExchangeRateService fx)
        {
            _converter = converter;
            _fx        = fx;
        }

        /// <summary>Get a static exchange rate between two currencies.</summary>
        [HttpGet("rate")]
        [ProducesResponseType(200)]
        public IActionResult GetRate([FromQuery] string from = "USD", [FromQuery] string to = "ZAR")
        {
            return Ok(new
            {
                From      = from,
                To        = to,
                Rate      = _converter.GetExchangeRate(from, to),
                Timestamp = DateTime.Now
            });
        }

        /// <summary>Get the live USD → ZAR exchange rate from open.er-api.com.</summary>
        [HttpGet("live-usd-zar")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> GetLiveRate()
        {
            var rate = await _fx.GetUsdToZarRateAsync();
            return Ok(new { Rate = rate, Pair = "USD/ZAR", Timestamp = DateTime.UtcNow });
        }
    }
}
