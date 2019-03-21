using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Device.Location;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geocaching.Models
{
    class Geocache
    {
        [Key]
        public int ID { get; set; }
        public Person Person { get; set; }
        public GeoCoordinate Coordinates { get; set; }
        [Required, MaxLength(255)]
        public string Contents { get; set; }
        [Required, MaxLength(255)]
        public string Message { get; set; }
    }
}
