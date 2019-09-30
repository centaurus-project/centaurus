using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Test
{
    public static class RandomExtensions
    {
        /// <summary>
        /// Generates normally distributed random sequences.
        /// </summary>
        /// <param name = "mean">Mean of the distribution</param>
        /// <param name = "sigma">Standard deviation</param>
        /// <returns></returns>
        public static double NextNormallyDistributed(this Random randomGenerator, double sigma = 1, double mean = 0)
        {
            var rStdNormal = Math.Sqrt(Math.Log(randomGenerator.NextDouble()) * -2)
                * Math.Sin(randomGenerator.NextDouble() * Math.PI * 2);

            return sigma * rStdNormal + mean;
        }

        /// <summary>
        ///   Generates values from a triangular distribution.
        /// </summary>
        /// <param name = "min">Min value</param>
        /// <param name = "max">Max value</param>
        /// <param name = "mode">The most common value - the top of the triangle</param>
        /// <returns></returns>
        public static double NextTriangularlyDistributed(this Random randomGenerator, double min, double max, double mode)
        {
            double rnd = randomGenerator.NextDouble(),
                left = mode - min,
                right = max - mode,
                spread = max - min;

            return left / spread > rnd
                       ? min + Math.Sqrt(rnd * spread * left)
                       : max - Math.Sqrt((1 - rnd) * spread * right);
        }
    }
}
