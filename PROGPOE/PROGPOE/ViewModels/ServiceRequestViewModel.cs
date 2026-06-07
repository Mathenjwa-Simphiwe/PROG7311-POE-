namespace PROGPOE.ViewModels
{
    public class ServiceRequestViewModel
    {
        public int ServiceRequestId { get; set; }
        public string RequestId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? ContractNumber { get; set; }
        public string? ClientName { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal? Cost { get; set; }
        public decimal? LocalCostZar { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? AdminNotes { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? DecisionDate { get; set; }
    }
}
