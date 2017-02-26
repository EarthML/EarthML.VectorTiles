using System;
using System.Collections.Generic;
using System.Linq;
using EarthML.SpatialCluster.Models;

namespace EarthML.SpatialCluster.Processing
{
    public class VectorTileWrapper
    {
        protected VectorTileClipper Clipper { get; set; }
        public VectorTileWrapper(VectorTileClipper clipper = null)
        {
            Clipper = clipper ?? new VectorTileClipper();
        }

        /// <summary>
        /// Wrap features as per original GeoJSONVT.
        /// </summary>
        /// <param name="features"></param>
        /// <param name="buffer"></param>
        /// <param name="intersectX"></param>
        /// <returns></returns>
        public List<VectorTileFeature> Wrap(List<VectorTileFeature> features, double buffer, Func<double[], double[], double, double[]> intersectX)
        {
            var merged = features;
            var left = Clipper.Clip(features, 1, -1 - buffer, buffer, 0, intersectX, -1, 2);//Left world copy;
            var right = Clipper.Clip(features, 1, 1 - buffer, 2 + buffer, 0, intersectX, -1, 2); //Right world copy;

            if (left.Any() || right.Any())
            {
                merged = Clipper.Clip(features, 1, -buffer, 1 + buffer, 0, intersectX, -1, 2);//Center world copy;

                if (left.Any()) merged = ShiftFeatureCoords(left, 1).Concat(merged).ToList(); //merge left into center;
                if (right.Any()) merged = merged.Concat(ShiftFeatureCoords(right, -1)).ToList(); //merge right into center

            }

            return merged;


        }
        public IEnumerable<VectorTileFeature> Wrap(VectorTileFeature feature, double buffer, Func<double[], double[], double, double[]> intersectX)
        {
            //var merged = new List<VectorTileFeature> { feature };
            var left = Clipper.Clip(feature, 1, -1 - buffer, buffer, 0, intersectX, -1, 2);//Left world copy;
            var right = Clipper.Clip(feature, 1, 1 - buffer, 2 + buffer, 0, intersectX, -1, 2); //Right world copy;
            var hasLeft = left != null;
            var hasRight = right != null;

            if (hasLeft || hasRight)
            {
                var center = Clipper.Clip(feature, 1, -buffer, 1 + buffer, 0, intersectX, -1, 2);//Center world copy;
                if (center != null)
                    yield return center;

                if (hasLeft) yield return ShiftFeatureCoords(left, 1);
                if (hasRight) yield return ShiftFeatureCoords(right, -1);


            }
            else
            {
                yield return feature;
            }


        }
        private VectorTileFeature ShiftFeatureCoords(VectorTileFeature feature, double offset)
        {
            var newFeatures = new List<VectorTileFeature>();


            var type = feature.Type;

            if (type == 1)
            {
                return new VectorTileFeature
                {
                    Geometry = ShiftCoords(feature.GetPoints(), offset),
                    Type = type,
                    Tags = feature.Tags,
                    Min = new[] { feature.Min[0] + offset, feature.Min[1] },
                    Max = new[] { feature.Max[0] + offset, feature.Max[1] }
                };
            }

            var newGeometry = new List<VectorTileGeometry>();
            var rings = feature.GetRings();
            for (var j = 0; j < rings.Length; j++)
            {
                newGeometry.Add(ShiftCoords(rings[j], offset));
            }

            return new VectorTileFeature
            {
                Geometry = newGeometry.ToArray(),
                Type = type,
                Tags = feature.Tags,
                Min = new[] { feature.Min[0] + offset, feature.Min[1] },
                Max = new[] { feature.Max[0] + offset, feature.Max[1] }
            };

        }
        private List<VectorTileFeature> ShiftFeatureCoords(List<VectorTileFeature> features, double offset)
        {
            return features.Select(feature => ShiftFeatureCoords(feature, offset)).ToList();
            //var newFeatures = new List<VectorTileFeature>();

            //for (var i = 0; i < features.Count; i++)
            //{
            //    var feature = features[i];
               
            //    var type = feature.Type;


            //    var newGeometry = new List<VectorTileGeometry>();
            //    var rings = feature.GetPointsGeometry();
            //    for (var j = 0; j < rings.Length; j++)
            //    {
            //        newGeometry.Add(ShiftCoords(rings[j], offset));
            //    }


            //    newFeatures.Add(new VectorTileFeature
            //    {
            //        Geometry = newGeometry.ToArray(),
            //        Type = type,
            //        Tags = feature.Tags,
            //        Min = new[] { feature.Min[0] + offset, feature.Min[1] },
            //        Max = new[] { feature.Max[0] + offset, feature.Max[1] }
            //    });
            //}

            //return newFeatures;
        }

        private VectorTileGeometry ShiftCoords(VectorTileGeometry points, double offset)
        {
            var newPoints = new VectorTileGeometry()
            {
                Area = points.Area,
                Distance = points.Distance
            };
            for (var i = 0; i < points.Count; i++)
            {
                newPoints.Add(new[] { points[i][0] + offset, points[i][1], points[i][2] });
            }

            return newPoints;
        }
    }
}
