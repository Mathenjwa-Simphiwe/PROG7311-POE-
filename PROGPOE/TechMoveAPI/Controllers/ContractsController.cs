using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using TechMoveAPI.Data;
using TechMoveAPI.Models;
using TechMoveAPI.Services;

namespace TechMoveAPI.Controllers
{
    /// <summary>
    /// Manages contracts in the Global Logistics Management System.
    /// Admins can see and manage all contracts; Clients see only their own.
    /// </summary>
    [ApiController]
    [Route("api/contracts")]
    [Authorize]
    [Produces("application/json")]
    public class ContractsController : ControllerBase
    {
        private readonly AppDbContext    _db;
        private readonly ContractService _cs;
        private readonly FileStorageService _fs;

        public ContractsController(AppDbContext db, ContractService cs, FileStorageService fs)
        {
            _db = db;
            _cs = cs;
            _fs = fs;
        }

        // ── Helpers ─────────────────────────────────────────────────────────
        private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        private bool   IsAdmin => User.IsInRole("Admin");

        // ── GET /api/contracts ───────────────────────────────────────────────
        /// <summary>
        /// Get all contracts with optional filters.
        /// Admins see all; Clients see only their own.
        /// </summary>
        /// <param name="startDate">Filter: contracts starting on or after this date.</param>
        /// <param name="endDate">Filter: contracts ending on or before this date.</param>
        /// <param name="status">Filter by status (Draft=0, Active=1, Expired=2, OnHold=3, Terminated=4).</param>
        [HttpGet]
        [ProducesResponseType(200)]
        public async Task<IActionResult> GetContracts(
            [FromQuery] DateTime?       startDate,
            [FromQuery] DateTime?       endDate,
            [FromQuery] ContractStatus? status)
        {
            var clientId = IsAdmin ? null : UserId;
            var contracts = await _cs.SearchAsync(startDate, endDate, status, clientId);

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

        // ── GET /api/contracts/{id} ──────────────────────────────────────────
        /// <summary>Get a single contract by ID.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetContract(int id)
        {
            var query = _db.Contracts.Include(c => c.Client).AsQueryable();

            // Clients may only see their own contract
            if (!IsAdmin) query = query.Where(c => c.ClientId == UserId);

            var c = await query.FirstOrDefaultAsync(c => c.ContractId == id);
            if (c == null) return NotFound(new { Message = "Contract not found." });

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

        // ── POST /api/contracts ──────────────────────────────────────────────
        /// <summary>
        /// Create a new contract (Admin only). Optionally attach a signed PDF agreement.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> CreateContract([FromForm] CreateContractDto dto)
        {
            if (!DateTime.TryParse(dto.StartDate, out var startDate))
                return BadRequest(new { Message = "Invalid start date format." });

            if (!DateTime.TryParse(dto.EndDate, out var endDate))
                return BadRequest(new { Message = "Invalid end date format." });

            if (endDate <= startDate)
                return BadRequest(new { Message = "End date must be after start date." });

            var contract = new Contract
            {
                ClientId     = dto.ClientId,
                StartDate    = startDate,
                EndDate      = endDate,
                ServiceLevel = (ServiceLevel)dto.ServiceLevel
            };

            var created = await _cs.CreateAsync(contract);

            if (dto.Agreement != null)
            {
                created.SignedAgreementPath = await _fs.SaveFileAsync(created.ContractId, dto.Agreement);
                await _db.SaveChangesAsync();
            }

            return CreatedAtAction(nameof(GetContract), new { id = created.ContractId }, new
            {
                created.ContractId,
                created.ContractNumber,
                Message = "Contract created successfully."
            });
        }

        // ── PUT /api/contracts/{id} ──────────────────────────────────────────
        /// <summary>Update a contract's dates and service level (Admin only).</summary>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateContract(int id, [FromBody] UpdateContractDto dto)
        {
            var contract = await _db.Contracts.FindAsync(id);
            if (contract == null) return NotFound(new { Message = "Contract not found." });

            if (!DateTime.TryParse(dto.StartDate, out var startDate))
                return BadRequest(new { Message = "Invalid start date." });

            if (!DateTime.TryParse(dto.EndDate, out var endDate))
                return BadRequest(new { Message = "Invalid end date." });

            if (endDate <= startDate)
                return BadRequest(new { Message = "End date must be after start date." });

            contract.StartDate    = startDate;
            contract.EndDate      = endDate;
            contract.ServiceLevel = (ServiceLevel)dto.ServiceLevel;
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Contract updated successfully." });
        }

        // ── PATCH /api/contracts/{id}/status ────────────────────────────────
        /// <summary>
        /// Approve, decline, or change the status of a contract.
        /// Admins can set any status; Clients can only sign (set to Active).
        /// </summary>
        [HttpPatch("{id:int}/status")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> PatchStatus(int id, [FromBody] ChangeStatusDto dto)
        {
            var query = _db.Contracts.AsQueryable();
            if (!IsAdmin) query = query.Where(c => c.ClientId == UserId);

            var contract = await query.FirstOrDefaultAsync(c => c.ContractId == id);
            if (contract == null) return NotFound(new { Message = "Contract not found." });

            // Clients can only activate (sign) their own contracts
            if (!IsAdmin && dto.Status != ContractStatus.Active)
                return Forbid();

            contract.ChangeStatus(dto.Status);
            await _db.SaveChangesAsync();

            return Ok(new { Message = $"Contract status updated to {dto.Status}." });
        }

        // ── DELETE /api/contracts/{id} ───────────────────────────────────────
        /// <summary>Delete a contract (Admin only).</summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteContract(int id)
        {
            var contract = await _db.Contracts.FindAsync(id);
            if (contract == null) return NotFound(new { Message = "Contract not found." });

            _db.Contracts.Remove(contract);
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Contract deleted successfully." });
        }

        // ── GET /api/contracts/{id}/download ────────────────────────────────
        /// <summary>Download the signed PDF agreement for a contract.</summary>
        [HttpGet("{id:int}/download")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DownloadAgreement(int id)
        {
            var contract = await _db.Contracts.FindAsync(id);
            if (contract?.SignedAgreementPath == null)
                return NotFound(new { Message = "Agreement not found." });

            var stream = _fs.GetFile(contract.SignedAgreementPath);
            if (stream == null)
                return NotFound(new { Message = "File not found on server." });

            return File(stream, "application/pdf", $"Contract_{contract.ContractNumber}.pdf");
        }
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────
    public class CreateContractDto
    {
        [Required] public string  ClientId     { get; set; } = string.Empty;
        [Required] public string  StartDate    { get; set; } = string.Empty;
        [Required] public string  EndDate      { get; set; } = string.Empty;
        [Required] public int     ServiceLevel { get; set; }
        public IFormFile? Agreement { get; set; }
    }

    public class UpdateContractDto
    {
        [Required] public string StartDate    { get; set; } = string.Empty;
        [Required] public string EndDate      { get; set; } = string.Empty;
        [Required] public int    ServiceLevel { get; set; }
    }

    public class ChangeStatusDto
    {
        [Required] public ContractStatus Status { get; set; }
    }
}
