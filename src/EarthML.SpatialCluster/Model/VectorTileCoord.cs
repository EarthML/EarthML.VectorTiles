using EarthML.GeoJson.Geometries;
using EarthML.SpatialCluster.Processing;
using System;
using System.Collections.Generic;
using System.Text;

namespace EarthML.SpatialCluster.Models
{
    public struct VectorTileCoord
    {
        public VectorTileCoord(int z, int x, int y) { Z = z; Y = y; X = x; }
        public int Z;
        public int Y;
        public int X;

        public IEnumerable<VectorTileCoord> GetChildCoordinate()
        {
            yield return new VectorTileCoord(Z + 1, X * 2, Y * 2);
            yield return new VectorTileCoord(Z + 1, X * 2, Y * 2 + 1);
            yield return new VectorTileCoord(Z + 1, X * 2 + 1, Y * 2);
            yield return new VectorTileCoord(Z + 1, X * 2 + 1, Y * 2 + 1);
        }


        public string ToID()
        {
            return ((((1 << Z) * Y + X) * 32) + Z).ToString();
        }

        public static string ToID(int z,int x, int y)
        {
            return ((((1 << z) * y + x) * 32) + z).ToString();
        }

        public static VectorTileCoord QuadKeyToTileXY(string quadKey)
        {
            var tileX = 0;var tileY = 0;
            var levelOfDetail = quadKey.Length;
            for (int i = levelOfDetail; i > 0; i--)
            {
                int mask = 1 << (i - 1);
                switch (quadKey[levelOfDetail - i])
                {
                    case '0':
                        break;

                    case '1':
                        tileX |= mask;
                        break;

                    case '2':
                        tileY |= mask;
                        break;

                    case '3':
                        tileX |= mask;
                        tileY |= mask;
                        break;

                    default:
                        throw new ArgumentException("Invalid QuadKey digit sequence.");
                }
            }

            return new VectorTileCoord(levelOfDetail, tileX, tileY);
        }

        public string ToQuadKey(int padding =0, char pad='_')
        {
            StringBuilder quadKey = new StringBuilder();
            for (int i = Z; i > 0; i--)
            {
                char digit = '0';
                int mask = 1 << (i - 1);
                if ((X & mask) != 0)
                {
                    digit++;
                }
                if ((Y & mask) != 0)
                {
                    digit++;
                    digit++;
                }
                quadKey.Append(digit);
            }
            padding -= Z;
            while (padding --> 0)
            {
                quadKey.Append(pad);
            }
            return quadKey.ToString();
        }
        //public void TileXYToPixelXY(int tileX, int tileY, out int pixelX, out int pixelY)
        //{
        //    pixelX = tileX * _tileSize;
        //    pixelY = tileY * _tileSize;
        //}
        static VectorTileConverter _con = new VectorTileConverter(); 
        public double[] GetExtent(double[] opt_extent=null)
        {
            var extent = opt_extent ?? new double[4];
            var x = 1.0 / (1 << Z) * X;
            var y = 1.0 / (1 << Z) * Y;
            var xx = 1.0 / (1 << Z) * (X+1);
            var yy = 1.0 / (1 << Z) * (Y+1);

            var p1 = _con.UnpackPoint(new[] {x,y });
            var p2 = _con.UnpackPoint(new[] { xx, yy });

            extent[0] = p1[0];
            extent[1] = p2[1];
            extent[2] = p2[0];
            extent[3] = p1[1];

            
            return extent;
        }

        public Polygon  GetPolygonCoords()
        {
            var extent = GetExtent();


            return new Polygon
            {
                Coordinates = new double[][][]
            {
                new double[][]
                {
                    new double[] {extent[0],extent[1]},
                    new double[] {extent[0],extent[3]},
                    new double[] {extent[2],extent[3]},
                    new double[] {extent[2],extent[1]},
                    new double[] {extent[0],extent[1]}
                }
            }
            };
        
        } 

    }
}
