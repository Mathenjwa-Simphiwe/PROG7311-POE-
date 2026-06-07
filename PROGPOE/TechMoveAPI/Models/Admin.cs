using Microsoft.AspNetCore.Identity;

namespace TechMoveAPI.Models
{
    public class Admin : IdentityUser
    {
        public string FullName   { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive     { get; set; } = true;
    }
}
