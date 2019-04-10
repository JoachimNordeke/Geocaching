using Geocaching.Data;
using Geocaching.Data.Enitites;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Device.Location;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geocaching.Service
{
    class DbService
    {
        public Person GetPerson(int id)
        {
            using (var db = new AppDbContext())
            {
                return db.Person.First(p => p.ID == id);
            }
        }

        public async Task<List<Person>> GetPersonsWithGeocachesAsync()
        {
            using (var db = new AppDbContext())
            {
                return await db.Person.Include(p => p.Geocaches).ToListAsync();
            }
        }

        public async Task AddGeocacheAsync(Geocache geocache, int personID)
        {
            using (var db = new AppDbContext())
            {
                var person = await db.Person.FirstAsync(p => p.ID == personID);
                person.Geocaches.Add(geocache);
                await db.SaveChangesAsync();
            }
        }

        public async Task AddPersonAsync(Person person)
        {
            using (var db = new AppDbContext())
            {
                await db.AddAsync(person);
                await db.SaveChangesAsync();
            }
        }

        public int[] GetPersonFoundGeocaches(Person person)
        {
            using (var db = new AppDbContext())
            {
                return db.FoundGeocache.Where(f => f.Person == person).Include(f => f.Geocache).Select(f => f.Geocache.ID).ToArray();
            }
        }

        public void AddFoundGeocache(FoundGeocache foundGeocache)
        {
            using (var db = new AppDbContext())
            {
                db.Add(foundGeocache);
                db.SaveChanges();
            }
        }

        public void RemoveFoundGeocache(Person person, Geocache geocache)
        {
            using (var db = new AppDbContext())
            {
                db.Remove(db.FoundGeocache.Where(f => f.Person == person && f.Geocache == geocache).Single());
                db.SaveChanges();
            }
        }

        public async Task ClearDatabase()
        {
            using (var db = new AppDbContext())
            {
                await Task.Run(() =>
                {
                    db.Person.RemoveRange(db.Person);
                    db.Geocache.RemoveRange(db.Geocache);
                    db.FoundGeocache.RemoveRange(db.FoundGeocache);
                });
                await db.SaveChangesAsync();
            }
        }

        public async Task ReadFromFileToDatabaseAsync(string path)
        {
            using (var db = new AppDbContext())
            {
                string[] lines = await Task.Run(() =>
                {
                    return File.ReadLines(path).ToArray();
                });

                await ClearDatabase();

                bool AddNewPerson = true;
                Person person = null; // Set to null to be usable in the 'else if' statement below.
                Geocache geocache;
                var PersonFoundGeocaches = new Dictionary<Person, int[]>();
                var GeocacheIdFromFile = new Dictionary<int, Geocache>();

                foreach (var line in lines)
                {
                    if (line != "")
                    {
                        if (AddNewPerson)
                        {
                            string[] temp = line.Split('|').Select(l => l.Trim()).ToArray();
                            person = new Person
                            {
                                FirstName = temp[0],
                                LastName = temp[1],
                                Country = temp[2],
                                City = temp[3],
                                StreetName = temp[4],
                                StreetNumber = byte.Parse(temp[5]),
                                Coordinates = new GeoCoordinate
                                {
                                    Latitude = double.Parse(temp[6].Replace('.', ',')),
                                    Longitude = double.Parse(temp[7].Replace('.', ','))
                                }
                            };

                            await db.AddAsync(person);
                            AddNewPerson = false;
                        }
                        else if (!line.StartsWith("Found"))
                        {
                            string[] temp = line.Split('|').Select(l => l.Trim()).ToArray();
                            geocache = new Geocache
                            {
                                Person = person,
                                Coordinates = new GeoCoordinate
                                {
                                    Latitude = double.Parse(temp[1].Replace('.', ',')),
                                    Longitude = double.Parse(temp[2].Replace('.', ','))
                                },
                                Contents = temp[3],
                                Message = temp[4]
                            };
                            GeocacheIdFromFile.Add(int.Parse(temp[0]), geocache);
                            await db.AddAsync(geocache);
                        }
                        else
                        {
                            if (line.Count() > 7)
                                PersonFoundGeocaches.Add(person, line.Substring(7).Split(',').Select(l => int.Parse(l.Trim())).ToArray());

                            AddNewPerson = true;
                        }
                    }
                }

                foreach (var p in PersonFoundGeocaches)
                {
                    p.Value.ToList().ForEach(async CacheID => await db.AddAsync(new FoundGeocache { PersonID = p.Key.ID, GeocacheID = GeocacheIdFromFile[CacheID].ID }));
                }

                await db.SaveChangesAsync();
            }
        }

        public async Task SaveToFileFromDatabaseAsync(string path)
        {
            using (var db = new AppDbContext())
            {
                var lines = new List<string>();
                foreach (var p in await db.Person.Include(p => p.Geocaches).Include(p => p.FoundGeocaches).ThenInclude(p => p.Geocache).ToListAsync())
                {
                    lines.Add(string.Join(" | ", new[] { p.FirstName, p.LastName, p.Country, p.City, p.StreetName,
                    Convert.ToString(p.StreetNumber), Convert.ToString(p.Coordinates.Latitude),
                    Convert.ToString(p.Coordinates.Longitude) }));

                    foreach (var g in p.Geocaches)
                    {
                        lines.Add(string.Join(" | ", new[] { g.ID.ToString(), Convert.ToString(g.Coordinates.Latitude),
                            Convert.ToString(g.Coordinates.Longitude), g.Contents, g.Message }));
                    }

                    int[] foundGeocachesId = p.FoundGeocaches.Select(fg => fg.Geocache.ID).ToArray();

                    lines.Add("Found: " + string.Join(", ", foundGeocachesId));

                    lines.Add("");
                }

                await Task.Run(() =>
                {
                    File.WriteAllLines(path, lines);
                });
            }
        }
    }
}
