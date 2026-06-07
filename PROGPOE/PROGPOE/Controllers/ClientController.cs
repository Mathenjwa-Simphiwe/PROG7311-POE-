using Microsoft.AspNetCore.Mvc;
using PROGPOE.Services;
using PROGPOE.ViewModels;
using System.Text.Json;

namespace PROGPOE.Controllers
{
    public class ClientController : Controller
    {
        private readonly TechMoveApiService _api;

        public ClientController(TechMoveApiService api)
        {
            _api = api;
        }

        // GET: /Client/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var data = await _api.GetDashboardAsync();
            if (data == null) return View(new ClientDashboardViewModel());

            var model = new ClientDashboardViewModel
            {
                TotalContracts = GetInt(data, "totalContracts"),
                ActiveContracts = GetInt(data, "activeContracts"),
                PendingRequests = GetInt(data, "pendingRequests")
            };
            return View(model);
        }

        // GET: /Client/ClientContracts
        public async Task<IActionResult> ClientContracts(string? status = null)
        {
            var data = await _api.GetContractsAsync(status: status);
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
                        HasAgreement = GetBool(item, "hasAgreement")
                    });
                }
            }
            return View(contracts);
        }

        // GET: /Client/Requests
        public async Task<IActionResult> Requests(string? status = null)
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

        // Same helper methods as in AdminController (copy them here or move to a static utility class)
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