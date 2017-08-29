using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthML.SpatialCluster.Algorithms.Optics
{
    internal class Point : PriorityQueueNode
    {
        public Point(UInt32 index, UInt32 id, double[] vector)
        {
            Index = index;
            Id = id;
            Vector = vector;

            WasProcessed = false;
            ReachabilityDistance = double.NaN;
        }

        public readonly UInt32 Id;
        public readonly double[] Vector;
        public readonly UInt32 Index;

        internal double ReachabilityDistance;
        internal bool WasProcessed;
    }
}
