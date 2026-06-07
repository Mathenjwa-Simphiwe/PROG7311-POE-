using Microsoft.AspNetCore.Identity;

namespace TechMoveAPI.Models
{
    public class Client : IdentityUser
    {
        public string FullName       { get; set; } = string.Empty;
        public string ContactDetails { get; set; } = string.Empty;
        public string Region         { get; set; } = string.Empty;
        public DateTime CreatedAt    { get; set; } = DateTime.Now;

        public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    }
}
