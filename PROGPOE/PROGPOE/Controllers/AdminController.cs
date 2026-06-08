using Microsoft.AspNetCore.Mvc;
using PROGPOE.Services;
using PROGPOE.ViewModels;
using System.Text.Json;

namespace PROGPOE.Controllers
{
    // ─────────────────────────────────────────────────────────────────────────
    //  MVC CONTROLLER – serves Razor views for the Admin UI.
    //  All data is fetched from TechMoveAPI via JavaScript (fetch/XHR) in the
    //  views, using the JWT stored in sessionStorage after login.
    //
    //  If you need server-side data (e.g. for pre-rendering), inject
    //  TechMoveApiService here and call it before returning the view.
    // ─────────────────────────────────────────────────────────────────────────
    public class AdminController : Controller
    {
        private readonly TechMoveApiService _api;

        public AdminController(TechMoveApiService api)
        {
            _api = api;
        }

        // GET: /Admin/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var data = await _api.GetDashboardAsync();
            if (data == null) return View(new AdminDashboardViewModel());

            var model = new AdminDashboardViewModel
            {
                TotalContracts = GetInt((JsonElement)data, "TotalContracts"),
                ActiveContracts = GetInt((JsonElement)data, "ActiveContracts"),
                PendingRequests = GetInt((JsonElement)data, "PendingRequests"),
                TotalClients = GetInt((JsonElement)data, "TotalClients")
            };
            return View(model);
        }

        // GET: /Admin/Contracts
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
            {
                foreach (var item in data.Value.EnumerateArray())
                {
                    contracts.Add(new ContractViewModel
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
                    });
                }
            }
            return View(contracts);
        }

        // GET: /Admin/ServiceRequests
        public async Task<IActionResult> ServiceRequests(string? status = null)
        {
            var data = await _api.GetServiceRequestsAsync(status);
            var requests = new List<ServiceRequestViewModel>();
            if (data?.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.Value.EnumerateArray())
                {
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
                }
            }
            return View(requests);
        }
        [HttpPost]
        public async Task<IActionResult> SignContract(int id)
        {
            var result = await _api.PatchContractStatusAsync(id, 1);
            if (result == null) TempData["Error"] = "Failed to activate contract.";
            else TempData["Success"] = "Contract activated!";
            return RedirectToAction("ClientContracts");
        }

        [HttpGet]
        public async Task<IActionResult> GetContracts()
        {
            var data = await _api.GetContractsAsync();
            var list = new List<object>();
            if (data?.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.Value.EnumerateArray())
                {
                    list.Add(new { contractId = GetInt(item, "contractId"), contractNumber = GetString(item, "contractNumber"), status = GetString(item, "status") });
                }
            }
            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] object dto)
        {
            var result = await _api.CreateServiceRequestAsync(dto);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, [FromBody] object dto)
        {
            var result = await _api.UpdateServiceRequestAsync(id, dto); // you need to add this method to TechMoveApiService
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _api.DeleteServiceRequestAsync(id);
            return Ok(result);
        }
        private static int GetInt(JsonElement el, string prop) =>
          el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

        private static string GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

        private static DateTime GetDateTime(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.TryGetDateTime(out var dt) ? dt : DateTime.MinValue;

        private static DateTime? GetNullableDateTime(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.TryGetDateTime(out var dt) ? dt : null;

        private static decimal? GetDecimal(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;

        private static bool GetBool(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.True ? true : false;
    }

}
