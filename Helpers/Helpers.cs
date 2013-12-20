using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CubicFunctionFitter
{
    public static class Helpers
    {
        //http://stackoverflow.com/a/2683487/758666
        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }

        /// <summary>
        /// Linearly interpolates over the value x between the points (xMin, yMin) and (xMax, yMax).
        /// </summary>
        public static double Lerp(
            double x,
            double xMin, double xMax,
            double yMin, double yMax)
        {
            double ratio = (x - xMin) / (xMax - xMin);
            return yMin + ratio * (yMax - yMin);
        }

    }
}
