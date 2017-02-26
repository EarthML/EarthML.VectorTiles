using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EarthML.SpatialCluster.Models;

namespace EarthML.SpatialCluster.Processing
{

    public class VectorTileFeatureClipResult
    {
        public VectorTileFeature TopLeft { get; set; }
        public VectorTileFeature BottomLeft { get; set; }
        public VectorTileFeature BottomRight { get; set; }
        public VectorTileFeature TopRight { get; set; }
    }

    /* clip features between two axis-parallel lines:
     *     |        |
     *  ___|___     |     /
     * /   |   \____|____/
     *     |        |
     */
    public class VectorTileClipper
    {
        protected static double[] IntersectX(double[] a, double[] b, double x)
        {
            return new[] { x, (x - a[0]) * (b[1] - a[1]) / (b[0] - a[0]) + a[1], 1 };
        }
        protected static double[] intersectY(double[] a, double[] b, double y)
        {
            return new[] { (y - a[1]) * (b[0] - a[0]) / (b[1] - a[1]) + a[0], y, 1 };
        }

        public VectorTileFeatureClipResult SplitByTile(VectorTileFeature feature, VectorTile tile)
        {
            var result = new VectorTileFeatureClipResult();
            var left = this.Clip(feature, tile.Z2, tile.X, tile.X + 0.5, 0, IntersectX, tile.Min[0], tile.Max[0]);
            var right = this.Clip(feature, tile.Z2, tile.X + 0.5, tile.X + 1, 0, IntersectX, tile.Min[0], tile.Max[0]);

            if (left != null)
            {
                result.TopLeft = this.Clip(left, tile.Z2, tile.Y, tile.Y + 0.5, 1, intersectY, tile.Min[1], tile.Max[1]);
                result.BottomLeft = this.Clip(left, tile.Z2, tile.Y + 0.5, tile.Y + 1, 1, intersectY, tile.Min[1], tile.Max[1]);
            }

            if (right != null)
            {

                result.TopRight = this.Clip(right, tile.Z2, tile.Y, tile.Y + 0.5, 1, intersectY, tile.Min[1], tile.Max[1]);
                result.BottomRight = this.Clip(right, tile.Z2, tile.Y + 0.5, tile.Y + 1, 1, intersectY, tile.Min[1], tile.Max[1]);
            }


            return result;
        }

        public VectorTileFeature Clip(VectorTileFeature feature, double scale, double k1, double k2, int axis, Func<double[], double[], double, double[]> intersect, double minAll, double maxAll)
        {
            k1 /= scale;
            k2 /= scale;

            if (minAll >= k1 && maxAll <= k2) return feature; // trivial accept
            else if (minAll > k2 || maxAll < k1) return null; // trivial reject

           // var geometry = feature.Geometry;
            var type = feature.Type;

            var min = feature.Min[axis];
            var max = feature.Max[axis];

            if (min >= k1 && max <= k2)
            { // trivial accept
                return feature;
            }
            else if (min > k2 || max < k1) return null; // trivial reject

            var slices = type == 1 ?
                    clipPoints(feature.GetPoints(), k1, k2, axis) :
                    clipGeometry(feature.GetRings(), k1, k2, axis, intersect, type == 3);

            if ((slices.Length == 1) ? slices[0].Count > 0 : slices.Length > 0)
            {
                // if a feature got clipped, it will likely get clipped on the next zoom level as well,
                // so there's no need to recalculate bboxes
                return new VectorTileFeature
                {
                    Geometry = slices,
                    Type = type,
                    Tags = feature.Tags,
                    Min = feature.Min,
                    Max = feature.Max
                };
            }

            return null;
        }
        public List<VectorTileFeature> Clip(List<VectorTileFeature> features, double scale, double k1, double k2, int axis, Func<double[], double[], double, double[]> intersect, double minAll, double maxAll)
        {
            k1 /= scale;
            k2 /= scale;

            if (minAll >= k1 && maxAll <= k2) return features; // trivial accept
            else if (minAll > k2 || maxAll < k1) return new List<VectorTileFeature>(); // trivial reject

            var clipped = new List<VectorTileFeature>();

            for (var i = 0; i < features.Count; i++)
            {

                var feature = features[i];
              //  var geometry = feature.Geometry;
                var type = feature.Type;
               
                var min = feature.Min[axis];
                var  max = feature.Max[axis];

                if (min >= k1 && max <= k2)
                { // trivial accept
                    clipped.Add(feature);
                    continue;
                }
                else if (min > k2 || max < k1) continue; // trivial reject

                var slices = type == 1 ?
                        clipPoints(feature.GetPoints(), k1, k2, axis) :
                        clipGeometry(feature.GetRings(), k1, k2, axis, intersect, type == 3);

                if ((slices.Length == 1) ? slices[0].Count >0 : slices.Length > 0)
                {
                    // if a feature got clipped, it will likely get clipped on the next zoom level as well,
                    // so there's no need to recalculate bboxes
                    clipped.Add( new VectorTileFeature {
                        Geometry=  slices ,
                        Type= type,
                        Tags= feature.Tags,
                        Min= feature.Min,
                        Max= feature.Max
                    });
                }
            }

            return clipped;

        }

