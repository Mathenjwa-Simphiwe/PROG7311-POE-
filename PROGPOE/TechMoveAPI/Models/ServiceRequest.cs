using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TechMoveAPI.Models
{
    public enum ServiceRequestStatus { Pending, Approved, InProgress, Completed, Denied }
    public enum ServiceRequestType   { Freight, Maintenance }

    public abstract class ServiceRequest
    {
        [Key]
        public int ServiceRequestId { get; set; }

        [Required, MaxLength(50)]
        public string RequestId { get; set; } = string.Empty;

        [Required]
        public DateTime RequestDate { get; set; } = DateTime.Now;

        [Required, MaxLength(200)]
        public string Origin { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required, Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; protected set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? UsdAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? LocalCostZar { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal? ExchangeRateUsed { get; set; }

        [Required]
        public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.Pending;

        [Required]
        public ServiceRequestType RequestType { get; protected set; }

        public int ContractId { get; set; }

        [ForeignKey(nameof(ContractId))]
        public Contract? Contract { get; set; }

        public string? AdminNotes   { get; set; }
        public DateTime? DecisionDate { get; set; }

        public abstract void CalculateCost();
    }

    public class FreightRequest : ServiceRequest
    {
        [MaxLength(200)] public string? Destination   { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal WeightKg { get; set; }
        [MaxLength(50)]  public string? TrackingNumber { get; set; }

        public FreightRequest() => RequestType = ServiceRequestType.Freight;

        public override void CalculateCost() => Cost = WeightKg * 5.50m;
    }

    public class MaintenanceRequest : ServiceRequest
    {
        [MaxLength(100)] public string? EquipmentType   { get; set; }
        public int EstimatedHours { get; set; }
        [MaxLength(100)] public string? TechnicianName  { get; set; }
        public DateTime? ScheduledDate { get; set; }

        public MaintenanceRequest() => RequestType = ServiceRequestType.Maintenance;

        public override void CalculateCost() => Cost = EstimatedHours * 120m;
    }
}
