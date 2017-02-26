using EarthML.GeoJson;
using EarthML.GeoJson.Converters;
using EarthML.SpatialCluster;
using EarthML.SpatialCluster.Models;
using EarthML.SpatialCluster.Processing;
using Microsoft.AspNetCore.NodeServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EarthML.SpatialClustor.GeoJSONPointConverter
{

    public class ClusterId
    {
        public double X { get; set; }
        public double Y { get; set; }

        public List<VectorTileFeature> Features { get; set; } = new List<VectorTileFeature>();
    }
    public class NodeBuffer
    {
        public byte[] Data { get; set; }

    }
    class Program
    {
        private static readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private static readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        static void Main(string[] args)
        {

            try
            {

                RunAsync(args,
                    cancellationTokenSource.Token).Wait();

            }
            finally
            {
                runCompleteEvent.Set();
            }

        }

        private static async Task RunAsync(string[] args, CancellationToken cannelcationtoken)
        {
            var services = new ServiceCollection();

            services.AddSingleton<ILoggerFactory>(new LoggerFactory().AddConsole(LogLevel.Trace));
            services.AddTransient<GeoJsonVectorTiles<GeoJsonVectorTilesOptions>>();


            services.AddNodeServices((o) =>
            {

                o.ProjectPath = Directory.GetCurrentDirectory(); // PlatformServices.Default.Application.ApplicationBasePath + "/../../..";
            });





            var serviceProvider = services.BuildServiceProvider();
            var node = serviceProvider.GetService<INodeServices>();
            var logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger<Program>();


            var proj4Str = args[Array.IndexOf(args, "--proj") + 1];
            var extent = args.Skip(Array.IndexOf(args, "--extent") + 1).Take(4).Select(double.Parse).ToArray();

            var maxZoom = 0;
            var max = (extent[2] - extent[0]) / 4096.0;
            while (max > 0.01)
            {
                max /= 2;
                maxZoom++;
            }

            maxZoom = 22;

            var tileConverter = new VectorTileConverter(new VectorTileConverterOptions
            {
                CoordinateTransform = async (p) =>
                {
                    var local = await node.InvokeAsync<double[]>("./proj4", "EPSG:4326", proj4Str, p);

                    return new double[] { (local[0] - extent[0]) / (extent[2] - extent[0]), -(local[1] - extent[3]) / (extent[3] - extent[1]), 0 };
                }
            });
            services.AddSingleton(new GeoJsonVectorTilesOptions() { MaxZoom = maxZoom });
            services.AddSingleton(tileConverter);
            serviceProvider = services.BuildServiceProvider();


            var points = File.ReadAllText(args[0]);

            var featureCollection = JsonConvert.DeserializeObject<GeoJsonObject>(points, new GeoJsonObjectConverter()) as GeoJsonFeatureCollection;
            

            var processor = serviceProvider.GetRequiredService<GeoJsonVectorTiles<GeoJsonVectorTilesOptions>>();

            processor.ProcessData(featureCollection);


            var q = new Queue<VectorTileCoord>(); q.Enqueue(new VectorTileCoord(0, 0, 0));

            while (q.Any())
            {
                var tileCoord = q.Dequeue();
                var tile = processor.GetTile(tileCoord.Z, tileCoord.X, tileCoord.Y);

                if (tile != null && tileCoord.Z < maxZoom && tile.Features.Count > 10)
                {
                    logger.LogInformation("[{tileZ},{tileX},{tileY}] : Finding clusters for {featureCount}",
                        tileCoord.Z, tileCoord.X, tileCoord.Y, tile.Features.Count);


                    List<ClusterId> centers = GetCenters(tile);
                    logger.LogInformation("[{tileZ},{tileX},{tileY}] : {clusterCount}|{singlePointCount}  clusters found for {featureCount} features",
                        tileCoord.Z, tileCoord.X, tileCoord.Y, centers.Count, centers.Count(c => c.Features.Count == 1), tile.Features.Count);

                    if (centers.Any() && centers.Count < tile.Features.Count)
                    {
                        foreach (var child in tileCoord.GetChildCoordinate())
                        {
                            q.Enqueue(child);
                        }
                    }



                    tile.Features = new List<VectorTileFeature>();
                    foreach (var center in centers)
                    {
                        if (center.Features.Count == 1)
                        {
                            tile.Features.AddRange(center.Features);
                        }
                        else
                        {
                            tile.Features.Add(new VectorTileFeature
                            {
                                Type = 1,
                                Geometry = new VectorTileGeometry() { new[] {
                                     Math.Round(center.Features.Average(f=>f.GetPoints().First()[0]))  ,
                                     Math.Round(center.Features.Average(f => f.GetPoints().First()[1])) } }  ,
                                Tags = new Dictionary<string, object>
                                 {
                                     { "clusterId", Guid.NewGuid().ToString() },
                                     {"count", center.Features.Count }
                                 }
                            });
                        }

                    }
                }

              //  tile = tile ?? new VectorTile { X = tileCoord.X, Y = tileCoord.Y, Z2 = 1 << tileCoord.Z, Features = new List<VectorTileFeature>(), Transformed = true };

                if (tile != null)
                {
                    logger.LogInformation("[{tileZ},{tileX},{tileY}] : Writing tile with {featureCount}",
                        tileCoord.Z, tileCoord.X, tileCoord.Y, tile.Features.Count);

                    var file = $"{args[1]}/{tileCoord.Z}/{tileCoord.X}/{tileCoord.Y}.vector.pbf";
                    Directory.CreateDirectory(Path.GetDirectoryName(file));

                    var stream = await node.InvokeAsync<NodeBuffer>("./topbf",
                        new VectorTile { Features = tile.Features, X = tile.X, Y = tile.Y, Z2 = tile.Z2, Transformed = tile.Transformed }, file);



                    // File.WriteAllBytes(file+".pbf", stream.Data);


                   // return;


                }
            }

        }

        private static List<ClusterId> GetCenters(VectorTile tile)
        {


            var centers = new List<ClusterId>();


            foreach (var feature in tile.Features)
            {
                var k = feature.GetPoints().Single();
                var x = k[0];
                var y = k[1];
                var distance = double.MaxValue;
                var ii = 0;
                for (var i = 0; i < centers.Count; i++)
                {

                    var d = Math.Sqrt((x - centers[i].X) * (x - centers[i].X) + (y - centers[i].Y) * (y - centers[i].Y));
                    if (d < distance)
                    {
                        distance = d;
                        ii = i;
                    }
                }
                if (distance > 512)
                {
                    centers.Add(new ClusterId() { X = x, Y = y, Features = new List<VectorTileFeature>() { feature } });
                }
                else
                {
                    centers[ii].Features.Add(feature);
                }
            }

            return centers;
        }
    }
}