using UnityEngine;

namespace Nekuzaky.Vicinity
{
    internal static class VicinityLifetime
    {
        internal static void Destroy(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
                return;
            }

            Object.DestroyImmediate(target);
        }
    }
}
