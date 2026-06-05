using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace PROGPOE.Tests
{
    /// <summary>
    /// Automated API Integration Tests.
    /// Tests run against the live running API endpoints and assert HTTP status codes + JSON responses.
    /// These prevent "breaking changes" in a DevOps pipeline before deployment.
    /// </summary>
    public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        private string? _adminToken;
        private string? _clientToken;

        public ApiIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
        }

        // ── HELPER: get a JWT token ───────────────────────────────────
        private async Task<string?> LoginAsync(string email, string password)
        {
            var payload = new { Email = email, Password = password };
            var res     = await _client.PostAsJsonAsync("/api/account/login", payload);
            if (!res.IsSuccessStatusCode) return null;

            var body = await res.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("token").GetString();
        }

        private HttpRequestMessage AuthRequest(HttpMethod method, string url, string token, object? body = null)
        {
            var req = new HttpRequestMessage(method, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            if (body != null)
                req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            return req;
        }

        // ── AUTH TESTS ───────────────────────────────────────────────

        [Fact]
        public async Task Login_Admin_ReturnsToken()
        {
            var token = await LoginAsync("admin@glms.com", "Admin@123");
            Assert.NotNull(token);
            Assert.NotEmpty(token);
        }

        [Fact]
        public async Task Login_Client_ReturnsToken()
        {
            var token = await LoginAsync("client@test.com", "Client@123");
            Assert.NotNull(token);
            Assert.NotEmpty(token);
        }

        [Fact]
        public async Task Login_InvalidCredentials_Returns401()
        {
            var res = await _client.PostAsJsonAsync("/api/account/login", new { Email = "fake@test.com", Password = "wrong" });
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }

        // ── UNAUTHENTICATED ACCESS ───────────────────────────────────

        [Fact]
        public async Task AdminContracts_WithoutToken_Returns401()
        {
            var res = await _client.GetAsync("/api/admin/contracts");
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }

        [Fact]
        public async Task ClientContracts_WithoutToken_Returns401()
        {
            var res = await _client.GetAsync("/api/client/contracts");
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }

        [Fact]
        public async Task AdminEndpoint_WithClientToken_Returns403()
        {
            var token = await LoginAsync("client@test.com", "Client@123");
            Assert.NotNull(token);

            var req = AuthRequest(HttpMethod.Get, "/api/admin/contracts", token!);
            var res = await _client.SendAsync(req);

            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }

        // ── ADMIN DASHBOARD ─────────────────────────────────────────

        [Fact]
        public async Task AdminDashboard_Returns200_WithStats()
        {
            var token = await LoginAsync("admin@glms.com", "Admin@123");
            Assert.NotNull(token);

            var req = AuthRequest(HttpMethod.Get, "/api/admin/dashboard", token!);
            var res = await _client.SendAsync(req);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var body = await res.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.TryGetProperty("totalContracts", out _) || body.TryGetProperty("TotalContracts", out _));
        }

        // ── ADMIN CONTRACTS CRUD ─────────────────────────────────────

        [Fact]
        public async Task GetContracts_ReturnsOK_AndNotNull()
        {
            var token = await LoginAsync("admin@glms.com", "Admin@123");
            Assert.NotNull(token);

            var req = AuthRequest(HttpMethod.Get, "/api/admin/contracts", token!);
            var res = await _client.SendAsync(req);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var body = await res.Content.ReadAsStringAsync();
            Assert.NotNull(body);
        }

        [Fact]
        public async Task GetContracts_WithStatusFilter_ReturnsOK()
        {
            var token = await LoginAsync("admin@glms.com", "Admin@123");
            Assert.NotNull(token);

            var req = AuthRequest(HttpMethod.Get, "/api/admin/contracts?status=0", token!);
            var res = await _client.SendAsync(req);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        [Fact]
        public async Task GetClients_Returns200_NotNull()
        {
            var token = await LoginAsync("admin@glms.com", "Admin@123");
            Assert.NotNull(token);

            var req = AuthRequest(HttpMethod.Get, "/api/admin/clients", token!);
            var res = await _client.SendAsync(req);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var body = await res.Content.ReadAsStringAsync();
            Assert.NotNull(body);
        }

        // ── ADMIN SERVICE REQUESTS ───────────────────────────────────

        [Fact]
        public async Task GetServiceRequests_ReturnsOK_NotNull()
        {
            var token = await LoginAsync("admin@glms.com", "Admin@123");
            Assert.NotNull(token);

            var req = AuthRequest(HttpMethod.Get, "/api/admin/service-requests", token!);
            var res = await _client.SendAsync(req);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var body = await res.Content.ReadAsStringAsync();
            Assert.NotNull(body);
        }

        [Fact]
        public async Task ApproveNonExistentRequest_Returns404()
        {
            var token = await LoginAsync("admin@glms.com", "Admin@123");
            Assert.NotNull(token);

            var req = AuthRequest(HttpMethod.Patch, "/api/admin/service-requests/99999/approve", token!, new { notes = "test" });
            var res = await _client.SendAsync(req);

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task DeclineNonExistentRequest_Returns404()
        {
            var token = await LoginAsync("admin@glms.com", "Admin@123");
            Assert.NotNull(token);

            var req = AuthRequest(HttpMethod.Patch, "/api/admin/service-requests/99999/decline", token!, new { notes = "test" });
            var res = await _client.SendAsync(req);

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task DeleteNonExistentContract_Returns404()
        {
            var token = await LoginAsync("admin@glms.com", "Admin@123");
            Assert.NotNull(token);

            var req = AuthRequest(HttpMethod.Delete, "/api/admin/contracts/99999", token!);
            var res = await _client.SendAsync(req);

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        // ── CLIENT DASHBOARD ─────────────────────────────────────────

        [Fact]
        public async Task ClientDashboard_Returns200()
        {
            var token = await LoginAsync("client@test.com", "Client@123");
            Assert.NotNull(token);

            var req = AuthRequest(HttpMethod.Get, "/api/client/dashboard", token!);
            var res = await _client.SendAsync(req);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        // ── CLIENT CONTRACTS ─────────────────────────────────────────

        [Fact]
        public async Task ClientContracts_Returns200_NotNull()
        {
            var token = await LoginAsync("client@test.com", "Client@123");
            Assert.NotNull(token);

            var req = AuthRequest(HttpMethod.Get, "/api/client/contracts", token!);
            var res = await _client.SendAsync(req);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var body = await res.Content.ReadAsStringAsync();
            Assert.NotNull(body);
        }

        // ── CLIENT SERVICE REQUESTS ──────────────────────────────────

        [Fact]
        public async Task ClientServiceRequests_Returns200_NotNull()
        {
            var token = await LoginAsync("client@test.com", "Client@123");
            Assert.NotNull(token);

            var req = AuthRequest(HttpMethod.Get, "/api/client/service-requests", token!);
            var res = await _client.SendAsync(req);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        [Fact]
        public async Task CreateServiceRequest_InvalidContract_Returns404()
        {
            var token = await LoginAsync("client@test.com", "Client@123");
            Assert.NotNull(token);

            var payload = new
            {
                contractId = 99999,
                type       = 0,
                origin     = "Cape Town",
                desc       = "Test freight",
                dest       = "Johannesburg",
                weight     = 100.0
            };

            var req = AuthRequest(HttpMethod.Post, "/api/client/service-requests", token!, payload);
            var res = await _client.SendAsync(req);

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task DeleteNonExistentClientRequest_Returns404()
        {
            var token = await LoginAsync("client@test.com", "Client@123");
            Assert.NotNull(token);

            var req = AuthRequest(HttpMethod.Delete, "/api/client/service-requests/99999", token!);
            var res = await _client.SendAsync(req);

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        // ── SWAGGER ──────────────────────────────────────────────────

        [Fact]
        public async Task SwaggerJson_Returns200_NotNull()
        {
            var res = await _client.GetAsync("/swagger/v1/swagger.json");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var body = await res.Content.ReadAsStringAsync();
            Assert.NotNull(body);
            Assert.Contains("TechMove", body);
        }
    }
}
