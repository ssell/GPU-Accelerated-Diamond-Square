using System.Threading;
using UnityEngine;

namespace VertexFragment
{
    /// <summary>
    /// A noise algorithm that excels at allowing a degree of control over the output.<para/>
    /// 
    /// Useful in instances where there is a general "shape" that is desired, but the details
    /// are not important/can be left up to RNG.
    /// </summary>
    public sealed class DiamondSquare : NoiseBase
    {
        public override bool RequiresPreGeneration => true;

        /// <summary>
        /// Diamond-Square, unlike other noise algorithms, is finite in length.<para/>
        /// It must be a square that is <c>2^n+1</c> in length (5x5, 9x9, 17x17, etc.).
        /// </summary>
        public int Dimensions = 1025;

        /// <summary>
        /// Diamond-Square takes in an array of height seed values, in addition to <see cref="NoiseBase.Seed"/>.<para/>
        /// 
        /// These values supply the starting state of the algorithm and the dimensions must be square,
        /// a multiple of <c>2^n+1</c>, and smaller in size than <see cref="Dimensions"/>. Should use
        /// the smallest amount of height seeds as possible, while still achieving the general shape 
        /// that is desired.
        /// </summary>
        public float[,] HeightSeeds = new float[3, 3]
        {
            { 0.0f, 0.5f, 0.0f },
            { 0.5f, 1.0f, 0.5f },
            { 0.0f, 0.5f, 0.0f }
        };

        /// <summary>
        /// Value on the range [0.0, 1.0] that controls the magnitude of the random offsets applied.
        /// Lower values produce greater offsets.
        /// </summary>
        public float Amplitude = 0.5f;

        /// <summary>
        /// Value on the range [0.0, 1.0]. Amount each successive subdivision is affected by random variations.
        /// </summary>
        public float Persistence = 0.5f;

        /// <summary>
        /// The final generated noise map.
        /// </summary>
        private float[,] Noise;

        /// <summary>
        /// The PRNG used to offset each value.
        /// </summary>
        public System.Random Rng;

        // ---------------------------------------------------------------------------------
        // Generation
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// Diamond-Square requires state, and as such has to be pre-generated.
        /// So must call this before any of the <see cref="GetValue"/> methods.
        /// </summary>
        /// <param name="threadPool"></param>
        /// <param name="threadsPerDimension"></param>
        /// <returns></returns>
        public override bool Generate(ThreadPool threadPool)
        {
            if (Rng == null)
            {
                Rng = new System.Random(Seed);
            }

            Noise = new float[Dimensions, Dimensions];
            bool[,] isSetMap = new bool[Dimensions, Dimensions];

            InitializeNoise(isSetMap);

            float amplitude = Amplitude;
            int stepsPerSubdivision = 1;
            int stepSize = Dimensions / 2;
            int maxStepsPerThread = 128;

            while (stepSize > 0)
            {
                float offsetModifier = Mathf.Clamp01(amplitude);

                DiamondSteps(threadPool, maxStepsPerThread, stepsPerSubdivision, stepSize, isSetMap, offsetModifier);
                SquareSteps(threadPool, maxStepsPerThread, stepsPerSubdivision, stepSize, isSetMap, offsetModifier);

                stepsPerSubdivision *= 2;
                stepSize /= 2;
                amplitude *= Persistence;
            }

            return true;
        }

        /// <summary>
        /// Generates the base noise map that the algorithm runs off of.<para/>
        /// 
        /// The array value is a tuple where (has seed value, seed value). We use the 
        /// bool flag as 0.0f is a valid seed value. Alternatively could just set the
        /// value to -1.0f if no seed value is present, but thats not as fancy.
        /// </summary>
        /// <returns></returns>
        private void InitializeNoise(bool[,] isSetMap)
        {
            int seedDimensions = HeightSeeds.GetLength(0);
            int stepSize = ((Dimensions - 1) / (seedDimensions - 1));     // Example: ((257 - 1) / (9 - 1)) = 32.

            for (int iy = 0; iy < seedDimensions; ++iy)
            {
                for (int ix = 0; ix < seedDimensions; ++ix)
                {
                    SetValue(ix * stepSize, iy * stepSize, HeightSeeds[ix, iy], isSetMap);
                }
            }
        }

