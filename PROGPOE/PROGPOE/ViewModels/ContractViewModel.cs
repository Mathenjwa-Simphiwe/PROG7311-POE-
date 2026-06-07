namespace PROGPOE.ViewModels
{
    public class ContractViewModel
    {
        public int ContractId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ServiceLevel { get; set; } = string.Empty;
        public string? ClientName { get; set; }
        public string? ClientId { get; set; }
        public bool HasAgreement { get; set; }
    }
