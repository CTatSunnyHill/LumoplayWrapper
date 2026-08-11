using Microsoft.EntityFrameworkCore;
using IntTech_Controller_Backend.Models;
using MongoDB.EntityFrameworkCore.Extensions;


namespace IntTech_Controller_Backend.Data
{
    /**
     * EF Core context over the MongoDB database, exposing one DbSet per
     * collection. Transactions are disabled because the deployment targets a
     * standalone MongoDB server, which does not support them.
     */
    public class IntTechDBContext : DbContext
    {
        /** LUMOplay units the backend can drive. */
        public DbSet<Device> Devices { get; set; }
        /** The game library. */
        public DbSet<Game> Games { get; set; }
        /** User-built and default playlists. */
        public DbSet<Playlist> Playlists { get; set; }
        /** PJLink-controllable projectors. */
        public DbSet<Projector> Projectors { get; set; }
        /** Accounts that can sign in to the controller. */
        public DbSet<User> Users { get; set; }
        /** Physical rooms or sites. */
        public DbSet<Location> Locations { get; set; }
        /** Top-level groupings of tags. */
        public DbSet<Category> Categories{ get; set; }
        /** Labels applied to games. */
        public DbSet<Tag> Tags { get; set; }
        /** Help-page entries. */
        public DbSet<Faq> Faqs { get; set; }


        /**
         * Creates the context and turns off EF's automatic transactions, which
         * a standalone (non-replica-set) MongoDB server cannot honour.
         *
         * <param name="options">provider and connection options supplied by DI</param>
         */
        public IntTechDBContext(DbContextOptions options) : base (options)
        {
            Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        }

        /**
         * Binds each entity type to its MongoDB collection name.
         *
         * <param name="modelBuilder">the builder EF uses to assemble the model</param>
         */
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
