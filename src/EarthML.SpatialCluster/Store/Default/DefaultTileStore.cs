using EarthML.SpatialCluster.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthML.SpatialCluster.Store.Default
{
    public class DefaultTileStore : ITileStore
    {
        private Dictionary<string, VectorTile> _store = new Dictionary<string, VectorTile>();
        public ICollection<VectorTileCoord> TileCoords { get; set; } = new List<VectorTileCoord>();

        public bool Contains(string id)
        {
            return _store.ContainsKey(id);
        }

        public VectorTile Get(string id)
        {
            return _store[id];
        }

        public VectorTile Set(string id, VectorTile value)
        {
            return _store[id] = value;
        }
    }
}
