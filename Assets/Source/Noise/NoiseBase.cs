using UnityEngine;

namespace VertexFragment
{
    public abstract class NoiseBase
    {
        /// <summary>
        /// The random seed used by the noise function.
        /// </summary>
        public virtual int Seed { get; set; } = 1337;

        /// <summary>
        /// Does this noise generator require pre-generation?
        /// </summary>
        public virtual bool RequiresPreGeneration { get; } = false;

        /// <summary>
        /// For implementations where <see cref="RequiresPreGeneration"/> is true.
        /// </summary>
        /// <param name="threadPool"></param>
        /// <param name="threadsPerDimension"></param>
        /// <returns></returns>
        public virtual bool Generate(ThreadPool threadPool)
        {
            return true;
        }

        /// <summary>
        /// Pre-fills the noise from the seed texture.
        /// </summary>
        /// <param name="source"></param>
        public abstract void SetHeightSeedsFromTexture(Texture2D source);

        /// <summary>
        /// Retrieves the noise value, on the range <c>[0.0, 1.0]</c>, for the specified 2D position.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public abstract float GetValue(float x, float y);

        /// <summary>
        /// Retrieves the noise value, on the range <c>[0.0, 1.0]</c>, for the specified 3D position.
        /// Note that not all generator support 3D noise and may simply return 2D.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <returns></returns>
        public abstract float GetValue(float x, float y, float z);
    }
}