using System;
using System.Collections.Generic;
using System.Linq;

namespace EarthML.SpatialCluster.Models
{
    public class VectorTile
    {
        public List<VectorTileFeature> Features { get; set; } = new List<VectorTileFeature>();

        public int NumPoints { get; set; } = 0;
        public int NumSimplified { get; set; } = 0;


        public List<VectorTileFeature> Source { get; internal set; }

        public int Z2 { get; set; }
        public int Y { get; set; }
        public int X { get; set; }

        public int Z { get { return (int)Math.Log(Z2, 2); } }

        public bool Transformed { get; set; }


        public double[] Min { get; set; } = new double[] { 2, 1 };
        public double[] Max { get; set; } = new double[] { -1, 0 };



        public VectorTileCoord TileCoord { get { return new VectorTileCoord(Z, X, Y); } }
        public VectorTileCoord ParentTileCoord { get { return new VectorTileCoord(Z - 1, (int)Math.Floor(X / 2.0), (int)Math.Floor(Y / 2.0)); } }

        public static VectorTile CreateTile(List<VectorTileFeature> features, int z2, int tx, int ty, double tolerance, bool noSimplify)
        {
            var tile = new VectorTile();

            tile.Z2 = z2;
            tile.X = tx;
            tile.Y = ty;
            for (var i = 0; i < features.Count; i++)
            {
                tile.AddFeature(features[i], tolerance, noSimplify);

                var min = features[i].Min;
                var max = features[i].Max;

                if (min[0] < tile.Min[0]) tile.Min[0] = min[0];
                if (min[1] < tile.Min[1]) tile.Min[1] = min[1];
                if (max[0] > tile.Max[0]) tile.Max[0] = max[0];
                if (max[1] > tile.Max[1]) tile.Max[1] = max[1];
            }
            return tile;
        }
        
        public void Add(VectorTileFeature feature, double tolerance, bool noSimplify)
        {
            this.AddFeature(feature, tolerance, noSimplify);

            var min = feature.Min;
            var max = feature.Max;

            if (min[0] < this.Min[0]) this.Min[0] = min[0];
            if (min[1] < this.Min[1]) this.Min[1] = min[1];
            if (max[0] > this.Max[0]) this.Max[0] = max[0];
            if (max[1] > this.Max[1]) this.Max[1] = max[1];
        }
        private void AddFeature(VectorTileFeature feature, double tolerance, bool noSimplify)
        {
         //   var geom = feature.Geometry;
            var type = feature.Type;
            var simplified = new List<VectorTileGeometry>();

            var sqTolerance = tolerance * tolerance;
            // i, j, ring, p;

            if (type == 1)
            {
                var first = new VectorTileGeometry(); simplified.Add(first);
                var points = feature.GetPoints();
                for (var i = 0; i < points.Count; i++)
                {
                    first.Add(points[i]);
                    NumPoints++;
                    NumSimplified++;
                }

            }
            else
            {
                var geom = feature.GetRings();
                // simplify and transform projected coordinates for tile geometry
                for (var i = 0; i < geom.Length; i++)
                {
                    var ring = geom[i];

                    // filter out tiny polylines & polygons
                    if (!noSimplify && ((type == 2 && ring.Distance < tolerance) ||
                                        (type == 3 && ring.Area < sqTolerance)))
                    {
                        NumPoints += ring.Count;
                        continue;
                    }

                    var simplifiedRing = new VectorTileGeometry();

                    for (var j = 0; j < ring.Count; j++)
                    {
                        var p = ring[j];
                        // keep points with importance > tolerance
                        if (noSimplify || p[2] > sqTolerance)
                        {
                            simplifiedRing.Add(p);
                            NumSimplified++;
                        }
                        NumPoints++;
                    }

                    simplified.Add(simplifiedRing);
                }
            }

            if (simplified.Count > 0)
            {
                Features.Add(new VectorTileFeature
                {
                    Geometry = type == 1? simplified.Single() : simplified.ToArray() as object,
                    Type = type,
                    Tags = feature.Tags
                });
            }
        }
    }
}
