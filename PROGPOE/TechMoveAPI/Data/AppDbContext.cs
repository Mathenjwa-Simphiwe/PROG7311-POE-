using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TechMoveAPI.Models;

namespace TechMoveAPI.Data
{
    public class AppDbContext : IdentityDbContext<Client>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Admin>              Admins              { get; set; } = null!;
        public DbSet<Client>             Clients             { get; set; } = null!;
        public DbSet<Contract>           Contracts           { get; set; } = null!;
        public DbSet<ServiceRequest>     ServiceRequests     { get; set; } = null!;
        public DbSet<FreightRequest>     FreightRequests     { get; set; } = null!;
        public DbSet<MaintenanceRequest> MaintenanceRequests { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Admin>().ToTable("Admins");

            builder.Entity<ServiceRequest>()
                .HasDiscriminator<ServiceRequestType>("RequestType")
                .HasValue<FreightRequest>(ServiceRequestType.Freight)
                .HasValue<MaintenanceRequest>(ServiceRequestType.Maintenance);

            builder.Entity<Contract>()
                .HasOne(c => c.Client)
                .WithMany(c => c.Contracts)
                .HasForeignKey(c => c.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ServiceRequest>()
                .HasOne(sr => sr.Contract)
                .WithMany(c => c.ServiceRequests)
                .HasForeignKey(sr => sr.ContractId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
