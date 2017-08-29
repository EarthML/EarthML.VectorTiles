using EarthML.GeoJson;
using EarthML.GeoJson.Converters;
using EarthML.SpatialCluster;
using EarthML.SpatialCluster.Models;
using EarthML.SpatialCluster.Processing;
using Loyc.Collections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.NodeServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.StaticFiles;
using EarthML.GeoJson.Geometries;
using EarthML.SpatialCluster.Algorithms.Optics;

namespace EarthML.GeoJSON2VectorTiles
{

    public class NodeBuffer
    {
        public byte[] Data { get; set; }

    }
    public static class t
    {
        public static double[] Sub(this double[] U, double[] V)
        {
            return new double[] { U[0] - V[0], U[1] - V[1] };
        }
        public static int[] Sub(this int[] U, int[] V)
        {
            return new int[] { U[0] - V[0], U[1] - V[1] };
        }
        public static double Cross(this double[] U, double[] V)
        {
            return U[0] * V[1] - U[1] * V[0];
        }
        public static int Cross(this int[] U, int[] V)
        {
            return U[0] * V[1] - U[1] * V[0];
        }

        /// <summary>Computes the convex hull of a polygon, in clockwise order in a Y-up 
        /// coordinate system (counterclockwise in a Y-down coordinate system).</summary>
        /// <remarks>Uses the Monotone Chain algorithm, a.k.a. Andrew's Algorithm.</remarks>
        public static IListSource<double[]> ComputeConvexHull(IEnumerable<double[]> points)
        {
            var list = new List<double[]>(points);
            return ComputeConvexHull(list, true);
        }
        public static IListSource<double[]> ComputeConvexHull(List<double[]> points, bool sortInPlace = false)
        {
            if (!sortInPlace)
                points = new List<double[]>(points);
            points.Sort((a, b) =>
              a[0] == b[0] ? a[1].CompareTo(b[1]) : (a[0] > b[0] ? 1 : -1));

            // Importantly, DList provides O(1) insertion at beginning and end
            DList<double[]> hull = new DList<double[]>();
            int L = 0, U = 0; // size of lower and upper hulls

            // Builds a hull such that the output polygon starts at the leftmost point.
            for (int i = points.Count - 1; i >= 0; i--)
            {
                double[] p = points[i], p1;

                // build lower hull (at end of output list)
                while (L >= 2 && (p1 = hull.Last).Sub(hull[hull.Count - 2]).Cross(p.Sub(p1)) >= 0)
                {
                    hull.RemoveAt(hull.Count - 1);
                    L--;
                }
                hull.PushLast(p);
                L++;

                // build upper hull (at beginning of output list)
                while (U >= 2 && (p1 = hull.First).Sub(hull[1]).Cross(p.Sub(p1)) <= 0)
                {
                    hull.RemoveAt(0);
                    U--;
                }
                if (U != 0) // when U=0, share the point added above
                    hull.PushFirst(p);
                U++;
                Debug.Assert(U + L == hull.Count + 1);
            }
            hull.RemoveAt(hull.Count - 1);
            return hull;
        }
    }
 
    class Program
    {
        static async Task Main(string[] args)
        {




            var host = new WebHostBuilder()
              .UseKestrel()
              .ConfigureServices((ctx,appservices) =>
              {
                  appservices.AddMvc();
                 
              })
              .Configure(app =>
              {
                  app.UseDeveloperExceptionPage();

                  //app.UseDirectoryBrowser(new DirectoryBrowserOptions
                  //{
                  //    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "tmp")),

                  //});
                  app.UseStaticFiles(new StaticFileOptions
                  {
                      FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "tmp")),
                      ServeUnknownFileTypes = true,
                      ContentTypeProvider = new FileExtensionContentTypeProvider(new Dictionary<string, string> { { ".vector.pdf", "vectorfile" } })
                  });
                 
                  app.UseMvc();
                  
                
              })
              .Build();

            await host.RunAsync();

            var services = new ServiceCollection();

            services.AddSingleton(new LoggerFactory().AddConsole(LogLevel.Trace));
            services.AddTransient<GeoJsonVectorTiles<GeoJsonVectorTilesOptions>>();


            services.AddNodeServices((o) =>
            {

                o.ProjectPath = Directory.GetCurrentDirectory(); // PlatformServices.Default.Application.ApplicationBasePath + "/../../..";
            });



            var maxZoom = 6;

            var serviceProvider = services.BuildServiceProvider();
            var node = serviceProvider.GetService<INodeServices>();
            var logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger<Program>();

            var tileConverter = new VectorTileConverter();
            services.AddSingleton(new GeoJsonVectorTilesOptions() { MaxZoom = maxZoom ,  Buffer = 256});
            services.AddSingleton(tileConverter);
            serviceProvider = services.BuildServiceProvider();

          

