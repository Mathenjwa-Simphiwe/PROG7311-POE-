using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using TechMoveAPI.Data;
using TechMoveAPI.Factory;
using TechMoveAPI.Models;
using TechMoveAPI.Services;

namespace TechMoveAPI.Controllers
{
    /// <summary>
    /// Manages freight and maintenance service requests.
    /// Admins can see and action all requests; Clients manage their own.
    /// </summary>
    [ApiController]
    [Route("api/service-requests")]
    [Authorize]
    [Produces("application/json")]
    public class ServiceRequestsController : ControllerBase
    {
        private readonly AppDbContext        _db;
        private readonly ExchangeRateService _fx;

        public ServiceRequestsController(AppDbContext db, ExchangeRateService fx)
        {
            _db = db;
            _fx = fx;
        }

        private string UserId  => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        private bool   IsAdmin => User.IsInRole("Admin");

        // ── GET /api/service-requests ────────────────────────────────────────
        /// <summary>Get all service requests. Admins see all; Clients see their own.</summary>
        [HttpGet]
        [ProducesResponseType(200)]
        public async Task<IActionResult> GetAll([FromQuery] ServiceRequestStatus? status)
        {
            var query = _db.ServiceRequests
                .Include(r => r.Contract).ThenInclude(c => c!.Client)
                .OrderByDescending(r => r.RequestDate)
                .AsQueryable();

            if (!IsAdmin)  query = query.Where(r => r.Contract!.ClientId == UserId);
            if (status.HasValue) query = query.Where(r => r.Status == status.Value);

            return Ok(await query.Select(r => new
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
                ClientName     = r.Contract.Client!.FullName,
                r.AdminNotes,
                r.RequestDate,
                r.DecisionDate
            }).ToListAsync());
        }

