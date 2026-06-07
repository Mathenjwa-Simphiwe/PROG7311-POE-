using TechMoveAPI.Models;

namespace TechMoveAPI.Factory
{
    public class FreightRequestFactory
    {
        public ServiceRequest Create(Dictionary<string, object> d)
        {
            var r = new FreightRequest
            {
                RequestId   = $"FR-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}",
                RequestDate = DateTime.Now,
                Origin      = d["Origin"].ToString()!,
                Description = d["Description"].ToString()!,
                Destination = d["Destination"].ToString(),
                WeightKg    = System.Convert.ToDecimal(d["WeightKg"]),
                Status      = ServiceRequestStatus.Pending
            };
            r.CalculateCost();
            return r;
        }
    }

    public class MaintenanceRequestFactory
    {
        public ServiceRequest Create(Dictionary<string, object> d)
        {
            var r = new MaintenanceRequest
            {
                RequestId      = $"MT-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}",
                RequestDate    = DateTime.Now,
                Origin         = d["Origin"].ToString()!,
                Description    = d["Description"].ToString()!,
                EquipmentType  = d["EquipmentType"].ToString(),
                EstimatedHours = System.Convert.ToInt32(d["EstimatedHours"]),
                Status         = ServiceRequestStatus.Pending
            };
            r.CalculateCost();
            return r;
        }
    }
}
