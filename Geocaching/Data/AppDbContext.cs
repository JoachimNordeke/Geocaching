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
            model.Entity<Person>().OwnsOne(p => p.Coordinates,
                    c =>
                    {
                        c.Ignore(p => p.Speed);
                        c.Ignore(p => p.Altitude);
                        c.Ignore(p => p.Course);
                        c.Ignore(p => p.HorizontalAccuracy);
                        c.Ignore(p => p.IsUnknown);
                        c.Ignore(p => p.VerticalAccuracy);

                        c.Property(p => p.Latitude)
                            .HasColumnName("Latitude")
                            .HasColumnType("float");
                        c.Property(p => p.Longitude)
                            .HasColumnName("Longitude")
                            .HasColumnType("float");
                    });

            model.Entity<Geocache>().OwnsOne(g => g.Coordinates,
                    c =>
                    {
                        c.Ignore(g => g.Speed);
                        c.Ignore(g => g.Altitude);
                        c.Ignore(g => g.Course);
                        c.Ignore(g => g.HorizontalAccuracy);
                        c.Ignore(g => g.IsUnknown);
                        c.Ignore(g => g.VerticalAccuracy);

                        c.Property(g => g.Latitude)
                            .HasColumnName("Latitude")
                            .HasColumnType("float");
                        c.Property(g => g.Longitude)
                            .HasColumnName("Longitude")
                            .HasColumnType("float");
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
