using EarthML.SpatialCluster.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthML.SpatialCluster
{
    public class GeoJsonVectorTilesOptions
    {
        public int MaxZoom { get; set; } = 14;
        public int IndexMaxZoom { get; set; } = 5;
        public int IndexMaxPoints { get; set; } = 100000;
        public bool SolidChildren { get; set; } = false;
        public double Tolerance { get; set; } = 3;
        public double Extent { get; set; } = 4096;
        public double Buffer { get; set; } = 64;
        public int Debug { get; set; } = 0;
        public ITileStore Tiles { get; set; }
    }
}
