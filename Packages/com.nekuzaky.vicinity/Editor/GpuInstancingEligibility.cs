using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nekuzaky.Vicinity.Editor
{
    internal struct GpuInstancingExclusion
    {
        internal string Reason;
        internal Object Context;
    }

    internal static class GpuInstancingEligibility
    {
        #region Main Methods

        internal static void Collect(GameObject host, List<GpuInstancingExclusion> exclusions)
        {
            if (host == null)
            {
                return;
            }

            CheckOptOutComponent(host, exclusions);
            CheckRenderers(host, exclusions);
            CheckScripts(host, exclusions);
        }

        internal static bool SceneUsesRealtimeGlobalIllumination() => Lightmapping.realtimeGI;

        internal static bool IsExcluded(GameObject host)
        {
            List<GpuInstancingExclusion> exclusions = new List<GpuInstancingExclusion>();
            Collect(host, exclusions);
            return exclusions.Count > 0;
        }

        #endregion

        #region Privates

        private const string OptOutComponentName = "DisallowGPUDrivenRendering";

        private static readonly string[] PerInstanceCallbacks =
        {
            "OnRenderObject",
            "OnWillRenderObject",
            "OnBecameVisible",
            "OnBecameInvisible"
        };

        private static void CheckOptOutComponent(GameObject host, List<GpuInstancingExclusion> exclusions)
        {
            foreach (Component component in host.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().Name != OptOutComponentName)
                {
                    continue;
                }

                exclusions.Add(new GpuInstancingExclusion
                {
                    Reason = "it carries a Disallow GPU Driven Rendering component, which opts it out on purpose",
                    Context = component
                });

                return;
            }
        }

        private static void CheckRenderers(GameObject host, List<GpuInstancingExclusion> exclusions)
        {
            foreach (Renderer renderer in host.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                if (renderer is not MeshRenderer)
                {
                    exclusions.Add(new GpuInstancingExclusion
                    {
                        Reason = $"'{renderer.GetType().Name}' is not a Mesh Renderer, and only Mesh Renderers are drawn on the GPU",
                        Context = renderer
                    });

                    continue;
                }

                if (renderer.HasPropertyBlock())
                {
                    exclusions.Add(new GpuInstancingExclusion
                    {
                        Reason = "a renderer carries a material property block",
                        Context = renderer
                    });
                }

                if (renderer.lightProbeUsage == LightProbeUsage.UseProxyVolume)
                {
                    exclusions.Add(new GpuInstancingExclusion
                    {
                        Reason = "a renderer uses a Light Probe Proxy Volume",
                        Context = renderer
                    });
                }
            }
        }

        private static void CheckScripts(GameObject host, List<GpuInstancingExclusion> exclusions)
        {
            foreach (MonoBehaviour behaviour in host.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                System.Type type = behaviour.GetType();

                foreach (string callback in PerInstanceCallbacks)
                {
                    MethodInfo method = type.GetMethod(
                        callback,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                    if (method == null)
                    {
                        continue;
                    }

                    exclusions.Add(new GpuInstancingExclusion
                    {
                        Reason = $"the script '{type.Name}' implements {callback}, a per-instance callback",
                        Context = behaviour
                    });

                    break;
                }
            }
        }

        #endregion
    }
}
