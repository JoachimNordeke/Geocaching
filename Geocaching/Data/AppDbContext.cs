using Geocaching.Models;
using Microsoft.EntityFrameworkCore;

namespace Geocaching.Data
{
    class AppDbContext : DbContext
    {
        public DbSet<Person> Person { get; set; }
        public DbSet<FoundGeocache> FoundGeocache { get; set; }
        public DbSet<Geocache> Geocache { get; set; }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<Person>(person =>
            {
                person.Property(p => p.Latitude).HasColumnType("float");
                person.Property(p => p.Longitude).HasColumnType("float");
            });

            model.Entity<Geocache>(cache =>
            {
                cache.Property(c => c.Latitude).HasColumnType("float");
                cache.Property(c => c.Longitude).HasColumnType("float");
            });

            model.Entity<FoundGeocache>().HasKey(fg => new { fg.PersonID, fg.GeocacheID });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // Jockes SQL-string
            //options.UseSqlServer(@"Data Source=JOCKES;Initial Catalog=Geocaching;Integrated Security=True");
            //Ghassans SQL-string
            options.UseSqlServer(@"Data Source=(local)\SQLEXPRESS;Initial Catalog=Geocaching;Integrated Security=True");
        }
    }
}