            var points = File.ReadAllText(args[0]);

            var featureCollection = JsonConvert.DeserializeObject<GeoJsonObject>(points, new GeoJsonObjectConverter()) as GeoJsonFeatureCollection;

        //    featureCollection.Features = featureCollection.Features.Where(f => f.Geometry is Polygon).ToArray();
            foreach(var feature in featureCollection.Features)
            {
                feature.Properties = new Dictionary<string, object>
                {
                    { "tileId", Regex.Replace(feature.Properties["_font_COLOR_000000_TILE_ID_font"] as string, "<[^>]+>", string.Empty)}
                };
            }



            
           // var pointsList = new PointsList();
           // var features = featureCollection.Features.Where(f => f.Geometry is Point).ToArray();
           // for (uint i = 0; i < features.Length; i++)
           // {
           //     pointsList.AddPoint(i, (features[i].Geometry as Point).Coordinates);
           // }

           // double maxRadius = 0.1;
           // int minPoints = features.Length / 25;
           // var optics = new OPTICS(maxRadius, minPoints, pointsList);

           // optics.BuildReachability();

           // var reachablity = optics.ReachabilityPoints().GroupBy(k => k.Reachability);
           //var featuresList = new List<GeoJsonFeature>();
           // foreach (var item in reachablity)
           // {
           //     Console.WriteLine($"{item.Key}: {item.Count()} - {string.Join(",", item.Select(k => k.PointId))}");

           //     var hull = t.ComputeConvexHull(item.Select(k => features[k.PointId]).Select(k => (k.Geometry as Point).Coordinates));


           //     featuresList.Add(new GeoJsonFeature
           //     {
           //         //Type = 3,
           //         Geometry = new Polygon { Coordinates = new double[][][] { hull.ToArray() } },
           //         Properties = new Dictionary<string, object>
           //                      {
           //                          { "tileId", Math.Round( item.Key,2)},
           //                          {"count", item.Count() }
           //                      }
           //     });

           //     // Console.WriteLine(item.PointId + ";" + item.Reachability);
           // }
           // featureCollection.Features = featuresList.ToArray();


           //var clusters = featureCollection.Features.Where(f => f.Geometry is Polygon).GroupBy(f => (f.Properties["tileId"] as string).Substring(0, 3));
           //var fl = new List<GeoJsonFeature>();
           //foreach (var cluster in clusters)
           //{
           //    var b = cluster.SelectMany(c => (c.Geometry as Polygon).Coordinates[0]);
           //    var meanX = b.Select(b1 => b1[0]).Average();
           //    var meanY = b.Select(b1 => b1[1]).Average();
           //    fl.Add(new GeoJsonFeature {
           //        Geometry = new Point { Coordinates= new double[] { meanX,meanY} } ,
           //        Properties = new Dictionary<string, object> { { "tileId", cluster.Key },{ "label",true } } } );
           //}

           //    featureCollection.Features = featureCollection.Features.Concat(fl).ToArray();

           var processor = serviceProvider.GetRequiredService<GeoJsonVectorTiles<GeoJsonVectorTilesOptions>>();

            processor.ProcessData(featureCollection);


            var q = new Queue<VectorTileCoord>(); q.Enqueue(new VectorTileCoord(0, 0, 0));

            while (q.Any())
            {
                var tileCoord = q.Dequeue();
                var tile = processor.GetTile(tileCoord.Z, tileCoord.X, tileCoord.Y);
                 
                if (tile != null)
                {
                    if (tileCoord.Z < maxZoom)
                    {
                        if (tile.Features.Count > 0)
                        {
                            foreach (var child in tileCoord.GetChildCoordinate())
                            {
                                q.Enqueue(child);
                            }
                        }
                    }

                    if (tile.Features.Count > 25)
                    {
                        
                    }
                 //   var groups = tile.Features.Where(f=>f.Type == 3).GroupBy(f => (f.Tags["tileId"] as string).Substring(0, 3));



                    //if (tile.NumPoints > 512)
                    //{

                    //    tile.Features = tile.Features.Where(f => f.Type == 1 && f.Tags.ContainsKey("label")).ToList();

                    //    foreach (var group in groups)
                    //    {

                    //        var hull = t.ComputeConvexHull(group.SelectMany(k => k.GetRings().SelectMany(m => m)));


                    //        tile.Features.Add(new VectorTileFeature
                    //        {
                    //            Type = 3,
                    //            Geometry = new[] { new VectorTileGeometry(hull) },
                    //            Tags = new Dictionary<string, object>
                    //             {
                    //                 { "tileId", group.Key},
                    //                 {"count", group.Count() }
                    //             }
                    //        });
                    //    }

                    //}


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
    }
}
