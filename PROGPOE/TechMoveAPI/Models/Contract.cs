using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TechMoveAPI.Interfaces;

namespace TechMoveAPI.Models
{
    public enum ContractStatus { Draft, Active, Expired, OnHold, Terminated }
    public enum ServiceLevel   { Standard, Premium, Enterprise }

    public class Contract
    {
        [Key]
        public int ContractId { get; set; }

        [Required, MaxLength(50)]
        public string ContractNumber { get; set; } = string.Empty;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public ContractStatus Status { get; set; } = ContractStatus.Draft;

        [Required]
        public ServiceLevel ServiceLevel { get; set; } = ServiceLevel.Standard;

        [MaxLength(500)]
        public string? SignedAgreementPath { get; set; }

        [Required]
        public string ClientId { get; set; } = string.Empty;

        [ForeignKey(nameof(ClientId))]
        public Client? Client { get; set; }

        public ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();

        [NotMapped]
        private readonly List<IContractObserver> _observers = new();

        public void AttachObserver(IContractObserver observer)
        {
            if (!_observers.Contains(observer))
                _observers.Add(observer);
        }

        public void ChangeStatus(ContractStatus newStatus)
        {
            var oldStatus = Status;
            Status = newStatus;
            if (oldStatus != newStatus)
            {
                foreach (var o in _observers)
                    o.OnContractStatusChanged(this);
            }
        }
    }
}
