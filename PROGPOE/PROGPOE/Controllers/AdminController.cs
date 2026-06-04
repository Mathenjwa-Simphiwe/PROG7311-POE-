using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROGPOE.Data;
using PROGPOE.Models;
using PROGPOE.Services;

namespace PROGPOE.Controllers
{
    // ─────────────────────────────────────────────────────────────────
    //  API CONTROLLER  – all routes under /api/admin
    //  Requires a valid JWT with Role = "Admin"
    // ─────────────────────────────────────────────────────────────────
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class ApiAdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ContractService _cs;
        private readonly FileStorageService _fs;

        public ApiAdminController(AppDbContext db, ContractService cs, FileStorageService fs)
        {
            _db = db;
            _cs = cs;
            _fs = fs;
        }

        // ── DASHBOARD ─────────────────────────────────────────────────
        /// <summary>Returns admin dashboard summary statistics.</summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard() => Ok(new
        {
            TotalContracts   = await _db.Contracts.CountAsync(),
            ActiveContracts  = await _db.Contracts.CountAsync(c => c.Status == ContractStatus.Active),
            PendingRequests  = await _db.ServiceRequests.CountAsync(r => r.Status == ServiceRequestStatus.Pending),
            TotalClients     = await _db.Clients.CountAsync()
        });

        // ── CONTRACTS ─────────────────────────────────────────────────

        /// <summary>GET all contracts with optional filters (startDate, endDate, status).</summary>
        [HttpGet("contracts")]
        public async Task<IActionResult> GetContracts(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] ContractStatus? status)
        {
            var contracts = await _cs.SearchAsync(startDate, endDate, status);
            return Ok(contracts.Select(c => new
            {
                c.ContractId,
                c.ContractNumber,
                c.StartDate,
                c.EndDate,
                Status       = c.Status.ToString(),
                ServiceLevel = c.ServiceLevel.ToString(),
                ClientName   = c.Client?.FullName,
                ClientId     = c.ClientId,
                HasAgreement = !string.IsNullOrEmpty(c.SignedAgreementPath)
            }));
        }

        /// <summary>GET a single contract by ID.</summary>
        [HttpGet("contracts/{id}")]
        public async Task<IActionResult> GetContract(int id)
        {
            var c = await _db.Contracts.Include(x => x.Client).FirstOrDefaultAsync(x => x.ContractId == id);
            if (c == null) return NotFound(new { Message = "Contract not found" });
            return Ok(new
            {
                c.ContractId,
                c.ContractNumber,
                c.StartDate,
                c.EndDate,
                Status       = c.Status.ToString(),
                ServiceLevel = c.ServiceLevel.ToString(),
                ClientName   = c.Client?.FullName,
                ClientId     = c.ClientId,
                HasAgreement = !string.IsNullOrEmpty(c.SignedAgreementPath)
            });
        }

        /// <summary>POST – create a new contract (multipart/form-data with optional PDF upload).</summary>
        [HttpPost("contracts")]
        public async Task<IActionResult> CreateContract([FromForm] CreateContractRequest request)
        {
            if (!DateTime.TryParse(request.StartDate, out var startDate))
                return BadRequest(new { Message = "Invalid start date format." });
            if (!DateTime.TryParse(request.EndDate, out var endDate))
                return BadRequest(new { Message = "Invalid end date format." });
            if (endDate <= startDate)
                return BadRequest(new { Message = "End date must be after start date." });

            var contract = new Contract
            {
                ClientId     = request.ClientId,
                StartDate    = startDate,
                EndDate      = endDate,
                ServiceLevel = (ServiceLevel)request.ServiceLevel
            };

            var created = await _cs.CreateAsync(contract);

            if (request.Agreement != null)
            {
                created.SignedAgreementPath = await _fs.SaveFileAsync(created.ContractId, request.Agreement);
                await _db.SaveChangesAsync();
            }

            return CreatedAtAction(nameof(GetContract), new { id = created.ContractId }, new
            {
                created.ContractId,
                created.ContractNumber,
                Message = "Contract created successfully"
            });
        }

        /// <summary>PUT – fully update an existing contract's dates and service level.</summary>
        [HttpPut("contracts/{id}")]
        public async Task<IActionResult> UpdateContract(int id, [FromBody] UpdateContractRequest request)
        {
            var contract = await _db.Contracts.FindAsync(id);
            if (contract == null) return NotFound(new { Message = "Contract not found" });

            if (!DateTime.TryParse(request.StartDate, out var startDate))
                return BadRequest(new { Message = "Invalid start date." });
            if (!DateTime.TryParse(request.EndDate, out var endDate))
                return BadRequest(new { Message = "Invalid end date." });
            if (endDate <= startDate)
                return BadRequest(new { Message = "End date must be after start date." });

            contract.StartDate    = startDate;
            contract.EndDate      = endDate;
            contract.ServiceLevel = (ServiceLevel)request.ServiceLevel;
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Contract updated successfully" });
        }

        /// <summary>PATCH – update only the contract status (Approve / Decline / etc.).</summary>
        [HttpPatch("contracts/{id}/status")]
        public async Task<IActionResult> PatchContractStatus(int id, [FromBody] ChangeStatusRequest request)
        {
            var contract = await _db.Contracts.FindAsync(id);
            if (contract == null) return NotFound(new { Message = "Contract not found" });

            contract.ChangeStatus(request.Status);
            await _db.SaveChangesAsync();

            return Ok(new { Message = $"Contract status updated to {request.Status}" });
        }

        /// <summary>DELETE – remove a contract by ID.</summary>
        [HttpDelete("contracts/{id}")]
        public async Task<IActionResult> DeleteContract(int id)
        {
            var contract = await _db.Contracts.FindAsync(id);
            if (contract == null) return NotFound(new { Message = "Contract not found" });

            _db.Contracts.Remove(contract);
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Contract deleted successfully" });
        }

        // ── SERVICE REQUESTS ─────────────────────────────────────────

        /// <summary>GET all service requests with optional status filter.</summary>
        [HttpGet("service-requests")]
        public async Task<IActionResult> GetServiceRequests([FromQuery] ServiceRequestStatus? status)
        {
            var query = _db.ServiceRequests
                .Include(r => r.Contract).ThenInclude(c => c!.Client)
                .OrderByDescending(r => r.RequestDate)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);

            var requests = await query.ToListAsync();

            return Ok(requests.Select(r => new
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
                ContractNumber = r.Contract?.ContractNumber,
                ClientName     = r.Contract?.Client?.FullName,
                r.AdminNotes,
                r.RequestDate,
                r.DecisionDate
            }));
        }

        /// <summary>GET a single service request by ID.</summary>
        [HttpGet("service-requests/{id}")]
        public async Task<IActionResult> GetServiceRequest(int id)
        {
            var r = await _db.ServiceRequests
                .Include(x => x.Contract).ThenInclude(c => c!.Client)
                .FirstOrDefaultAsync(x => x.ServiceRequestId == id);
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
                ContractNumber = r.Contract?.ContractNumber,
                ClientName     = r.Contract?.Client?.FullName,
                r.AdminNotes,
                r.RequestDate,
                r.DecisionDate
            });
        }

        /// <summary>PATCH – approve a service request (sets status to Approved).</summary>
        [HttpPatch("service-requests/{id}/approve")]
        public async Task<IActionResult> ApproveRequest(int id, [FromBody] DecisionRequest request)
        {
            var sr = await _db.ServiceRequests.FindAsync(id);
            if (sr == null) return NotFound(new { Message = "Request not found" });

            sr.Status       = ServiceRequestStatus.Approved;
            sr.AdminNotes   = request.Notes;
            sr.DecisionDate = DateTime.Now;
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Request approved" });
        }

        /// <summary>PATCH – decline a service request (sets status to Denied).</summary>
        [HttpPatch("service-requests/{id}/decline")]
        public async Task<IActionResult> DeclineRequest(int id, [FromBody] DecisionRequest request)
        {
            var sr = await _db.ServiceRequests.FindAsync(id);
            if (sr == null) return NotFound(new { Message = "Request not found" });

            sr.Status       = ServiceRequestStatus.Denied;
            sr.AdminNotes   = request.Notes;
            sr.DecisionDate = DateTime.Now;
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Request declined" });
        }

        /// <summary>DELETE – remove a service request by ID.</summary>
        [HttpDelete("service-requests/{id}")]
        public async Task<IActionResult> DeleteServiceRequest(int id)
        {
            var sr = await _db.ServiceRequests.FindAsync(id);
            if (sr == null) return NotFound(new { Message = "Request not found" });

            _db.ServiceRequests.Remove(sr);
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Service request deleted" });
        }

        // ── AGREEMENT DOWNLOAD ───────────────────────────────────────

        /// <summary>GET – download the signed PDF agreement for a contract.</summary>
        [HttpGet("contracts/{id}/download")]
        public async Task<IActionResult> DownloadAgreement(int id)
        {
            var contract = await _db.Contracts.FindAsync(id);
            if (contract?.SignedAgreementPath == null)
                return NotFound(new { Message = "Agreement not found" });

            var stream = _fs.GetFile(contract.SignedAgreementPath);
            if (stream == null) return NotFound(new { Message = "File not found on server" });

            return File(stream, "application/pdf", $"Contract_{contract.ContractNumber}.pdf");
        }

        // ── CLIENTS ─────────────────────────────────────────────────

        /// <summary>GET all registered clients.</summary>
        [HttpGet("clients")]
        public async Task<IActionResult> GetClients()
        {
            var clients = await _db.Clients
                .Select(c => new { c.Id, c.FullName, c.Email, c.Region })
                .ToListAsync();
            return Ok(clients);
        }

        // ── CURRENCY ─────────────────────────────────────────────────

        /// <summary>GET exchange rate between two currency codes.</summary>
        [HttpGet("currency/rate")]
        public IActionResult GetExchangeRate([FromQuery] string from = "USD", [FromQuery] string to = "EUR")
        {
            var converter = new CurrencyConverter();
            return Ok(new
            {
                From      = from,
                To        = to,
                Rate      = converter.GetExchangeRate(from, to),
                Timestamp = DateTime.Now
            });
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  MVC CONTROLLER – serves Razor views for the Admin UI area
    // ─────────────────────────────────────────────────────────────────
    public class AdminController : Controller
    {
        public IActionResult Dashboard()      => View();
        public IActionResult Contracts()      => View();
        public IActionResult ServiceRequests() => View();
    }

    // ── DTOs ──────────────────────────────────────────────────────────
    public class CreateContractRequest
    {
        public string ClientId    { get; set; } = string.Empty;
        public string StartDate   { get; set; } = string.Empty;
        public string EndDate     { get; set; } = string.Empty;
        public int    ServiceLevel { get; set; }
        public IFormFile? Agreement { get; set; }
    }

    public class UpdateContractRequest
    {
        public string StartDate    { get; set; } = string.Empty;
        public string EndDate      { get; set; } = string.Empty;
        public int    ServiceLevel  { get; set; }
    }

    public class ChangeStatusRequest
    {
        public ContractStatus Status { get; set; }
    }

    public class DecisionRequest
    {
        public string? Notes { get; set; }
    }
}
