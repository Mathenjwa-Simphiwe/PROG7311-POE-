using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PROGPOE.Services
{
    public class TechMoveApiService
    {
        private readonly HttpClient _http;
        private readonly IHttpContextAccessor _ctx;
        private readonly ILogger<TechMoveApiService> _logger;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public TechMoveApiService(
            HttpClient http,
            IHttpContextAccessor ctx,
            ILogger<TechMoveApiService> logger)
        {
            _http = http;
            _ctx = ctx;
            _logger = logger;
        }

        // ── Auth ─────────────────────────────────────────────────────────────
        public async Task<JsonElement?> LoginAsync(string email, string password)
        {
            var body = JsonSerializer.Serialize(new { email, password });
            var resp = await _http.PostAsync("api/auth/login",
                new StringContent(body, Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(json);
        }

        // ── Contracts ────────────────────────────────────────────────────────
        public async Task<JsonElement?> GetContractsAsync(
            string? startDate = null,
            string? endDate = null,
            string? status = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"api/contracts{BuildQuery(("startDate", startDate), ("endDate", endDate), ("status", status))}");
            await AttachToken(request);
            var resp = await _http.SendAsync(request);
            return await ReadJson(resp);
        }

        public async Task<JsonElement?> GetContractAsync(int id)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/contracts/{id}");
            await AttachToken(request);
            var resp = await _http.SendAsync(request);
            return await ReadJson(resp);
        }

        public async Task<JsonElement?> CreateContractAsync(
            string clientId,
            string startDate,
            string endDate,
            int serviceLevel,
            IFormFile? agreement = null)
        {
            var form = new MultipartFormDataContent();
            form.Add(new StringContent(clientId), "clientId");
            form.Add(new StringContent(startDate), "startDate");
            form.Add(new StringContent(endDate), "endDate");
            form.Add(new StringContent(serviceLevel.ToString()), "serviceLevel");

            if (agreement != null)
            {
                var ms = new MemoryStream();
                await agreement.CopyToAsync(ms);
                ms.Position = 0;
                form.Add(new StreamContent(ms), "agreement", agreement.FileName);
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/contracts") { Content = form };
            await AttachToken(request);
            var resp = await _http.SendAsync(request);
            return await ReadJson(resp);
        }

        public async Task<JsonElement?> PatchContractStatusAsync(int id, int status)
        {
            var body = JsonSerializer.Serialize(new { status });
            using var request = new HttpRequestMessage(HttpMethod.Patch, $"api/contracts/{id}/status")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            await AttachToken(request);
            var resp = await _http.SendAsync(request);
            return await ReadJson(resp);
        }

        public async Task<JsonElement?> UpdateContractAsync(int id, string startDate, string endDate, int serviceLevel)
        {
            var body = JsonSerializer.Serialize(new { startDate, endDate, serviceLevel });
            using var request = new HttpRequestMessage(HttpMethod.Put, $"api/contracts/{id}")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            await AttachToken(request);
            var resp = await _http.SendAsync(request);
            return await ReadJson(resp);
        }

        public async Task<JsonElement?> DeleteContractAsync(int id)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/contracts/{id}");
            await AttachToken(request);
            var resp = await _http.SendAsync(request);
            return await ReadJson(resp);
        }

        // ── Service Requests ─────────────────────────────────────────────────
        public async Task<JsonElement?> GetServiceRequestsAsync(string? status = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/service-requests{BuildQuery(("status", status))}");
            await AttachToken(request);
            var resp = await _http.SendAsync(request);
            return await ReadJson(resp);
        }

        public async Task<JsonElement?> CreateServiceRequestAsync(object dto)
        {
            var body = JsonSerializer.Serialize(dto);
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/service-requests")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            await AttachToken(request);
            var resp = await _http.SendAsync(request);
            return await ReadJson(resp);
        }

        public async Task<JsonElement?> UpdateServiceRequestAsync(int id, object dto)
        {
            var body = JsonSerializer.Serialize(dto);
            using var request = new HttpRequestMessage(HttpMethod.Put, $"api/service-requests/{id}")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            await AttachToken(request);
            var resp = await _http.SendAsync(request);
            return await ReadJson(resp);
        }

        public async Task<JsonElement?> DeleteServiceRequestAsync(int id)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/service-requests/{id}");
            await AttachToken(request);
            var resp = await _http.SendAsync(request);
            return await ReadJson(resp);
        }

        public async Task<JsonElement?> ApproveRequestAsync(int id, string? notes)
        {
            var body = JsonSerializer.Serialize(new { notes });
            using var request = new HttpRequestMessage(HttpMethod.Patch, $"api/service-requests/{id}/approve")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            await AttachToken(request);
            var resp = await _http.SendAsync(request);
            return await ReadJson(resp);
        }

        public async Task<JsonElement?> DeclineRequestAsync(int id, string? notes)
        {
            var body = JsonSerializer.Serialize(new { notes });
            using var request = new HttpRequestMessage(HttpMethod.Patch, $"api/service-requests/{id}/decline")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            await AttachToken(request);
            var resp = await _http.SendAsync(request);
            return await ReadJson(resp);
        }

        // ── Dashboard ─────────────────────────────────────────────────────────
        public async Task<JsonElement?> GetDashboardAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/dashboard");
            await AttachToken(request);
            var resp = await _http.SendAsync(request);
            return await ReadJson(resp);
        }

        // ── Clients ──────────────────────────────────────────────────────────
        public async Task<JsonElement?> GetClientsAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/clients");
            await AttachToken(request);
            var resp = await _http.SendAsync(request);
            return await ReadJson(resp);
        }

        // ── Internals ────────────────────────────────────────────────────────
        private async Task AttachToken(HttpRequestMessage request)
        {
            var token = _ctx.HttpContext?.Session.GetString("JwtToken");
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            else
                _logger.LogWarning("AttachToken: No JwtToken found in session.");
        }

        private static string BuildQuery(params (string key, string? value)[] pairs)
        {
            var parts = pairs
                .Where(p => !string.IsNullOrEmpty(p.value))
                .Select(p => $"{p.key}={Uri.EscapeDataString(p.value!)}");
            var qs = string.Join("&", parts);
            return qs.Length > 0 ? "?" + qs : string.Empty;
        }

        private async Task<JsonElement?> ReadJson(HttpResponseMessage resp)
        {
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("TechMoveAPI {Status}: {Body}", (int)resp.StatusCode, json);
                return null;
            }

            return JsonSerializer.Deserialize<JsonElement>(json, JsonOpts);
        }
    }
}