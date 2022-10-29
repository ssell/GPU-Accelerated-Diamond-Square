using UnityEngine;
using UnityEngine.UI;

namespace VertexFragment
{
    public sealed class DiamondSquareDemo : MonoBehaviour
    {
        // Controls
        public bool Generate = false;

        // Demo Scene
        public RawImage Noise2D;
        public GameObject Noise3D;

        // Input
        public Texture2D SeedTexture;
        public int Dimensions = 512;
        public float Amplitude = 1.0f;
        public float Persistence = 0.5f;
        public bool UseGpuAcceleration = false;

        void Start()
        {

        }

        void Update()
        {

        }
    }
}

