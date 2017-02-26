using EarthML.GeoJson;
using EarthML.SpatialCluster.Extensions;
using EarthML.SpatialCluster.Internal;
using EarthML.SpatialCluster.Models;
using EarthML.SpatialCluster.Processing;
using EarthML.SpatialCluster.Store;
using EarthML.SpatialCluster.Store.Default;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EarthML.SpatialCluster
{
       
    public class GeoJsonVectorTiles<T> where T : GeoJsonVectorTilesOptions, new()
    {
        private readonly ILogger Logger;

        protected VectorTileConverter Converter { get; set; }
        protected VectorTileWrapper Wrapper { get; set; }
        protected VectorTileClipper Clipper { get; set; }
        protected VectorTileTransformer Transformer { get; set; }

        public T Options { get; set; }       

        public ITileStore Tiles { get; set; }

        protected static double[] IntersectX(double[] a, double[] b, double x)
        {
            return new[] { x, (x - a[0]) * (b[1] - a[1]) / (b[0] - a[0]) + a[1], 1 };
        }
        protected static double[] intersectY(double[] a, double[] b, double y)
        {
            return new[] { (y - a[1]) * (b[0] - a[0]) / (b[1] - a[1]) + a[0], y, 1 };
        }


        public GeoJsonVectorTiles(ILoggerFactory loggerFactory, T options = null, VectorTileConverter converter = null, VectorTileWrapper wrapper = null, VectorTileClipper clipper = null, VectorTileTransformer transformer = null)
        {
            Logger = loggerFactory.CreateLogger<GeoJsonVectorTiles<T>>();
            Converter = converter ?? new VectorTileConverter();
            Clipper = clipper ?? new VectorTileClipper();
            Wrapper = wrapper ?? new VectorTileWrapper(Clipper);
            Options = options ?? new T();
            Transformer = transformer ?? new VectorTileTransformer();
            Tiles = Options.Tiles ?? new DefaultTileStore();
           
        }


        public void ProcessData(GeoJsonObject data)
        {

            Logger.LogDebug($"Preprocessing data");
            var z2 = 1 << Options.MaxZoom;//2^z
            var features = Converter.Convert(data, Options.Tolerance / (z2 * Options.Extent));
            Logger.LogDebug($"Preprocessing data end");

            //   Tiles = new Dictionary<string, VectorTile>();
            //   TileCoords = new List<VectorTileCoord>();

            features = Wrapper.Wrap(features, Options.Buffer / Options.Extent, IntersectX);

            // start slicing from the top tile down
            if (features.Count > 0) SplitTile(features, new VectorTileCoord());

        }

        public int? SplitTile(List<VectorTileFeature> startfeatures, VectorTileCoord startCoord, int? cz = null, int? cx = null, int? cy = null)
        {
            var stack = new Stack<GeoJsonVTStackItem>();
            stack.Push(new GeoJsonVTStackItem { Features = startfeatures, Coord = startCoord });


            int? solid = null;

            while (stack.Count > 0)
            {
                var item = stack.Pop();
                var features = item.Features;
                var x = item.Coord.X;
                var y = item.Coord.Y;
                var z = item.Coord.Z;

                var z2 = 1 << z;
                var id = item.Coord.ToID();
                VectorTile tile = Tiles.Contains(id) ? Tiles.Get(id) : null;



                if (tile == null)
                {
                    var tileTolerance = z == Options.MaxZoom ? 0 : Options.Tolerance / (z2 * Options.Extent);

                    tile = Tiles.Set(id, VectorTile.CreateTile(features, z2, x, y, tileTolerance, z == Options.MaxZoom));
                    Tiles.TileCoords.Add(new VectorTileCoord(z, x, y));

                }

                // save reference to original geometry in tile so that we can drill down later if we stop now
                tile.Source = features;

                // if it's the first-pass tiling
                if (!cz.HasValue)
                {
                    // stop tiling if we reached max zoom, or if the tile is too simple
                    if (z == Options.IndexMaxZoom || tile.NumPoints <= Options.IndexMaxPoints) continue;

                    // if a drilldown to a specific tile
                }
                else
                {

                    // stop tiling if we reached base zoom or our target tile zoom
                    if (z == Options.MaxZoom) continue;

                    // stop tiling if it's not an ancestor of the target tile
                    if (cz.HasValue)
                    {
                        if (z == cz.Value) continue;

                        var m = 1 << (cz.Value - z);
                        if (x != (int)Math.Floor((double)cx.Value / m) || y != (int)Math.Floor((double)cy.Value / m)) continue;
                    }
                }

                // stop tiling if the tile is solid clipped square
                if (!Options.SolidChildren && IsClippedSquare(tile, Options.Extent, Options.Buffer))
                {
                    if (cz.HasValue) solid = z; // and remember the zoom if we're drilling down
                    continue;
                }

                // if we slice further down, no need to keep source geometry
                tile.Source = null;

                //  if (debug > 1) console.time('clipping');

                // values we'll use for clipping
                var k1 = 0.5 * Options.Buffer / Options.Extent;
                var k2 = 0.5 - k1;
                var k3 = 0.5 + k1;
                var k4 = 1 + k1;
                List<VectorTileFeature> tl, bl, tr, br, left, right;

                tl = bl = tr = br = null;

                left = Clipper.Clip(features, z2, x - k1, x + k3, 0, IntersectX, tile.Min[0], tile.Max[0]);
                right = Clipper.Clip(features, z2, x + k2, x + k4, 0, IntersectX, tile.Min[0], tile.Max[0]);

                if (left.HasAny())
                {
                    tl = Clipper.Clip(left, z2, y - k1, y + k3, 1, intersectY, tile.Min[1], tile.Max[1]);
                    bl = Clipper.Clip(left, z2, y + k2, y + k4, 1, intersectY, tile.Min[1], tile.Max[1]);
                }

                if (right.HasAny())
                {
                    tr = Clipper.Clip(right, z2, y - k1, y + k3, 1, intersectY, tile.Min[1], tile.Max[1]);
                    br = Clipper.Clip(right, z2, y + k2, y + k4, 1, intersectY, tile.Min[1], tile.Max[1]);
                }

                //   if (debug > 1) console.timeEnd('clipping');

                if (tl.HasAny()) stack.Push(new GeoJsonVTStackItem { Features = tl, Coord = new VectorTileCoord(z + 1, x * 2, y * 2) });
                if (bl.HasAny()) stack.Push(new GeoJsonVTStackItem { Features = bl, Coord = new VectorTileCoord(z + 1, x * 2, y * 2 + 1) });
                if (tr.HasAny()) stack.Push(new GeoJsonVTStackItem { Features = tr, Coord = new VectorTileCoord(z + 1, x * 2 + 1, y * 2) });
                if (br.HasAny()) stack.Push(new GeoJsonVTStackItem { Features = br, Coord = new VectorTileCoord(z + 1, x * 2 + 1, y * 2 + 1) });
            }

            return solid;

        }



        public VectorTile GetTile(VectorTileCoord coord)
        {
            return GetTile(coord.Z, coord.X, coord.Y);
        }
        public VectorTile GetTile(int z, int x, int y)
        {
            var options = this.Options;
            var extent = options.Extent;
            var debug = options.Debug;

            var z2 = 1 << z;
            x = ((x % z2) + z2) % z2; // wrap tile x coordinate

            var id = VectorTileCoord.ToID(z, x, y);
            if (Tiles.Contains(id)) return Transformer.TransformTile(Tiles.Get(id), extent);

            //  if (debug > 1) console.log('drilling down to z%d-%d-%d', z, x, y);

            var z0 = z;
            var x0 = x;
            var y0 = y;
            VectorTile parent = null;

            while (parent.IsNull() && z0 > 0)
            {
                z0--;
                x0 = (int)Math.Floor(x0 / 2.0);
                y0 = (int)Math.Floor(y0 / 2.0);
                var tileId = VectorTileCoord.ToID(z0, x0, y0);
                parent = Tiles.Contains(tileId) ? Tiles.Get(tileId) : null;
            }

            if (parent.NoSource()) return null;

            // if we found a parent tile containing the original geometry, we can drill down from it
            //   if (debug > 1) console.log('found parent tile z%d-%d-%d', z0, x0, y0);

            // it parent tile is a solid clipped square, return it instead since it's identical
            if (IsClippedSquare(parent, extent, options.Buffer)) return Transformer.TransformTile(parent, extent);

            // if (debug > 1) console.time('drilling down');
            var solid = SplitTile(parent.Source, new VectorTileCoord(z0, x0, y0), z, x, y);
            //   if (debug > 1) console.timeEnd('drilling down');

            // one of the parent tiles was a solid clipped square
            if (solid.HasValue)
            {
                double m = 1 << (z - solid.Value);
                id = VectorTileCoord.ToID(solid.Value, (int)Math.Floor(x / m), (int)Math.Floor(y / m));
            }

            return Tiles.Contains(id) ? Transformer.TransformTile(this.Tiles.Get(id), extent) : null;
        }






        protected bool IsClippedSquare(VectorTile tile, double extent, double buffer)
        {
            var features = tile.Source;
            if (features.Count != 1) return false;

            var feature = features[0];
            var rings = feature.GetRings();
            if (feature.Type != 3 || rings.Length > 1) return false;

            var len = rings[0].Count;
            if (len != 5) return false;

            for (var i = 0; i < len; i++)
            {
                var p = Transformer.TransformPoint(rings[0][i], extent, tile.Z2, tile.X, tile.Y);
                if ((p[0] != -buffer && p[0] != extent + buffer) ||
                    (p[1] != -buffer && p[1] != extent + buffer)) return false;
            }

            return true;
        }


    }
}
