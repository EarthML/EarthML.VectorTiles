using System;
using System.Collections.Generic;
using EarthML.SpatialCluster.Models;
using EarthML.GeoJson;
using EarthML.GeoJson.Geometries;
using System.Threading.Tasks;

namespace EarthML.SpatialCluster.Processing
{
    public class VectorTileConverterOptions
    {
        public VectorTileSimplifier Simplifier { get; set; } = new VectorTileSimplifier();

        public Func<double[],Task<double[]>> CoordinateTransform { get; set; }
    }
    public class VectorTileConverter
    {
        protected VectorTileConverterOptions Options { get; private set; }

        protected VectorTileSimplifier Simplifier => Options.Simplifier;

        public VectorTileConverter(VectorTileConverterOptions options = null)
        {
            Options = options ?? new VectorTileConverterOptions();
        }
        public List<VectorTileFeature> Convert(GeoJsonObject data, double tolerance)
        {
            var features = new List<VectorTileFeature>();
            if (data.Type == GeoJsonObject.FeatureCollectionType)
            {
                var featureCollection = data as GeoJsonFeatureCollection;

                for (int i = 0; i < featureCollection.Features.Length; i++)
                {
                    ConvertFeature(features, featureCollection.Features[i], tolerance);
                }
            }
            else if (data.Type == GeoJsonObject.FeatureType)
            {
                ConvertFeature(features, data as GeoJsonFeature, tolerance);

            }
            else
            {
                // single geometry or a geometry collection
                ConvertFeature(features, new GeoJsonFeature { Geometry = data as GeometryObject }, tolerance);
            }

            return features;

        }



        private void ConvertFeature(List<VectorTileFeature> features, GeoJsonFeature feature, double tolerance)
        {
            if (feature.Geometry == null)
                return;

            var geom = feature.Geometry;
            var type = geom.Type;

            if (type == GeoJsonObject.GeoJsonPointType)
            {
                var point = geom as Point;
                features.Add(Create(feature.Properties, 1, new[] { new VectorTileGeometry { ProjectPoint(point.Coordinates) } }));
            }
            else if (type == GeoJsonObject.GeoJsonMultiPointType)
            {
                var multiPoint = geom as MultiPoint;
                features.Add(Create(feature.Properties, 1, new[] { Project(multiPoint.Coordinates) }));
            }
            else if (type == GeoJsonObject.GeoJsonLineStringType)
            {
                var linestring = geom as LineString;
                features.Add(Create(feature.Properties, 2, new[] { Project(linestring.Coordinates, tolerance) }));

            }
            else if (type == GeoJsonObject.GeoJsonMultiLineStringType || type == GeoJsonObject.GeoJsonPolygonType)
            {
                var coords = (geom as MultiLineStringPolygonGeometry).Coordinates;
                var rings = new List<VectorTileGeometry>();
                for (var i = 0; i < coords.Length; i++)
                {
                    rings.Add(Project(coords[i], tolerance));
                }
                features.Add(Create(feature.Properties, type == GeoJsonObject.GeoJsonPolygonType ? 3 : 2, rings.ToArray()));
            }
            else if (type == GeoJsonObject.GeoJsonMultiPolygonType)
            {
                var coords = (geom as MultiPolygon).Coordinates;
                var rings = new List<VectorTileGeometry>();
                for (var i = 0; i < coords.Length; i++)
                {
                    for (var j = 0; j < coords[i].Length; j++)
                    {
                        rings.Add(Project(coords[i][j], tolerance));
                    }
                }
                features.Add(Create(feature.Properties, 3, rings.ToArray()));

            }
            else if (type == GeoJsonObject.GeoJsonGeometryCollectionType)
            {
                var collection = geom as GeometryCollection;
                for (var i = 0; i < collection.Geometries.Length; i++)
                {
                    ConvertFeature(features, new GeoJsonFeature
                    {
                        Geometry = collection.Geometries[i],
                        Properties = feature.Properties,
                    }, tolerance);
                }

            }
            else
            {
                throw new Exception("Input data is not a valid GeoJSON object.");
            }
        }



        private VectorTileFeature Create(Dictionary<string, object> properties, int type, VectorTileGeometry[] geoJsonVTPointCollection)
        {
            var feature = new VectorTileFeature
            {
                Geometry = type == 1 ? geoJsonVTPointCollection[0] :  geoJsonVTPointCollection as object,
                Tags = properties,
                Type = type,


            };
            CalcBBox(feature);
            return feature;
        }
        private VectorTileFeature CalcBBox(VectorTileFeature feature)
        {
          //  var geometry = feature.Geome;
            var min = feature.Min;
            var max = feature.Max;

            if (feature.Type == 1)
            {
                CalcRingBBox(min, max, feature.GetPoints());
            }
            else
            {
                var geometry = feature.GetRings();
                for (var i = 0; i < geometry.Length; i++)
                    CalcRingBBox(min, max, geometry[i]);
            }

            return feature;
        }
        private void CalcRingBBox(double[] min, double[] max, VectorTileGeometry points)
        {
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                min[0] = Math.Min(p[0], min[0]);
                max[0] = Math.Max(p[0], max[0]);
                min[1] = Math.Min(p[1], min[1]);
                max[1] = Math.Max(p[1], max[1]);
            }
        }
        private VectorTileGeometry Project(double[][] lonlats, double? tolerance = null)
        {
            var projected = new VectorTileGeometry();
            for (var i = 0; i < lonlats.Length; i++)
            {
                projected.Add(ProjectPoint(lonlats[i]));
            }
            if (tolerance.HasValue && tolerance.Value > 0)
            {
                Simplifier.simplify(projected, tolerance.Value);
                calcSize(projected);
            }
            return projected;
        }
        private void calcSize(VectorTileGeometry points)
        {
            double area = 0;
            double dist = 0;
            double[] a = null;
            double[] b = null;
            for (int i = 0; i < points.Count - 1; i++)
            {
                a = b ?? points[i];
                b = points[i + 1];

                area += a[0] * b[1] - b[0] * a[1];

                // use Manhattan distance instead of Euclidian one to avoid expensive square root computation
                dist += Math.Abs(b[0] - a[0]) + Math.Abs(b[1] - a[1]);
            }
            points.Area = Math.Abs(area / 2);
            points.Distance = dist;
        }
       

        public double[] ProjectPoint(double[] p)
        {
            if (Options.CoordinateTransform != null)
            {
                return Options.CoordinateTransform(p).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            var sin = Math.Sin(p[1] * Math.PI / 180);
            var x = (p[0] / 360 + 0.5);
            var y = (0.5 - 0.25 * Math.Log((1 + sin) / (1 - sin)) / Math.PI);

            y = y < 0 ? 0 :
                y > 1 ? 1 : y;
            return new[] { x, y, 0.0 };

        }
        public double[] UnpackPoint(double[] p)
        {
            var latitude = 90 - 360 * Math.Atan(Math.Exp((p[1]-0.5) * 2 * Math.PI)) / Math.PI;
            var longitude = 360 * (p[0]-0.5);

            return new[] { longitude, latitude };
        }
    }
}
