using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EarthML.SpatialCluster.Processing
{
    public class VectorTileSimplifier
    {
        public void simplify(List<double[]> points, double tolerance)
        {
            var sqTolerance = tolerance * tolerance;
            var len = points.Count;
            var first = 0;
            int? last = len - 1;
            var stack = new Stack<int>();
            int index = 0;
        // var i, maxSqDist, sqDist, index;

            // always retain the endpoints (1 is the max value)
            points[first][2] = 1;
            points[last.Value][2] = 1;

            // avoid recursion by using a stack
            while (last.HasValue)
            {

                var maxSqDist = 0.0;

                for (var i = first + 1; i < last; i++)
                {
                    var sqDist = getSqSegDist(points[i], points[first], points[last.Value]);

                    if (sqDist > maxSqDist)
                    {
                        index = i;
                        maxSqDist = sqDist;
                    }
                }

                if (maxSqDist > sqTolerance)
                {
                    points[index][2] = maxSqDist; // save the point importance in squared pixels as a z coordinate
                    stack.Push(first);
                    stack.Push(index);
                    first = index;

                }
                else
                {
                    if (stack.Count > 0)
                    {
                        last = stack.Pop();
                        first = stack.Pop();
                    }else
                    {
                        last = null;
                    }
                
                }
            }
        }

        private double getSqSegDist(double[] p, double[] a, double[] b)
        {

            var x = a[0];
            var y = a[1];
            var bx = b[0];
            var by = b[1];
            var px = p[0];
            var py = p[1];
            var dx = bx - x;
            var dy = by - y;

            if (dx != 0 || dy != 0)
            {

                var t = ((px - x) * dx + (py - y) * dy) / (dx * dx + dy * dy);

                if (t > 1)
                {
                    x = bx;
                    y = by;

                }
                else if (t > 0)
                {
                    x += dx * t;
                    y += dy * t;
                }
            }

            dx = px - x;
            dy = py - y;

            return dx * dx + dy * dy;
        }
    }

}
