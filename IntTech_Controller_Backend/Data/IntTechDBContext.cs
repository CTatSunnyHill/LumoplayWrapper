using Microsoft.EntityFrameworkCore;
using IntTech_Controller_Backend.Models;
using MongoDB.EntityFrameworkCore.Extensions;


namespace IntTech_Controller_Backend.Data
{
    public class IntTechDBContext : DbContext
    {
        public DbSet<Device> Devices { get; set; }
        public DbSet<LumoPlayGame> Games { get; set; }

        public IntTechDBContext(DbContextOptions options) : base (options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Device>().ToCollection("devices");
            modelBuilder.Entity<LumoPlayGame>().ToCollection("lumoGames");
        }

    }
}
