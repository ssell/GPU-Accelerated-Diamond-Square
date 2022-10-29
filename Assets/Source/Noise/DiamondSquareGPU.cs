using UnityEngine;

namespace VertexFragment
{
    /// <summary>
    /// A GPU-accelerated version of <see cref="DiamondSquare"/>.
    /// </summary>
    public sealed class DiamondSquareGPU : NoiseBase
    {
        // ---------------------------------------------------------------------------------
        // General Properties
        // ---------------------------------------------------------------------------------

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
        private float[] Noise;

        // ---------------------------------------------------------------------------------
        // Compute Shader Properties
        // ---------------------------------------------------------------------------------

        private const string ComputePath = "Shaders/Compute/DiamondSquare";

        private static readonly int NoiseBufferId = Shader.PropertyToID("_NoiseBuffer");
        private static readonly int IsSetBufferId = Shader.PropertyToID("_IsSetBuffer");
        private static readonly int DimensionsId = Shader.PropertyToID("_Dimensions");
        private static readonly int StepSizeId = Shader.PropertyToID("_StepSize");
        private static readonly int OffsetModifierId = Shader.PropertyToID("_OffsetModifier");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        private static ComputeShader Compute;

        // ---------------------------------------------------------------------------------
        // Debug Properties
        // ---------------------------------------------------------------------------------

        public bool DiamondOnly = false;
        public bool SquareOnly = false;
        public int DebugMaxSteps = 999;

        // ---------------------------------------------------------------------------------
        // Generation
        // ---------------------------------------------------------------------------------

        public override bool Generate(ThreadPool threadPool)
        {
            Noise = new float[Dimensions * Dimensions];

            if (!FetchComputeShader())
            {
                Debug.LogError($"Failed to load compute shader at '{ComputePath}'");
                return false;
            }

            var buffers = PrepareComputeShader();

            Compute.GetKernelThreadGroupSizes(0, out uint threadsPerX, out _, out _);

            int threadGroups = Dimensions / (int)threadsPerX;
            float amplitude = Amplitude;
            int stepSize = (Dimensions - 1) / 4;                    // Our smallest seed map (3x3) already populates the first couple of steps.

            int i = 1;

            while (stepSize >= 1)
            {
                float offsetModifier = Mathf.Clamp01(amplitude);

                Compute.SetInt(StepSizeId, stepSize);
                Compute.SetFloat(OffsetModifierId, offsetModifier);

                if (!SquareOnly)
                {
                    Compute.SetBuffer(0, NoiseBufferId, buffers.noiseBuffer);
                    Compute.SetBuffer(0, IsSetBufferId, buffers.isSetBuffer);
                    Compute.Dispatch(0, threadGroups, threadGroups, 1);
                }

                if (!DiamondOnly)
                {
                    Compute.SetInt(StepSizeId, stepSize / 2);
                    Compute.SetBuffer(1, NoiseBufferId, buffers.noiseBuffer);
                    Compute.SetBuffer(1, IsSetBufferId, buffers.isSetBuffer);
                    Compute.Dispatch(1, threadGroups, threadGroups, 1);
                }

                if (i++ >= DebugMaxSteps)
                {
                    break;
                }

                stepSize /= 2;
                amplitude *= Persistence;
            }

            buffers.noiseBuffer.GetData(Noise);
            buffers.noiseBuffer.Release();
            buffers.isSetBuffer.Release();

            return true;
        }

        /// <summary>
        /// Loads the compute shader from the resources directory.
        /// </summary>
        /// <returns></returns>
        private bool FetchComputeShader()
        {
            if (Compute == null)
            {
                Compute = Resources.Load(ComputePath) as ComputeShader;
            }

            return (Compute != null);
        }

        /// <summary>
        /// Initializes our compute buffers and sets the static data.
        /// </summary>
        /// <returns></returns>
        private (ComputeBuffer noiseBuffer, ComputeBuffer isSetBuffer) PrepareComputeShader()
        {
            ComputeBuffer noiseBuffer = new ComputeBuffer(Dimensions * Dimensions, sizeof(float));
            ComputeBuffer isSetBuffer = new ComputeBuffer(Dimensions * Dimensions, sizeof(float));

            Compute.SetInt(DimensionsId, Dimensions);
            Compute.SetInt(SeedId, Seed);

            FillFromSeeds(noiseBuffer, isSetBuffer);

            return (noiseBuffer, isSetBuffer);
        }

        /// <summary>
        /// Sets the <see cref="HeightSeeds"/> from the provided texture's R channel.
        /// Does not perform any validation of the source dimensions, etc.
        /// </summary>
        /// <param name="source"></param>
        public void SetHeightSeedsFromTexture(Texture2D source)
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
        /// Pre-fills our <see cref="Noise"/> data with values from the seed map.
        /// </summary>
        /// <param name="noiseBuffer"></param>
        /// <param name="isSetBuffer"></param>
        private void FillFromSeeds(ComputeBuffer noiseBuffer, ComputeBuffer isSetBuffer)
        {
            float[] startIsSetState = new float[Dimensions * Dimensions];

            int seedDimensions = HeightSeeds.GetLength(0);
            int stepSize = (Dimensions - 1) / (seedDimensions - 1);

            for (int y = 0; y < seedDimensions; ++y)
            {
                for (int x = 0; x < seedDimensions; ++x)
                {
                    int index = (x * stepSize) + (y * stepSize * Dimensions);

                    Noise[index] = HeightSeeds[x, y];
                    startIsSetState[index] = 1.0f;
                }
            }

            noiseBuffer.SetData(Noise);
            isSetBuffer.SetData(startIsSetState);
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

            return Noise[(int)((y * Dimensions) + x)];
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
    }
}
