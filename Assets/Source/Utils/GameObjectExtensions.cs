using UnityEngine;

namespace VertexFragment
{
    public static class GameObjectExtensions
    {
        /// <summary>
        /// Gets the component in the <see cref="GameObject"/>. If it does not exist, add it.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="go"></param>
        /// <returns></returns>
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();

            if (component == null)
            {
                component = go.AddComponent<T>();
            }

            return component;
        }
    }
}
