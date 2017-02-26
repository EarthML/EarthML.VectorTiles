using EarthML.SpatialCluster.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthML.SpatialCluster.Store
{
    public interface ITileStore
    {
        ICollection<VectorTileCoord> TileCoords { get; }

        VectorTile Get(string id);
        VectorTile Set(string id, VectorTile value);

        bool Contains(string id);

    }
}
