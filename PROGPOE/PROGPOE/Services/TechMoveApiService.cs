using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PROGPOE.Services
{
    /// <summary>
    /// Wraps all HTTP calls to the TechMoveAPI backend.
    /// Reads the bearer token from the current session and attaches it automatically.
    /// </summary>
    public class TechMoveApiService
    {
        private readonly HttpClient         _http;
        private readonly IHttpContextAccessor _ctx;
        private readonly ILogger<TechMoveApiService> _logger;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public TechMoveApiService(
            HttpClient              http,
            IHttpContextAccessor    ctx,
            ILogger<TechMoveApiService> logger)
        {
            _http   = http;
            _ctx    = ctx;
            _logger = logger;
        }

        // ── Auth ─────────────────────────────────────────────────────────────

        /// <summary>POST /api/auth/login – returns the raw JSON response (token, role, fullName).</summary>
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

        /// <summary>GET /api/contracts with optional query parameters.</summary>
        public async Task<JsonElement?> GetContractsAsync(
            string? startDate = null,
            string? endDate   = null,
            string? status    = null)
        {
            AttachToken();
            var qs = BuildQuery(("startDate", startDate), ("endDate", endDate), ("status", status));
            var resp = await _http.GetAsync($"api/contracts{qs}");
            return await ReadJson(resp);
        }

        /// <summary>GET /api/contracts/{id}</summary>
        public async Task<JsonElement?> GetContractAsync(int id)
        {
            AttachToken();
            var resp = await _http.GetAsync($"api/contracts/{id}");
            return await ReadJson(resp);
        }

        /// <summary>POST /api/contracts (multipart form for optional PDF)</summary>
        public async Task<JsonElement?> CreateContractAsync(
            string  clientId,
            string  startDate,
            string  endDate,
            int     serviceLevel,
            IFormFile? agreement = null)
        {
            AttachToken();

            var form = new MultipartFormDataContent();
            form.Add(new StringContent(clientId),              "clientId");
            form.Add(new StringContent(startDate),             "startDate");
            form.Add(new StringContent(endDate),               "endDate");
            form.Add(new StringContent(serviceLevel.ToString()), "serviceLevel");

            if (agreement != null)
            {
                var ms = new MemoryStream();
                await agreement.CopyToAsync(ms);
                ms.Position = 0;
                form.Add(new StreamContent(ms), "agreement", agreement.FileName);
            }

            var resp = await _http.PostAsync("api/contracts", form);
            return await ReadJson(resp);
        }

        /// <summary>PATCH /api/contracts/{id}/status</summary>
        public async Task<JsonElement?> PatchContractStatusAsync(int id, int status)
        {
            AttachToken();
            var body = JsonSerializer.Serialize(new { status });
            var resp = await _http.PatchAsync($"api/contracts/{id}/status",
                new StringContent(body, Encoding.UTF8, "application/json"));
            return await ReadJson(resp);
        }

        /// <summary>PUT /api/contracts/{id}</summary>
        public async Task<JsonElement?> UpdateContractAsync(int id, string startDate, string endDate, int serviceLevel)
        {
            AttachToken();
            var body = JsonSerializer.Serialize(new { startDate, endDate, serviceLevel });
            var resp = await _http.PutAsync($"api/contracts/{id}",
                new StringContent(body, Encoding.UTF8, "application/json"));
            return await ReadJson(resp);
        }

        /// <summary>DELETE /api/contracts/{id}</summary>
        public async Task<JsonElement?> DeleteContractAsync(int id)
        {
            AttachToken();
            var resp = await _http.DeleteAsync($"api/contracts/{id}");
            return await ReadJson(resp);
        }

        // ── Service Requests ─────────────────────────────────────────────────

        /// <summary>GET /api/service-requests</summary>
        public async Task<JsonElement?> GetServiceRequestsAsync(string? status = null)
        {
            AttachToken();
            var qs   = BuildQuery(("status", status));
            var resp = await _http.GetAsync($"api/service-requests{qs}");
            return await ReadJson(resp);
        }

        /// <summary>POST /api/service-requests</summary>
        public async Task<JsonElement?> CreateServiceRequestAsync(object dto)
        {
            AttachToken();
            var body = JsonSerializer.Serialize(dto);
            var resp = await _http.PostAsync("api/service-requests",
                new StringContent(body, Encoding.UTF8, "application/json"));
            return await ReadJson(resp);
        }

        /// <summary>PATCH /api/service-requests/{id}/approve</summary>
        public async Task<JsonElement?> ApproveRequestAsync(int id, string? notes)
        {
            AttachToken();
            var body = JsonSerializer.Serialize(new { notes });
            var resp = await _http.PatchAsync($"api/service-requests/{id}/approve",
                new StringContent(body, Encoding.UTF8, "application/json"));
            return await ReadJson(resp);
        }

        /// <summary>PATCH /api/service-requests/{id}/decline</summary>
        public async Task<JsonElement?> DeclineRequestAsync(int id, string? notes)
        {
            AttachToken();
            var body = JsonSerializer.Serialize(new { notes });
            var resp = await _http.PatchAsync($"api/service-requests/{id}/decline",
                new StringContent(body, Encoding.UTF8, "application/json"));
            return await ReadJson(resp);
        }

        // ── Dashboard ─────────────────────────────────────────────────────────

        /// <summary>GET /api/dashboard</summary>
        public async Task<JsonElement?> GetDashboardAsync()
        {
            AttachToken();
            var resp = await _http.GetAsync("api/dashboard");
            return await ReadJson(resp);
        }

        // ── Clients ──────────────────────────────────────────────────────────

        /// <summary>GET /api/clients (admin only)</summary>
        public async Task<JsonElement?> GetClientsAsync()
        {
            AttachToken();
            var resp = await _http.GetAsync("api/clients");
            return await ReadJson(resp);
        }

        // ── Internals ────────────────────────────────────────────────────────

        private void AttachToken()
        {
            var token = _ctx.HttpContext?.Session.GetString("JwtToken");
            if (!string.IsNullOrEmpty(token))
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
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
