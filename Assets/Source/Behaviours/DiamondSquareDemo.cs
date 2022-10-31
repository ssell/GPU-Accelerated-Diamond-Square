using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

namespace VertexFragment
{
    /// <summary>
    /// Simple demo scene controller that generates the image and mesh visualizations.
    /// </summary>
    public sealed class DiamondSquareDemo : MonoBehaviour
    {
        // Controls
        public bool Generate = false;

        // Demo Scene
        public RawImage Noise2D;
        public GameObject Noise3D;
        public Material MeshMaterial;
        public bool SaveHeightmapToDisk;

        // Input
        public Texture2D SeedTexture;
        public int Seed = 1337;
        public int Dimensions = 1024;
        public float Amplitude = 1.0f;
        public float Persistence = 0.5f;
        public float MaxHeight = 100.0f;
        public int MeshXZStep = 4;
        public bool UseGpuAcceleration = false;

        private ThreadPool Threads;

        void Start()
        {
            Threads = new ThreadPool(64);
            Threads.Build();
        }

        void Update()
        {
            if (!GenerateRequested())
            {
                return;
            }

            if (!IsValidInput())
            {
                return;
            }

            float[,] noise = GenerateNoise();

            Set2DNoiseVisual(noise);
            Set3DNoiseVisual(noise);
        }

        /// <summary>
        /// Has the <see cref="Generate"/> control been toggled?
        /// </summary>
        /// <returns></returns>
        private bool GenerateRequested()
        {
            if (Generate)
            {
                Generate = false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Generates the Diamond-Square noise using the selected generated (CPU or GPU).
        /// </summary>
        /// <returns></returns>
        private float[,] GenerateNoise()
        {
            NoiseBase noiseGenerator = (UseGpuAcceleration ? 
                new DiamondSquareGPU()
                {
                    Seed= Seed,
                    Dimensions = Dimensions,
                    Amplitude = Amplitude,
                    Persistence = Persistence
                } :
                new DiamondSquare()
                {
                    Seed = Seed,
                    Dimensions = Dimensions,
                    Amplitude = Amplitude,
                    Persistence = Persistence
                });

            Stopwatch sw = Stopwatch.StartNew();

            noiseGenerator.SetHeightSeedsFromTexture(SeedTexture);
            noiseGenerator.Generate(Threads);

            sw.Stop();
            UnityEngine.Debug.Log($"Finished generating noise in {sw.ElapsedMilliseconds} ms.");

            int dimensionsMinusOne = Dimensions - 1;
            float[,] noise = new float[dimensionsMinusOne, dimensionsMinusOne];

            for (int y = 0; y < dimensionsMinusOne; ++y)
            {
                for (int x = 0; x < dimensionsMinusOne; ++x)
                {
                    noise[x, y] = noiseGenerator.GetValue(x, y);
                }
            }
            
            return noise;
        }

        /// <summary>
        /// Creates the 2D image of the noise.
        /// </summary>
        /// <param name="noise"></param>
        private void Set2DNoiseVisual(float[,] noise)
        {
            if (Noise2D == null)
            {
                return;
            }

            if (Noise2D.texture != null)
            {
                Destroy(Noise2D.texture);
            }

            Texture2D noiseTexture = TextureUtils.FloatArrayToTexture(noise);
            Noise2D.texture = noiseTexture;

            if (SaveHeightmapToDisk)
            {
                TextureUtils.SavePng(noiseTexture, $"DiamondSquare_{Dimensions - 1}x{Dimensions - 1}_Seed_{Seed}");
            }
        }

        /// <summary>
        /// Creates the mesh for the 3D visualization of the noise.
        /// </summary>
        /// <param name="noise"></param>
        private void Set3DNoiseVisual(float[,] noise)
        {
            if (Noise3D == null)
            {
                return;
            }

            var filter = Noise3D.gameObject.GetOrAddComponent<MeshFilter>();
            var renderer = Noise3D.gameObject.GetOrAddComponent<MeshRenderer>();

            if (filter.mesh != null)
            {
                Destroy(filter.mesh);
            }

            int dimensionsMinusOne = Dimensions - 1;
            int verticesPerDimension = dimensionsMinusOne / MeshXZStep;
            float oneOverDimensions = 1.0f / verticesPerDimension;

            CustomMesh mesh = new CustomMesh("3D Noise");
            mesh.InitializeBuffers(verticesPerDimension * verticesPerDimension, 3);

            for (int z = 0; z < dimensionsMinusOne; z += MeshXZStep)
            {
                for (int x = 0; x < dimensionsMinusOne; x += MeshXZStep)
                {
                    float height = noise[x, z] * MaxHeight;

                    mesh.Vertices.Add(new Vector3(x, height, z));
                    mesh.UVs.Add(new Vector2((float)x * oneOverDimensions, (float)z * oneOverDimensions));
                }
            }

            for (int z = 0; z < verticesPerDimension - 1; ++z)
            {
                for (int x = 0; x < verticesPerDimension - 1; ++x)
                {
                    int ll = (z * verticesPerDimension) + x;
                    int lr = (z * verticesPerDimension) + x + 1;
                    int ur = ((z + 1) * verticesPerDimension) + x + 1;
                    int ul = ((z + 1) * verticesPerDimension) + x;

                    mesh.Indices.Add(ll);
                    mesh.Indices.Add(ul);
                    mesh.Indices.Add(lr);

                    mesh.Indices.Add(ul);
                    mesh.Indices.Add(ur);
                    mesh.Indices.Add(lr);
                }
            }

            mesh.Build();

            filter.mesh = mesh.Mesh;
            renderer.material = Instantiate(MeshMaterial);
        }

        /// <summary>
        /// Used to validate the control parameters of the algorithm.
        /// </summary>
        /// <returns></returns>
        private bool IsValidInput()
        {
            if (Dimensions < 3)
            {
                UnityEngine.Debug.LogError("Diamond Square noise dimensions too small - must be greater than 2.");
                return false;
            }

            if (!MathUtils.IsPowerOf2Plus1(Dimensions))
            {
                if (MathUtils.IsPowerOf2(Dimensions))
                {
                    Dimensions++;
                    UnityEngine.Debug.LogWarning("Dimensions set to power of 2 + 1.");
                }
                else
                {
                    UnityEngine.Debug.LogError("Diamond Square noise dimensions must be a power of 2 + 1.");
                    return false;
                }
            }

            return true;
        }
    }
}