        /// <summary>
        /// Performs all of the diamond steps for the subdivision.
        /// Diamonds sample from the corners, and place a new value in the center of the diamond.<para/>
        /// 
        /// <code>
        ///     X───────X
        ///     │       │
        ///     │   O   │
        ///     │       │
        ///     X───────X
        /// </code>
        /// </summary>
        /// <param name="stepsPerSubdivision"></param>
        /// <param name="stepsPerThread"></param>
        /// <param name="isSetMap"></param>
        /// <param name="offsetModifier"></param>
        private void DiamondSteps(ThreadPool threadPool, int stepsPerThread, int stepsPerSubdivision, int stepSize, bool[,] isSetMap, float offsetModifier)
        {
            int jobsPerDimension = (stepsPerSubdivision / stepsPerThread);

            if ((stepsPerSubdivision < stepsPerThread) || (jobsPerDimension == 1))
            {
                // Not enough steps to truly multi-thread.
                new DiamondStepJob()
                {
                    Noise = this,
                    StartX = 0,
                    StartY = 0,
                    Steps = stepsPerSubdivision,
                    StepSize = stepSize,
                    OffsetModifier = offsetModifier,
                    IsSetMap = isSetMap
                }.Execute(CancellationToken.None);
            }
            else
            {
                for (int y = 0; y < jobsPerDimension; ++y)
                {
                    for (int x = 0; x < jobsPerDimension; ++x)
                    {
                        threadPool.EnqueueJob(new DiamondStepJob()
                        {
                            Noise = this,
                            StartX = (x * stepsPerThread),
                            StartY = (y * stepsPerThread),
                            Steps = stepsPerThread,
                            StepSize = stepSize,
                            OffsetModifier = offsetModifier,
                            IsSetMap = isSetMap
                        });
                    }
                }

                threadPool.Sync();
            }
        }

        /// <summary>
        /// Performs all of the square steps for the subdivision.
        /// Squares sample from those adjacent to it, and place a new value in the center of that square.<para/>
        /// 
        /// <code>
        ///     ┌───X───┐
        ///     │       │
        ///     X   O   X
        ///     │       │
        ///     └───X───┘
        /// </code>
        /// </summary>
        /// <param name="threadPool"></param>
        /// <param name="stepsPerThread"></param>
        /// <param name="stepsPerSubdivision"></param>
        /// <param name="stepSize"></param>
        /// <param name="isSetMap"></param>
        /// <param name="offsetModifier"></param>
        private void SquareSteps(ThreadPool threadPool, int stepsPerThread, int stepsPerSubdivision, int stepSize, bool[,] isSetMap, float offsetModifier)
        {
            int jobsPerDimension = (stepsPerSubdivision / stepsPerThread);

            if ((stepsPerSubdivision < stepsPerThread) || (jobsPerDimension == 1))
            {
                // Not enough steps to truly multi-thread.
                new SquareStepJob()
                {
                    Noise = this,
                    StartX = 0,
                    StartY = 0,
                    Steps = stepsPerSubdivision,
                    StepSize = stepSize,
                    OffsetModifier = offsetModifier,
                    IsSetMap = isSetMap
                }.Execute(CancellationToken.None);
            }
            else
            {
                for (int y = 0; y < jobsPerDimension; ++y)
                {
                    for (int x = 0; x < jobsPerDimension; ++x)
                    {
                        threadPool.EnqueueJob(new SquareStepJob()
                        {
                            Noise = this,
                            StartX = (x * stepsPerThread),
                            StartY = (y * stepsPerThread),
                            Steps = stepsPerThread,
                            StepSize = stepSize,
                            OffsetModifier = offsetModifier,
                            IsSetMap = isSetMap
                        });
                    }
                }

                threadPool.Sync();
            }
        }

        /// <summary>
        /// Samples the corner neighbors for the diamond step.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="stepSize"></param>
        /// <param name="offsetMod"></param>
        /// <returns></returns>
        private float SampleDiamond(int x, int y, int stepSize, float offsetMod)
        {
            int left = (int)Mathf.Clamp(x - stepSize, 0, Dimensions - 1);
            int right = (int)Mathf.Clamp(x + stepSize, 0, Dimensions - 1);
            int upper = (int)Mathf.Clamp(y + stepSize, 0, Dimensions - 1);
            int lower = (int)Mathf.Clamp(y - stepSize, 0, Dimensions - 1);

            float value = 0.25f * (Noise[left, upper] + Noise[right, upper] + Noise[left, lower] + Noise[right, lower]);
            float offset = (float)Rng.NextDoubleSigned() * offsetMod;

            return Mathf.Clamp01(value + offset);
        }

        /// <summary>
        /// Samples the adjacent neighbors for the square step.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="stepSize"></param>
        /// <param name="offsetMod"></param>
        /// <returns></returns>
        private float SampleSquare(int x, int y, int stepSize, float offsetMod)
        {
            int left = (int)Mathf.Clamp(x - stepSize, 0, Dimensions - 1);
            int right = (int)Mathf.Clamp(x + stepSize, 0, Dimensions - 1);
            int upper = (int)Mathf.Clamp(y + stepSize, 0, Dimensions - 1);
            int lower = (int)Mathf.Clamp(y - stepSize, 0, Dimensions - 1);

            float value = 0.25f * (Noise[x, upper] + Noise[left, y] + Noise[right, y] + Noise[x, lower]);
            float offset = (float)Rng.NextDoubleSigned() * offsetMod;

            return Mathf.Clamp01(value + offset);
        }

        /// <summary>
        /// Sets the value in <see cref="Noise"/> if it has not already been set.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="value"></param>
        /// <param name="isSetMap"></param>
        private void SetValue(int x, int y, float value, bool[,] isSetMap)
        {
            Noise[x, y] = value;
            isSetMap[x, y] = true;
        }

