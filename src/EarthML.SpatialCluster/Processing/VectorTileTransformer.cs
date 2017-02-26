using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EarthML.SpatialCluster.Models;

namespace EarthML.SpatialCluster.Processing
{
    public class VectorTileTransformer
    {
        public double[] TransformPoint(double[] p, double extent, int z2, int tx, int ty)
        {
            var x = Math.Round(extent * (p[0] * z2 - tx));
            var y = Math.Round(extent * (p[1] * z2 - ty));
           if(x<0 ||y < 0)
            {

            }
            return new[] { x, y };
        }

        public VectorTile TransformTile(VectorTile tile, double extent)
        {
            if (tile.Transformed) return tile;

            var z2 = tile.Z2;
            var tx = tile.X;
            var ty = tile.Y;
          

            for (var i = 0; i < tile.Features.Count; i++)
            {
                var feature = tile.Features[i];
               
                var type = feature.Type;

                if (type == 1)
                {
                    var geom = feature.GetPoints();
                    for (var j = 0; j < geom.Count; j++) geom[j] = TransformPoint(geom[j], extent, z2, tx, ty);

                }
                else
                {
                    var geom = feature.GetRings();
                    for (var j = 0; j < geom.Length; j++)
                    {
                        var ring = geom[j];
                        for (var k = 0; k < ring.Count; k++) ring[k] = TransformPoint(ring[k], extent, z2, tx, ty);
                    }
                }
            }

            tile.Transformed = true;

            return tile;
        }
    }
}
