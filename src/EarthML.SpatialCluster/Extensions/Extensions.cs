using EarthML.SpatialCluster.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthML.SpatialCluster.Extensions
{
    public static class Extensions
    {
        public static bool HasAny(this List<VectorTileFeature> features)
        {
            if (features == null)
                return false;
            return features.Any();
        }
        public static bool NotNull(this VectorTile tile)
        {
            if (tile == null)
                return false;
            return true;
        }
        public static bool IsNull(this VectorTile tile)
        {
            if (tile == null)
                return true;
            return false;
        }
        public static bool NoSource(this VectorTile tile)
        {
            return tile.IsNull() || tile.Source == null;
        }
    }
}