        private VectorTileGeometry[] clipGeometry(VectorTileGeometry[] geometry, double k1, double k2, int axis, Func<double[], double[], double, double[]> intersect, bool closed)
        {
            var slices = new List<VectorTileGeometry>();

            for (var i = 0; i < geometry.Length; i++)
            {

                double? ak = null;
                double? bk = null;
                double[] b = null;
                var points = geometry[i];
                var area = points.Area;
                var dist = points.Distance;
                var len = points.Count;
                double[] a;
              //  var j;
              //  var last;

                var slice = new VectorTileGeometry();

                for (var j = 0; j < len - 1; j++)
                {
                    a = b ?? points[j];
                    b = points[j + 1];
                    ak = bk ?? a[axis];
                    bk = b[axis];

                    if (ak.Value < k1)
                    {

                        if ((bk.Value > k2))
                        { // ---|-----|-->
                            slice.Add(intersect(a, b, k1)); slice.Add(intersect(a, b, k2));
                            if (!closed) slice = NewSlice(slices, slice, area, dist);

                        }
                        else if (bk.Value >= k1) slice.Add(intersect(a, b, k1)); // ---|-->  |

                    }
                    else if (ak.Value > k2)
                    {

                        if ((bk.Value < k1))
                        { // <--|-----|---
                            slice.Add(intersect(a, b, k2)); slice.Add(intersect(a, b, k1));
                            if (!closed) slice = NewSlice(slices, slice, area, dist);

                        }
                        else if (bk.Value <= k2) slice.Add(intersect(a, b, k2)); // |  <--|---

                    }
                    else
                    {

                        slice.Add(a);

                        if (bk.Value < k1)
                        { // <--|---  |
                            slice.Add(intersect(a, b, k1));
                            if (!closed) slice = NewSlice(slices, slice, area, dist);

                        }
                        else if (bk.Value > k2)
                        { // |  ---|-->
                            slice.Add(intersect(a, b, k2));
                            if (!closed) slice = NewSlice(slices, slice, area, dist);
                        }
                        // | --> |
                    }
                }

                // add the last point
                a = points[len - 1];
                ak = a[axis];
                if (ak.Value >= k1 && ak.Value <= k2) slice.Add(a);

                // close the polygon if its endpoints are not the same after clipping
                if (slice.Any())
                {
                    var last = slice[slice.Count - 1];
                    if (closed && (slice[0][0] != last[0] || slice[0][1] != last[1])) slice.Add(slice[0]);
                    
                }

                // add the final slice
                NewSlice(slices, slice, area, dist);


            }

            return slices.ToArray();
        }

        private VectorTileGeometry NewSlice(List<VectorTileGeometry> slices, VectorTileGeometry slice, double area, double dist)
        {
            if (slice.Any())
            {
                // we don't recalculate the area/length of the unclipped geometry because the case where it goes
                // below the visibility threshold as a result of clipping is rare, so we avoid doing unnecessary work
                slice.Area = area;
                slice.Distance = dist;

                slices.Add(slice);
            }
            return new VectorTileGeometry();
        }

       

        private VectorTileGeometry[] clipPoints(VectorTileGeometry geometry, double k1, double k2, int axis)
        {
            var slice = new VectorTileGeometry();

            for (var i = 0; i < geometry.Count; i++)
            {
                var a = geometry[i];
                var ak = a[axis];

                if (ak >= k1 && ak <= k2) slice.Add(a);
            }
            return new[] { slice };
        }
    }
}
