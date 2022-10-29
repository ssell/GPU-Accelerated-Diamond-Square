using System;

namespace VertexFragment
{
    public static class RandomExtensions
    {
        /// <summary>
        /// Returns next random value on the range <c>[-1.0, 1.0]</c>.
        /// </summary>
        /// <param name="rng"></param>
        /// <returns></returns>
        public static double NextDoubleSigned(this Random rng)
        {
            return (rng.NextDouble() * 2.0) - 1.0;
        }
    }
}
