using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geocaching.Data.Enitites
{
    interface ITag
    {
        int ID { get; set; }
        GeoCoordinate Coordinates { get; set; }
    }
}