        // ── GET /api/service-requests/{id} ───────────────────────────────────
        /// <summary>Get a single service request by ID.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetById(int id)
        {
            var query = _db.ServiceRequests
                .Include(r => r.Contract).ThenInclude(c => c!.Client)
                .AsQueryable();

            if (!IsAdmin) query = query.Where(r => r.Contract!.ClientId == UserId);

            var r = await query.FirstOrDefaultAsync(r => r.ServiceRequestId == id);
            if (r == null) return NotFound(new { Message = "Request not found." });

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
                ClientName     = r.Contract.Client!.FullName,
                r.AdminNotes,
                r.RequestDate,
                r.DecisionDate
            });
        }

        // ── POST /api/service-requests ───────────────────────────────────────
        /// <summary>
        /// Create a new Freight or Maintenance service request (Client only).
        /// Set Type=0 for Freight, Type=1 for Maintenance.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Client")]
        [ProducesResponseType(201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Create([FromBody] CreateRequestDto dto)
        {
            var contract = await _db.Contracts
                .FirstOrDefaultAsync(c => c.ContractId == dto.ContractId && c.ClientId == UserId);

            if (contract == null)
                return NotFound(new { Message = "Contract not found or does not belong to you." });

            if (contract.Status is ContractStatus.Expired or ContractStatus.OnHold)
                return BadRequest(new { Message = "Cannot create request for an expired or on-hold contract." });

            var data = new Dictionary<string, object>
            {
                ["Origin"]      = dto.Origin.Trim(),
                ["Description"] = dto.Description.Trim()
            };

            ServiceRequest request;

            if (dto.Type == 0) // Freight
            {
                if (string.IsNullOrWhiteSpace(dto.Destination) || dto.WeightKg == null)
                    return BadRequest(new { Message = "Freight requests require Destination and WeightKg." });

                data["Destination"] = dto.Destination.Trim();
                data["WeightKg"]    = dto.WeightKg.Value;
                request = new FreightRequestFactory().Create(data);
            }
            else // Maintenance
            {
                if (string.IsNullOrWhiteSpace(dto.EquipmentType) || dto.EstimatedHours == null)
                    return BadRequest(new { Message = "Maintenance requests require EquipmentType and EstimatedHours." });

                data["EquipmentType"]  = dto.EquipmentType.Trim();
                data["EstimatedHours"] = dto.EstimatedHours.Value;
                request = new MaintenanceRequestFactory().Create(data);
            }

            if (dto.UsdAmount is > 0)
            {
                var (zarAmount, rateUsed) = await _fx.ConvertUsdToZarAsync(dto.UsdAmount.Value);
                request.UsdAmount        = dto.UsdAmount.Value;
                request.LocalCostZar     = zarAmount;
                request.ExchangeRateUsed = rateUsed;
            }

            request.ContractId = dto.ContractId;
            _db.ServiceRequests.Add(request);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = request.ServiceRequestId }, new
            {
                request.RequestId,
                request.Cost,
                request.UsdAmount,
                request.LocalCostZar,
                request.ExchangeRateUsed,
                Message = "Service request created successfully."
            });
        }

        // ── PUT /api/service-requests/{id} ───────────────────────────────────
        /// <summary>Update a pending service request's origin and description (Client only).</summary>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Client")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateRequestDto dto)
        {
            var r = await _db.ServiceRequests
                .Include(x => x.Contract)
                .FirstOrDefaultAsync(x => x.ServiceRequestId == id && x.Contract!.ClientId == UserId);

            if (r == null) return NotFound(new { Message = "Request not found." });
            if (r.Status != ServiceRequestStatus.Pending)
                return BadRequest(new { Message = "Only pending requests can be edited." });

            r.Origin      = dto.Origin.Trim();
            r.Description = dto.Description.Trim();
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Request updated successfully." });
        }

        // ── PATCH /api/service-requests/{id}/approve ─────────────────────────
        /// <summary>Approve a service request (Admin only).</summary>
        [HttpPatch("{id:int}/approve")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Approve(int id, [FromBody] DecisionDto dto)
        {
            var r = await _db.ServiceRequests.FindAsync(id);
            if (r == null) return NotFound(new { Message = "Request not found." });

            r.Status       = ServiceRequestStatus.Approved;
            r.AdminNotes   = dto.Notes;
            r.DecisionDate = DateTime.Now;
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Request approved." });
        }

        // ── PATCH /api/service-requests/{id}/decline ─────────────────────────
        /// <summary>Decline a service request (Admin only).</summary>
        [HttpPatch("{id:int}/decline")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Decline(int id, [FromBody] DecisionDto dto)
        {
            var r = await _db.ServiceRequests.FindAsync(id);
            if (r == null) return NotFound(new { Message = "Request not found." });

            r.Status       = ServiceRequestStatus.Denied;
            r.AdminNotes   = dto.Notes;
            r.DecisionDate = DateTime.Now;
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Request declined." });
        }

        // ── DELETE /api/service-requests/{id} ────────────────────────────────
        /// <summary>Cancel (delete) a pending service request.</summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Delete(int id)
        {
            var query = _db.ServiceRequests.Include(r => r.Contract).AsQueryable();
            if (!IsAdmin) query = query.Where(r => r.Contract!.ClientId == UserId);

            var r = await query.FirstOrDefaultAsync(r => r.ServiceRequestId == id);
            if (r == null) return NotFound(new { Message = "Request not found." });

            if (r.Status != ServiceRequestStatus.Pending)
                return BadRequest(new { Message = "Only pending requests can be cancelled." });

            _db.ServiceRequests.Remove(r);
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Request cancelled." });
        }
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────
    public class CreateRequestDto
    {
        [Required] public int     ContractId     { get; set; }
        [Required] public int     Type           { get; set; }  // 0=Freight, 1=Maintenance
        [Required] public string  Origin         { get; set; } = string.Empty;
        [Required] public string  Description    { get; set; } = string.Empty;
        public string?  Destination    { get; set; }
        public decimal? WeightKg       { get; set; }
        public string?  EquipmentType  { get; set; }
        public int?     EstimatedHours { get; set; }
        public decimal? UsdAmount      { get; set; }
    }

    public class UpdateRequestDto
    {
        [Required] public string Origin      { get; set; } = string.Empty;
        [Required] public string Description { get; set; } = string.Empty;
    }

    public class DecisionDto
    {
        public string? Notes { get; set; }
    }
}
