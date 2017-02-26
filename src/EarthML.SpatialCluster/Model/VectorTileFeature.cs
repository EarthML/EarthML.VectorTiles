using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;


namespace EarthML.SpatialCluster.Models
{
    
    
  
    public class VectorTileFeature
    {
        public object Geometry { get; set; }

        //  internal VectorTileGeometry[] GeometriesImp => Geometry as VectorTileGeometry[];
        //   internal VectorTileGeometry GeometryImp => Geometry as VectorTileGeometry;

        public VectorTileGeometry GetPoints() => Geometry as VectorTileGeometry;
        public VectorTileGeometry[] GetRings() => Type==1 ? new[] { Geometry as VectorTileGeometry } : Geometry as VectorTileGeometry[];

        public int Type { get; set; }
        public Dictionary<string, object> Tags { get; set; }

     
        public double[] Min { get; set; } = new double[] { 2, 1 };
 
        public double[] Max { get; set; } = new double[] { -1, 0 };


    }

   
}
