using Microsoft.EntityFrameworkCore;
using TechMoveAPI.Data;
using TechMoveAPI.Interfaces;
using TechMoveAPI.Models;
using System.Text.Json;

namespace TechMoveAPI.Services
{
    // ── Contract Service ──────────────────────────────────────────────────────
    public class ContractService
    {
        private readonly AppDbContext _db;
        private readonly Billing      _billing;
        private readonly EmailObserver _email;

        public ContractService(AppDbContext db, Billing billing, EmailObserver email)
        {
            _db      = db;
            _billing = billing;
            _email   = email;
        }

        public async Task<Contract> CreateAsync(Contract contract)
        {
            contract.ContractNumber = $"CT-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
            contract.Status = ContractStatus.Draft;

            contract.AttachObserver(_billing);
            contract.AttachObserver(_email);

            _db.Contracts.Add(contract);
            await _db.SaveChangesAsync();
            return contract;
        }

        public async Task<List<Contract>> SearchAsync(DateTime? sd, DateTime? ed, ContractStatus? st, string? clientId = null)
        {
            var q = _db.Contracts.Include(c => c.Client).AsQueryable();
            if (sd.HasValue)             q = q.Where(c => c.StartDate >= sd.Value);
            if (ed.HasValue)             q = q.Where(c => c.EndDate   <= ed.Value);
            if (st.HasValue)             q = q.Where(c => c.Status    == st.Value);
            if (!string.IsNullOrEmpty(clientId)) q = q.Where(c => c.ClientId == clientId);
            return await q.OrderByDescending(c => c.StartDate).ToListAsync();
        }
    }

    // ── Currency Converter ────────────────────────────────────────────────────
    public class CurrencyConverter : ICurrencyConverter
    {
        private readonly Dictionary<string, decimal> _rates = new()
        {
            ["USD"] = 1.00m,
            ["EUR"] = 0.92m,
            ["GBP"] = 0.79m,
            ["ZAR"] = 18.50m,
            ["JPY"] = 149.50m
        };

        public decimal Convert(decimal amount, string from, string to)
        {
            var fromRate = _rates.GetValueOrDefault(from.ToUpper(), 1.0m);
            var toRate   = _rates.GetValueOrDefault(to.ToUpper(), 1.0m);
            return Math.Round(amount / fromRate * toRate, 2);
        }

        public decimal GetExchangeRate(string from, string to)
        {
            var fromRate = _rates.GetValueOrDefault(from.ToUpper(), 1.0m);
            var toRate   = _rates.GetValueOrDefault(to.ToUpper(), 1.0m);
            return Math.Round(toRate / fromRate, 4);
        }
    }

    // ── Billing Observer ──────────────────────────────────────────────────────
    public class Billing : IContractObserver
    {
        public void OnContractStatusChanged(Contract contract)
        {
            Console.WriteLine($"[BILLING] Contract {contract.ContractNumber} → {contract.Status}");
        }
    }

    // ── Email Observer ────────────────────────────────────────────────────────
    public class EmailObserver : IContractObserver
    {
        public void OnContractStatusChanged(Contract contract)
        {
            var email = contract.Client?.Email ?? "unknown@client.com";
            Console.WriteLine($"[EMAIL] To: {email} | Contract {contract.ContractNumber}: {contract.Status}");
        }
    }

    // ── Exchange Rate Service ─────────────────────────────────────────────────
    public class ExchangeRateService
    {
        private readonly HttpClient _http;
        private readonly ILogger<ExchangeRateService> _logger;
        private const decimal FallbackUsdToZar = 18.50m;

        public ExchangeRateService(HttpClient http, ILogger<ExchangeRateService> logger)
        {
            _http   = http;
            _logger = logger;
        }

        public async Task<decimal> GetUsdToZarRateAsync()
        {
            try
            {
                var response = await _http.GetAsync("https://open.er-api.com/v6/latest/USD");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("rates", out var rates) &&
                    rates.TryGetProperty("ZAR", out var zarProp))
                {
                    var rate = zarProp.GetDecimal();
                    _logger.LogInformation("Live USD/ZAR rate: {Rate}", rate);
                    return rate;
                }

                return FallbackUsdToZar;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Currency API failed: {Msg}. Using fallback {Rate}.", ex.Message, FallbackUsdToZar);
                return FallbackUsdToZar;
            }
        }

        public async Task<(decimal zarAmount, decimal rateUsed)> ConvertUsdToZarAsync(decimal usdAmount)
        {
            var rate = await GetUsdToZarRateAsync();
            return (Math.Round(usdAmount * rate, 2), rate);
        }
    }

    // ── File Storage Service ──────────────────────────────────────────────────
    public class FileStorageService
    {
        private readonly string _uploadPath;

        public FileStorageService(IWebHostEnvironment env)
        {
            _uploadPath = Path.Combine(env.ContentRootPath, "Uploads", "Agreements");
            Directory.CreateDirectory(_uploadPath);
        }

        public async Task<string> SaveFileAsync(int contractId, IFormFile file)
        {
            if (file == null || file.Length == 0)   throw new ArgumentException("No file provided.");
            if (file.ContentType != "application/pdf") throw new ArgumentException("Only PDF files are accepted.");
            if (file.Length > 10_485_760)            throw new ArgumentException("File exceeds 10 MB limit.");

            var fileName = $"Contract_{contractId}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            var filePath = Path.Combine(_uploadPath, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/Uploads/Agreements/{fileName}";
        }

        public FileStream? GetFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var fp = Path.Combine(Directory.GetCurrentDirectory(), path.TrimStart('/'));
            return File.Exists(fp) ? new FileStream(fp, FileMode.Open, FileAccess.Read) : null;
        }
    }
}