        // ---------------------------------------------------------------------------------
        // Jobs
        // ---------------------------------------------------------------------------------

        private sealed class DiamondStepJob : JobBase
        {
            public DiamondSquare Noise;

            public int StartX;
            public int StartY;
            public int Steps;
            public int StepSize;

            public float OffsetModifier;

            public bool[,] IsSetMap;

            public override void Execute(CancellationToken cancellationToken)
            {
                int endX = Mathf.Min(StartX + Steps, IsSetMap.GetLength(0));
                int endY = Mathf.Min(StartY + Steps, IsSetMap.GetLength(1));

                for (int iy = StartY; iy < endY; ++iy)
                {
                    for (int ix = StartX; ix < endX; ++ix)
                    {
                        int originX = (StepSize * 2) * ix;
                        int originY = (StepSize * 2) * iy;

                        int dx = originX + StepSize;
                        int dy = originY + StepSize;

                        if (Noise.IsValidPosition(dx, dy) && !IsSetMap[dx, dy])
                        {
                            Noise.SetValue(dx, dy, Noise.SampleDiamond(dx, dy, StepSize, OffsetModifier), IsSetMap);
                        }
                    }
                }
            }
        }

        private sealed class SquareStepJob : JobBase
        {
            public DiamondSquare Noise;

            public int StartX;
            public int StartY;
            public int Steps;
            public int StepSize;

            public float OffsetModifier;

            public bool[,] IsSetMap;

            public override void Execute(CancellationToken cancellationToken)
            {
                int endX = Mathf.Min(StartX + Steps, IsSetMap.GetLength(0));
                int endY = Mathf.Min(StartY + Steps, IsSetMap.GetLength(1));

                for (int iy = StartY; iy < endY; ++iy)
                {
                    for (int ix = StartX; ix < endX; ++ix)
                    {
                        int originX = (StepSize * 2) * ix;
                        int originY = (StepSize * 2) * iy;

                        int s0x = originX + StepSize;
                        int s0y = originY;

                        if (Noise.IsValidPosition(s0x, s0y) && !IsSetMap[s0x, s0y])
                        {
                            Noise.SetValue(s0x, s0y, Noise.SampleSquare(s0x, s0y, StepSize, OffsetModifier), IsSetMap);
                        }

                        int s1x = originX;
                        int s1y = originY + StepSize;

                        if (Noise.IsValidPosition(s1x, s1y) && !IsSetMap[s1x, s1y])
                        {
                            Noise.SetValue(s1x, s1y, Noise.SampleSquare(s1x, s1y, StepSize, OffsetModifier), IsSetMap);
                        }

                        int s2x = originX + (StepSize * 2);
                        int s2y = originY + StepSize;

                        if (Noise.IsValidPosition(s2x, s2y) && !IsSetMap[s2x, s2y])
                        {
                            Noise.SetValue(s2x, s2y, Noise.SampleSquare(s2x, s2y, StepSize, OffsetModifier), IsSetMap);
                        }

                        int s3x = originX + StepSize;
                        int s3y = originY + (StepSize * 2);

                        if (Noise.IsValidPosition(s3x, s3y) && !IsSetMap[s3x, s3y])
                        {
                            Noise.SetValue(s3x, s3y, Noise.SampleSquare(s3x, s3y, StepSize, OffsetModifier), IsSetMap);
                        }
                    }
                }
            }
        }

        // ---------------------------------------------------------------------------------
        // NoiseBase
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// Retrieves a value from the generated noise map.<para/>
        /// 
        /// Must have previously called <see cref="Generate"/> and <c>(x, y)</c> must be within <c>[0, <see cref="Dimensions"/>)</c>.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public override float GetValue(float x, float y)
        {
            if ((Noise == null) || (x < 0) || (y < 0) || (x >= Dimensions) || (y >= Dimensions))
            {
                return 0.0f;
            }

            return Noise[(int)x, (int)y];
        }

        /// <summary>
        /// Calls <see cref="GetValue(float, float)"/> with <c>(x, z)</c>.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <returns></returns>
        public override float GetValue(float x, float y, float z)
        {
            return GetValue(x, z);
        }

        // ---------------------------------------------------------------------------------
        // Misc
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// Sets the <see cref="HeightSeeds"/> from the provided texture's R channel.
        /// Does not perform any validation of the source dimensions, etc.
        /// </summary>
        /// <param name="source"></param>
        public override void SetHeightSeedsFromTexture(Texture2D source)
        {
            HeightSeeds = new float[source.width, source.height];
            Color[] sourcePixels = source.GetPixels(0, 0, source.width, source.height);

            for (int y = 0; y < source.height; ++y)
            {
                for (int x = 0; x < source.width; ++x)
                {
                    HeightSeeds[x, y] = sourcePixels[x + (y * source.width)].r;
                }
            }
        }

        /// <summary>
        /// Is this a valid position in the noise array?
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private bool IsValidPosition(int x, int y)
        {
            return (x >= 0) && (y >= 0) && (x < Dimensions) && (y < Dimensions);
        }
    }
}
