using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Device.Location;
using Microsoft.EntityFrameworkCore;

namespace Geocaching.Models
{
    class Person
    {
        [Key]
        public int ID { get; set; }
        [Required, MaxLength(50)]
        public string FirstName { get; set; }
        [Required, MaxLength(50)]
        public string LastName { get; set; }
        public GeoCoordinate Coordinates { get; set; }
        [Required, MaxLength(50)]
        public string Country { get; set; }
        [Required, MaxLength(50)]
        public string City { get; set; }
        [Required, MaxLength(50)]
        public string StreetName { get; set; }
        public byte StreetNumber { get; set; }

        public ICollection<Geocache> Geocaches { get; set; } = new List<Geocache>();
        public ICollection<FoundGeocache> FoundGeocaches { get; set; }
    }
}
