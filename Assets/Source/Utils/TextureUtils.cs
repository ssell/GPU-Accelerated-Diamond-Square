using System.IO;
using UnityEngine;

namespace VertexFragment
{
    public static class TextureUtils
    {
        /// <summary>
        /// Creates a new texture from a float array of values on the range <c>[0.0, 1.0]</c>.
        /// All components of the texels are set to the same value, with the exception of alpha always being 1.0.
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public static Texture2D FloatArrayToTexture(float[,] image, TextureFormat format = TextureFormat.RGBA32, bool mipmaps = false)
        {
            int width = image.GetLength(0);
            int height = image.GetLength(1);

            Texture2D texture = new Texture2D(width, height, format, mipmaps);
            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    float c = image[x, y];
                    pixels[x + (y * width)] = new Color(c, c, c, 1.0f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return texture;
        }

        /// <summary>
        /// Saves the texture as a PNG file on disk. Make sure <see cref="Texture2D.Apply"/> was called.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="path"></param>
        public static void SavePng(Texture2D source, string path)
        {
            if (!path.EndsWith(".png"))
            {
                path = $"{path}.png";
            }

            var fileInfo = new FileInfo(path);
            fileInfo.Directory.Create();

            Debug.Log($"Saving texture to '{path}' ...");

            var bytes = source.EncodeToPNG();

            File.WriteAllBytes(path, bytes);
        }
    }
}
