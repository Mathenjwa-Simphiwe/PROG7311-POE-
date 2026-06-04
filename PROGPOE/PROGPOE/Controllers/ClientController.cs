using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROGPOE.Data;
using PROGPOE.Factory;
using PROGPOE.Models;
using PROGPOE.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace PROGPOE.Controllers
{
    // ─────────────────────────────────────────────────────────────────
    //  API CONTROLLER – all routes under /api/client
    //  Requires a valid JWT with Role = "Client"
    // ─────────────────────────────────────────────────────────────────
    [ApiController, Route("api/client"), Authorize(Roles = "Client")]
    public class ApiClientController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ExchangeRateService _fx;

        public ApiClientController(AppDbContext db, ExchangeRateService fx)
        {
            _db = db;
            _fx = fx;
        }

        private string Id => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

        // ── DASHBOARD ─────────────────────────────────────────────────

        /// <summary>Returns the client's dashboard summary statistics.</summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard() => Ok(new
        {
            totalContracts   = await _db.Contracts.CountAsync(c => c.ClientId == Id),
            activeContracts  = await _db.Contracts.CountAsync(c => c.ClientId == Id && c.Status == ContractStatus.Active),
            pendingRequests  = await _db.ServiceRequests.CountAsync(r => r.Contract!.ClientId == Id && r.Status == ServiceRequestStatus.Pending)
        });

        // ── CONTRACTS ─────────────────────────────────────────────────

        /// <summary>GET all contracts belonging to the authenticated client. Optional ?status= filter.</summary>
        [HttpGet("contracts")]
        public async Task<IActionResult> GetContracts([FromQuery] ContractStatus? status)
        {
            var query = _db.Contracts.Where(c => c.ClientId == Id);
            if (status.HasValue) query = query.Where(c => c.Status == status.Value);

            return Ok(await query.Select(c => new
            {
                c.ContractId,
                c.ContractNumber,
                c.StartDate,
                c.EndDate,
                Status       = c.Status.ToString(),
                ServiceLevel = c.ServiceLevel.ToString()
            }).ToListAsync());
        }

        /// <summary>GET a single contract by ID (must belong to this client).</summary>
        [HttpGet("contracts/{id}")]
        public async Task<IActionResult> GetContract(int id)
        {
            var c = await _db.Contracts.FirstOrDefaultAsync(c => c.ContractId == id && c.ClientId == Id);
            if (c == null) return NotFound(new { Message = "Contract not found" });
            return Ok(new
            {
                c.ContractId,
                c.ContractNumber,
                c.StartDate,
                c.EndDate,
                Status       = c.Status.ToString(),
                ServiceLevel = c.ServiceLevel.ToString()
            });
        }

        /// <summary>PATCH – client signs (activates) their contract.</summary>
        [HttpPatch("contracts/{id}/sign")]
        public async Task<IActionResult> SignContract(int id)
        {
            var c = await _db.Contracts.FirstOrDefaultAsync(c => c.ContractId == id && c.ClientId == Id);
            if (c == null) return NotFound(new { Message = "Contract not found" });
            c.ChangeStatus(ContractStatus.Active);
            await _db.SaveChangesAsync();
            return Ok(new { Message = "Contract signed and activated" });
        }

        // ── SERVICE REQUESTS ─────────────────────────────────────────

        /// <summary>GET all service requests for the authenticated client. Optional ?status= filter.</summary>
        [HttpGet("service-requests")]
        public async Task<IActionResult> GetRequests([FromQuery] ServiceRequestStatus? status)
        {
            var query = _db.ServiceRequests.Where(r => r.Contract!.ClientId == Id);
            if (status.HasValue) query = query.Where(r => r.Status == status.Value);

            return Ok(await query
                .Select(r => new
                {
                    r.ServiceRequestId,
                    r.RequestId,
                    Type           = r.RequestType.ToString(),
                    r.Origin,
                    r.Description,
                    r.Cost,
                    r.UsdAmount,
                    r.LocalCostZar,
                    r.ExchangeRateUsed,
                    Status         = r.Status.ToString(),
                    ContractNumber = r.Contract!.ContractNumber,
                    r.AdminNotes,
                    r.RequestDate
                })
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync());
        }

        /// <summary>GET a single service request by ID (must belong to this client).</summary>
        [HttpGet("service-requests/{id}")]
        public async Task<IActionResult> GetRequest(int id)
        {
            var r = await _db.ServiceRequests
                .Include(x => x.Contract)
                .FirstOrDefaultAsync(x => x.ServiceRequestId == id && x.Contract!.ClientId == Id);
            if (r == null) return NotFound(new { Message = "Request not found" });

            return Ok(new
            {
                r.ServiceRequestId,
                r.RequestId,
                Type           = r.RequestType.ToString(),
                r.Origin,
                r.Description,
                r.Cost,
                r.UsdAmount,
                r.LocalCostZar,
                r.ExchangeRateUsed,
                Status         = r.Status.ToString(),
                ContractNumber = r.Contract!.ContractNumber,
                r.AdminNotes,
                r.RequestDate
            });
        }

        /// <summary>POST – create a new FreightRequest or MaintenanceRequest via Factory Pattern.</summary>
        [HttpPost("service-requests")]
        public async Task<IActionResult> CreateRequest([FromBody] ReqDto d)
        {
            var contract = await _db.Contracts
                .FirstOrDefaultAsync(c => c.ContractId == d.ContractId && c.ClientId == Id);

            if (contract == null)
                return NotFound(new { Message = "Contract not found or does not belong to you." });

            if (contract.Status == ContractStatus.Expired || contract.Status == ContractStatus.OnHold)
                return BadRequest(new { Message = "Cannot create request for an expired or on-hold contract." });

            var data = new Dictionary<string, object>
            {
                ["Origin"]      = d.Origin.Trim(),
                ["Description"] = d.Desc.Trim()
            };

            ServiceRequest request;

            if (d.Type == 0) // Freight
            {
                if (string.IsNullOrWhiteSpace(d.Dest) || d.Weight == null)
                    return BadRequest(new { Message = "Freight requests require Destination and Weight." });

                data["Destination"] = d.Dest.Trim();
                data["WeightKg"]    = d.Weight.Value;
                request = new FreightRequestFactory().Create(data);
            }
            else // Maintenance
            {
                if (string.IsNullOrWhiteSpace(d.Equip) || d.Hours == null)
                    return BadRequest(new { Message = "Maintenance requests require EquipmentType and EstimatedHours." });

                data["EquipmentType"]   = d.Equip.Trim();
                data["EstimatedHours"]  = d.Hours.Value;
                request = new Maintenance().Create(data);
            }

            if (d.UsdAmount.HasValue && d.UsdAmount.Value > 0)
            {
                var (zarAmount, rateUsed) = await _fx.ConvertUsdToZarAsync(d.UsdAmount.Value);
                request.UsdAmount       = d.UsdAmount.Value;
                request.LocalCostZar    = zarAmount;
                request.ExchangeRateUsed = rateUsed;
            }

            request.ContractId = d.ContractId;
            _db.ServiceRequests.Add(request);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetRequest), new { id = request.ServiceRequestId }, new
            {
                request.RequestId,
                request.Cost,
                request.UsdAmount,
                request.LocalCostZar,
                request.ExchangeRateUsed,
                Message = "Service request created successfully."
            });
        }

        /// <summary>PUT – update origin and description of a pending service request.</summary>
        [HttpPut("service-requests/{id}")]
        public async Task<IActionResult> UpdateRequest(int id, [FromBody] UpdateReqDto d)
        {
            var r = await _db.ServiceRequests
                .Include(x => x.Contract)
                .FirstOrDefaultAsync(x => x.ServiceRequestId == id && x.Contract!.ClientId == Id);

            if (r == null) return NotFound(new { Message = "Request not found" });
            if (r.Status != ServiceRequestStatus.Pending)
                return BadRequest(new { Message = "Only pending requests can be edited." });

            r.Origin      = d.Origin.Trim();
            r.Description = d.Description.Trim();
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Request updated successfully" });
        }

        /// <summary>DELETE – cancel (delete) a pending service request.</summary>
        [HttpDelete("service-requests/{id}")]
        public async Task<IActionResult> DeleteRequest(int id)
        {
            var r = await _db.ServiceRequests
                .Include(x => x.Contract)
                .FirstOrDefaultAsync(x => x.ServiceRequestId == id && x.Contract!.ClientId == Id);

            if (r == null) return NotFound(new { Message = "Request not found" });
            if (r.Status != ServiceRequestStatus.Pending)
                return BadRequest(new { Message = "Only pending requests can be cancelled." });

            _db.ServiceRequests.Remove(r);
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Request cancelled and deleted" });
        }

        // ── CURRENCY ─────────────────────────────────────────────────

        /// <summary>GET live USD→ZAR exchange rate.</summary>
        [HttpGet("currency/rate")]
        public async Task<IActionResult> GetRate()
        {
            var rate = await _fx.GetUsdToZarRateAsync();
            return Ok(new { rate, timestamp = DateTime.UtcNow });
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  MVC CONTROLLER – serves Razor views for the Client UI area
    // ─────────────────────────────────────────────────────────────────
    public class ClientController : Controller
    {
        public IActionResult Dashboard()       => View();
        public IActionResult ClientContracts() => View();
        public IActionResult Requests()        => View();
    }

    // ── DTOs ──────────────────────────────────────────────────────────
    public class ReqDto
    {
        [Required] public int    ContractId { get; set; }
        [Required] public int    Type       { get; set; }
        [Required] public string Origin     { get; set; } = "";
        [Required] public string Desc       { get; set; } = "";
        public string?  Dest     { get; set; }
        public decimal? Weight   { get; set; }
        public string?  Equip    { get; set; }
        public int?     Hours    { get; set; }
        public decimal? UsdAmount { get; set; }
    }

    public class UpdateReqDto
    {
        [Required] public string Origin      { get; set; } = "";
        [Required] public string Description { get; set; } = "";
    }
}
