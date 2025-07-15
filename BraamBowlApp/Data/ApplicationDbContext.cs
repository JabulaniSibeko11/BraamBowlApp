using BraamBowlApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BraamBowlApp.Data
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
            
        }
        public DbSet<User> users { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<Shop> Shops { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<Delivery> Deliveries { get; set; }
        public DbSet<Driver> Drivers { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure the User entity (mapped to AspNetUsers table)
            modelBuilder.Entity<User>(entity =>
            {
                // Assuming User extends IdentityUser and maps to AspNetUsers
                entity.Property(u => u.Balance)
                    .HasColumnType("decimal(18,2)")
                    .HasPrecision(18, 2);

                entity.Property(u => u.Monthly_Deposit_Total)
                    .HasColumnType("decimal(18,2)")
                    .HasPrecision(18, 2);
            });

            // Additional configurations for other entities
            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(o => o.Amount)
                    .HasColumnType("decimal(18,2)")
                    .HasPrecision(18, 2);
            });

            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.Property(oi => oi.Unit_Price_At_Time_Of_Order)
                    .HasColumnType("decimal(18,2)")
                    .HasPrecision(18, 2);
            });

            modelBuilder.Entity<MenuItem>(entity =>
            {
                entity.Property(m => m.Price)
                    .HasColumnType("decimal(18,2)")
                    .HasPrecision(18, 2);
            });
        }

    }
}
