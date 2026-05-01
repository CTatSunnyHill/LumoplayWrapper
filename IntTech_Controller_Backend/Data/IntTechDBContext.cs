using Microsoft.EntityFrameworkCore;
using IntTech_Controller_Backend.Models;
using MongoDB.EntityFrameworkCore.Extensions;


namespace IntTech_Controller_Backend.Data
{
    public class IntTechDBContext : DbContext
    {
        public DbSet<Device> Devices { get; set; }
        public DbSet<Game> Games { get; set; }
        public DbSet<Playlist> Playlists { get; set; }
        public DbSet<Projector> Projectors { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Category> Categories{ get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<Faq> Faqs { get; set; }


        public IntTechDBContext(DbContextOptions options) : base (options) 
        {
            Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Device>().ToCollection("devices");
            modelBuilder.Entity<Game>().ToCollection("games");
            modelBuilder.Entity<Playlist>().ToCollection("playlists");
            modelBuilder.Entity<Projector>().ToCollection("projectors");
            modelBuilder.Entity<User>().ToCollection("users");
            modelBuilder.Entity<Location>().ToCollection("locations");
            modelBuilder.Entity<Category>().ToCollection("categories");
            modelBuilder.Entity<Tag>().ToCollection("tags");
            modelBuilder.Entity<Faq>().ToCollection("faqs");
        }

    }
}
