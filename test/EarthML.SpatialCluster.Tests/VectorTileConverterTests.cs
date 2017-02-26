using EarthML.SpatialCluster.Processing;
using Microsoft.AspNetCore.NodeServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthML.SpatialCluster.Tests
{

    [TestClass]
    public class VectorTileConerterTests
    {
        private TestContext testContextInstance;
        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        [TestMethod]
        public void TestLowerLeftLatLng()
        {
            var tileConverter = new VectorTileConverter();

            var p1 = new[] { -180.0, -90.0 };
            var p2 = tileConverter.ProjectPoint(p1);

            Assert.AreEqual(0, p2[0], $"Longtitude {nameof(TestLowerLeftLatLng)} Test expected to be 0");
            Assert.AreEqual(1, p2[1], $"Latitude {nameof(TestLowerLeftLatLng)} expected to be 1");
        }

        [TestMethod]
        public void TestTopLeftLatLng()
        {
            var tileConverter = new VectorTileConverter();

            var p1 = new[] { -180.0, 90.0 };
            var p2 = tileConverter.ProjectPoint(p1);

            Assert.AreEqual(0, p2[0], $"Longtitude {nameof(TestTopLeftLatLng)} Test expected to be 0");
            Assert.AreEqual(0, p2[1], $"Latitude {nameof(TestTopLeftLatLng)} expected to be 0");
        }


        [TestMethod]
        public void TestTopRightLatLng()
        {
            var tileConverter = new VectorTileConverter();

            var p1 = new[] { 180.0, 90.0 };
            var p2 = tileConverter.ProjectPoint(p1);

            Assert.AreEqual(1, p2[0], $"Longtitude {nameof(TestTopRightLatLng)} Test expected to be 1");
            Assert.AreEqual(0, p2[1], $"Latitude {nameof(TestTopRightLatLng)} expected to be 0");
        }

        [TestMethod]
        public void TestBottomRightLatLng()
        {
            var tileConverter = new VectorTileConverter();

            var p1 = new[] { 180.0, -90.0 };
            var p2 = tileConverter.ProjectPoint(p1);

            Assert.AreEqual(1, p2[0], $"Longtitude {nameof(TestBottomRightLatLng)} Test expected to be 1");
            Assert.AreEqual(1, p2[1], $"Latitude {nameof(TestBottomRightLatLng)} expected to be 1");
        }


        [TestMethod]
        public void TestCustemProjection()
        {

            var services = new ServiceCollection();

            services.AddSingleton<ILoggerFactory>(new LoggerFactory().AddConsole(LogLevel.Trace));

            services.AddNodeServices((o) =>
            {

                o.ProjectPath = PlatformServices.Default.Application.ApplicationBasePath + "/../../..";
            });

            var serviceProvider = services.BuildServiceProvider();
            var node = serviceProvider.GetService<INodeServices>();


            var p1 = new[] { 12.62929948218958, 55.661805096493985 };
            var proj4Str = $"+proj=tmerc +lat_0={p1[1]} +lon_0={p1[0]} +x_0=0 +y_0=0 +towgs84=0,0,0,0,0,0,0 +units=m +vunits=m +no_defs";
            var extent = new[] { -20000.0, -20000, 20000, 20000 };


            var tileConverter = new VectorTileConverter(new VectorTileConverterOptions
            {
                CoordinateTransform = async (p) =>
                {
                    var local = await node.InvokeAsync<double[]>("./proj4", "EPSG:4326", proj4Str, p);

                    return new double[] { (local[0]- extent[0]) / (extent[2] - extent[0]), -( local[1] - extent[3]) / (extent[3] - extent[1]), 0 };
                }
            });


            var p2 = tileConverter.ProjectPoint(p1);

            Assert.AreEqual(0.5, p2[0], 10e-6, $"X {nameof(TestCustemProjection)} expected to be 0.5");
            Assert.AreEqual(0.5, p2[1], 10e-6, $"Y {nameof(TestCustemProjection)} expected to be 0.5");
            {
                var bottomLeft = tileConverter.ProjectPoint( node.InvokeAsync<double[]>("./proj4",proj4Str,"EPSG:4326", new[] { extent[0], extent[1] }).GetAwaiter().GetResult());

                Assert.AreEqual(0, bottomLeft[0], 10e-6, $"X {nameof(TestCustemProjection)} {nameof(bottomLeft)} expected to be 0");
                Assert.AreEqual(1, bottomLeft[1], 10e-6, $"Y {nameof(TestCustemProjection)}  {nameof(bottomLeft)} expected to be 1");
            }
            {
                var topLeft = tileConverter.ProjectPoint(node.InvokeAsync<double[]>("./proj4", proj4Str, "EPSG:4326", new[] { extent[0], extent[3] }).GetAwaiter().GetResult());

                Assert.AreEqual(0, topLeft[0], 10e-6, $"X {nameof(TestCustemProjection)} {nameof(topLeft)} expected to be 0");
                Assert.AreEqual(0, topLeft[1], 10e-6, $"Y {nameof(TestCustemProjection)}  {nameof(topLeft)} expected to be 0");
            }
            {
                var topRight = tileConverter.ProjectPoint(node.InvokeAsync<double[]>("./proj4", proj4Str, "EPSG:4326", new[] { extent[2], extent[3] }).GetAwaiter().GetResult());

                Assert.AreEqual(1, topRight[0], 10e-6, $"X {nameof(TestCustemProjection)} {nameof(topRight)} expected to be 1");
                Assert.AreEqual(0, topRight[1], 10e-6, $"Y {nameof(TestCustemProjection)}  {nameof(topRight)} expected to be 0");
            }
            {
                var bottomRight = tileConverter.ProjectPoint(node.InvokeAsync<double[]>("./proj4", proj4Str, "EPSG:4326", new[] { extent[2], extent[1] }).GetAwaiter().GetResult());

                Assert.AreEqual(1, bottomRight[0], 10e-6, $"X {nameof(TestCustemProjection)} {nameof(bottomRight)} expected to be 1");
                Assert.AreEqual(1, bottomRight[1], 10e-6, $"Y {nameof(TestCustemProjection)}  {nameof(bottomRight)} expected to be 1");
            }
        }
    }
}
