using EarthML.SpatialCluster.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthML.SpatialCluster.Internal
{
    public class GeoJsonVTStackItem
    {

        public VectorTileCoord Coord { get; internal set; }
        public List<VectorTileFeature> Features { get; internal set; }

    }
}
