using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthML.SpatialCluster
{
    public interface IVectorTileGeometry
    {

    }
    public class VectorTileGeometry : List<double[]>
    {
        public VectorTileGeometry()
        {

        }
        public VectorTileGeometry(IEnumerable<double[]> points) : base(points)
        {

        }
        public double Area { get; set; }
        public double Distance { get; set; }

        //public void Add(params double[][] points)
        //{
        //    AddRange(points);
        //}

        public int[][] ToIntArray()
        {
            return this.Select(k => new[] { (int)k[0], (int)k[1] }).ToArray();
        }
    }
}
