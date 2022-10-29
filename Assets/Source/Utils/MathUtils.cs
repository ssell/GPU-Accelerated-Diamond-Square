namespace VertexFragment
{
    public static class MathUtils
    {
        /// <summary>
        /// Is the provided integer a power of 2?
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static bool IsPowerOf2(int x)
        {
            return (x & (x - 1)) == 0;
        }

        /// <summary>
        /// Is this a <c>2^n+1</c>?
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static bool IsPowerOf2Plus1(int x)
        {
            return IsPowerOf2(x - 1);
        }

        /// <summary>
        /// Given a value, returns which power of 2 is closest, but less, than the value.
        /// For signed, 32-bit integers only.
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static int WhichPowerOf2(int x)
        {
            // This is probably a really dumb implementation, but ...

            if (x <= 1)
            {
                // Undefined for < 1, but lets just return 0 and move on with our lives.
                return 0;
            }

            // Quick look up for actual power-of-2 values.
            switch (x)
            {
                case 2: return 1;
                case 4: return 2;
                case 8: return 3;
                case 16: return 4;
                case 32: return 5;
                case 64: return 6;
                case 128: return 7;
                case 256: return 8;
                case 512: return 9;
                case 1024: return 10;
                case 2048: return 11;
                case 4096: return 12;
                case 8192: return 13;
                case 16384: return 14;
                case 32768: return 15;
                case 65536: return 16;
                case 131072: return 17;
                case 262144: return 18;
                case 524288: return 19;
                case 1048576: return 20;
                case 2097152: return 21;
                case 4194304: return 22;
                case 8388608: return 23;
                case 16777216: return 24;
                case 33554432: return 25;
                case 67108864: return 26;
                case 134217728: return 27;
                case 268435456: return 28;
                case 536870912: return 29;
                case 1073741824: return 30;
                default: break;
            }

            // Loop!
            int total = 2;
            int nearest = 0;

            while ((total < x) && (nearest < 30))
            {
                nearest++;
                total *= 2;
            }

            return nearest;
        }
    }
}
