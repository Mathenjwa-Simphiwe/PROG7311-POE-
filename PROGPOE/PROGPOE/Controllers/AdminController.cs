using Microsoft.AspNetCore.Mvc;
using PROGPOE.Services;
using PROGPOE.ViewModels;
using System.Text.Json;

namespace PROGPOE.Controllers
{
    public class AdminController : Controller
    {
        private readonly TechMoveApiService _api;

        public AdminController(TechMoveApiService api) => _api = api;

        // ── Dashboard ────────────────────────────────────────────────────────
        public async Task<IActionResult> Dashboard()
        {
            var data = await _api.GetDashboardAsync();
            if (data == null) return View(new AdminDashboardViewModel());

            return View(new AdminDashboardViewModel
            {
                TotalContracts = GetInt((JsonElement)data, "totalContracts"),
                ActiveContracts = GetInt((JsonElement)data, "activeContracts"),
                PendingRequests = GetInt((JsonElement)data, "pendingRequests"),
                TotalClients = GetInt((JsonElement)data, "totalClients")
            });
        }

        // ── Contracts list ───────────────────────────────────────────────────
        public async Task<IActionResult> Contracts(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? status = null)
        {
            var data = await _api.GetContractsAsync(
                startDate?.ToString("yyyy-MM-dd"),
                endDate?.ToString("yyyy-MM-dd"),
                status);

            var contracts = new List<ContractViewModel>();
            if (data?.ValueKind == JsonValueKind.Array)
                foreach (var item in data.Value.EnumerateArray())
                    contracts.Add(MapContract(item));

            return View(contracts);
        }

        // ── GET /Admin/CreateContract ────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> CreateContract()
        {
            var clients = await GetClientsListAsync();
            return View(clients);
        }

        // ── POST /Admin/CreateContract ───────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateContract(
            string clientId,
            string startDate,
            string endDate,
            int serviceLevel,
            IFormFile? agreement)
        {
            var result = await _api.CreateContractAsync(clientId, startDate, endDate, serviceLevel, agreement);

            if (result == null)
            {
                TempData["Error"] = "Failed to create contract. Check that the dates are valid.";
                return View(await GetClientsListAsync());
            }

            TempData["Success"] = "Contract created successfully!";
            return RedirectToAction(nameof(Contracts));
        }

        // ── GET /Admin/EditContract/{id} ─────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> EditContract(int id)
        {
            var data = await _api.GetContractAsync(id);
            if (data == null)
            {
                TempData["Error"] = "Contract not found.";
                return RedirectToAction(nameof(Contracts));
            }

            return View(MapContract(data.Value));
        }

        // ── POST /Admin/EditContract ─────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditContract(int id, string startDate, string endDate, int serviceLevel)
        {
            var result = await _api.UpdateContractAsync(id, startDate, endDate, serviceLevel);

            if (result == null)
                TempData["Error"] = "Failed to update contract.";
            else
                TempData["Success"] = "Contract updated successfully!";

            return RedirectToAction(nameof(Contracts));
        }

        // ── POST /Admin/UpdateStatus ─────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, int status)
        {
            var result = await _api.PatchContractStatusAsync(id, status);

            if (result == null)
                TempData["Error"] = "Failed to update contract status.";
            else
                TempData["Success"] = "Status updated!";

            return RedirectToAction(nameof(Contracts));
        }

        // ── POST /Admin/DeleteContract ───────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteContract(int id)
        {
            var result = await _api.DeleteContractAsync(id);

            if (result == null)
                TempData["Error"] = "Failed to delete contract.";
            else
                TempData["Success"] = "Contract deleted.";

            return RedirectToAction(nameof(Contracts));
        }

        // ── Service Requests list ────────────────────────────────────────────
        public async Task<IActionResult> ServiceRequests(string? status = null)
        {
            var data = await _api.GetServiceRequestsAsync(status);
            var requests = new List<ServiceRequestViewModel>();

            if (data?.ValueKind == JsonValueKind.Array)
                foreach (var item in data.Value.EnumerateArray())
                    requests.Add(new ServiceRequestViewModel
                    {
                        ServiceRequestId = GetInt(item, "serviceRequestId"),
                        RequestId = GetString(item, "requestId"),
                        Type = GetString(item, "type"),
                        ContractNumber = GetString(item, "contractNumber"),
                        ClientName = GetString(item, "clientName"),
                        Description = GetString(item, "description"),
                        Cost = GetDecimal(item, "cost"),
                        LocalCostZar = GetDecimal(item, "localCostZar"),
                        Status = GetString(item, "status"),
                        AdminNotes = GetString(item, "adminNotes"),
                        RequestDate = GetDateTime(item, "requestDate"),
                        DecisionDate = GetNullableDateTime(item, "decisionDate")
                    });

            return View(requests);
        }

        // ── POST /Admin/ApproveRequest ───────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRequest(int id, string? notes)
        {
            var result = await _api.ApproveRequestAsync(id, notes);
            TempData[result == null ? "Error" : "Success"] =
                result == null ? "Failed to approve request." : "Request approved!";
            return RedirectToAction(nameof(ServiceRequests));
        }

        // ── POST /Admin/DeclineRequest ───────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineRequest(int id, string? notes)
        {
            var result = await _api.DeclineRequestAsync(id, notes);
            TempData[result == null ? "Error" : "Success"] =
                result == null ? "Failed to decline request." : "Request declined.";
            return RedirectToAction(nameof(ServiceRequests));
        }

        // ── POST /Admin/DeleteServiceRequest ─────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteServiceRequest(int id)
        {
            var result = await _api.DeleteServiceRequestAsync(id);
            TempData[result == null ? "Error" : "Success"] =
                result == null ? "Failed to delete request." : "Request deleted.";
            return RedirectToAction(nameof(ServiceRequests));
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private async Task<List<ClientViewModel>> GetClientsListAsync()
        {
            var data = await _api.GetClientsAsync();
            var list = new List<ClientViewModel>();

            if (data?.ValueKind == JsonValueKind.Array)
                foreach (var item in data.Value.EnumerateArray())
                    list.Add(new ClientViewModel
                    {
                        Id = GetString(item, "id"),
                        FullName = GetString(item, "fullName"),
                        Email = GetString(item, "email"),
                        Region = GetString(item, "region")
                    });

            return list;
        }

        private static ContractViewModel MapContract(JsonElement item) => new()
        {
            ContractId = GetInt(item, "contractId"),
            ContractNumber = GetString(item, "contractNumber"),
            StartDate = GetDateTime(item, "startDate"),
            EndDate = GetDateTime(item, "endDate"),
            Status = GetString(item, "status"),
            ServiceLevel = GetString(item, "serviceLevel"),
            ClientName = GetString(item, "clientName"),
            ClientId = GetString(item, "clientId"),
            HasAgreement = GetBool(item, "hasAgreement")
        };

        private static int GetInt(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

        private static DateTime GetDateTime(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String && v.TryGetDateTime(out var dt) ? dt : DateTime.MinValue;

        private static DateTime? GetNullableDateTime(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String && v.TryGetDateTime(out var dt) ? dt : (DateTime?)null;

        private static decimal? GetDecimal(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;

        private static bool GetBool(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.True;
    }
}