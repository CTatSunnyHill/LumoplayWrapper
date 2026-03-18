using Microsoft.EntityFrameworkCore;
using IntTech_Controller_Backend.Models;
using MongoDB.EntityFrameworkCore.Extensions;


namespace IntTech_Controller_Backend.Data
{
    public class IntTechDBContext : DbContext
    {
        public DbSet<Device> Devices { get; set; }
        public DbSet<LumoPlayGame> Games { get; set; }
        public DbSet<LumoPlayPlaylist> Playlists { get; set; }
        public DbSet<Projector> Projectors { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Location> Locations { get; set; }


        public IntTechDBContext(DbContextOptions options) : base (options) 
        {
            Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Device>().ToCollection("devices");
            modelBuilder.Entity<LumoPlayGame>().ToCollection("lumoGames");
            modelBuilder.Entity<LumoPlayPlaylist>().ToCollection("lumoPlaylists");
            modelBuilder.Entity<Projector>().ToCollection("projectors");
            modelBuilder.Entity<User>().ToCollection("users");
            modelBuilder.Entity<Location>().ToCollection("locations");
        }

    }
}
