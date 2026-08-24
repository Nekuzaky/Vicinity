using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Nekuzaky.Vicinity.Editor
{
    internal sealed class ProjectCheck
    {
        internal string Title;
        internal string Explanation;
        internal bool IsSatisfied;
        internal bool IsAdvisory;
        internal string FixLabel;
        internal Action Fix;
    }

    internal static class VicinityProjectChecks
    {
        #region Main Methods

        internal static bool AddressablesInstalled
        {
            get
            {
#if VICINITY_ADDRESSABLES
                return true;
#else
                return false;
#endif
            }
        }

        internal static List<ProjectCheck> Collect()
        {
            List<ProjectCheck> checks = new List<ProjectCheck>();
            UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

            checks.Add(BuildRenderPipelineCheck(urp));
            checks.Add(BuildSrpBatcherCheck(urp));
            checks.Add(BuildGpuResidentDrawerCheck(urp));
            checks.Add(BuildMipmapStreamingCheck());
            checks.Add(BuildAddressablesCheck());

            return checks;
        }

        #endregion

        #region Privates

        private static ProjectCheck BuildRenderPipelineCheck(UniversalRenderPipelineAsset urp)
        {
            return new ProjectCheck
            {
                Title = "Universal Render Pipeline",
                Explanation = urp != null
                    ? "This project renders with URP, which is what Vicinity supports."
                    : "Vicinity only supports the Universal Render Pipeline in this version. Assign a URP asset in Graphics settings.",
                IsSatisfied = urp != null,
                FixLabel = urp != null ? null : "Open Graphics settings",
                Fix = urp != null ? null : static () => SettingsService.OpenProjectSettings("Project/Graphics")
            };
        }

        private static ProjectCheck BuildSrpBatcherCheck(UniversalRenderPipelineAsset urp)
        {
            bool enabled = urp != null && urp.useSRPBatcher;

            return new ProjectCheck
            {
                Title = "SRP Batcher",
                Explanation = enabled
                    ? "The SRP Batcher is on, which keeps draw calls cheap as objects appear and disappear."
                    : "The SRP Batcher is off. Loading and releasing objects will cost far more CPU than it should, and the GPU Resident Drawer cannot run without it.",
                IsSatisfied = enabled,
                FixLabel = urp == null || enabled ? null : "Turn the SRP Batcher on",
                Fix = urp == null || enabled ? null : () =>
                {
                    Undo.RecordObject(urp, "Enable SRP Batcher");
                    urp.useSRPBatcher = true;
                    EditorUtility.SetDirty(urp);
                }
            };
        }

        private static ProjectCheck BuildGpuResidentDrawerCheck(UniversalRenderPipelineAsset urp)
        {
            bool supported = IGPUResidentRenderPipeline.IsGPUResidentDrawerSupportedByProjectConfiguration();
            bool requested = urp != null && urp.gpuResidentDrawerMode != GPUResidentDrawerMode.Disabled;

            string explanation;
            if (!supported)
            {
                explanation = "This project cannot run the GPU Resident Drawer. It needs the Forward+ or Deferred+ rendering path, the SRP Batcher, BatchRendererGroup Variants set to Keep All, and a graphics API that supports compute shaders. Vicinity works without it, you just lose GPU instancing.";
            }
            else if (!requested)
            {
                explanation = "The GPU Resident Drawer is available but switched off. Turning it on makes the objects Vicinity loads much cheaper to draw.";
            }
            else
            {
                explanation = "The GPU Resident Drawer is on. Vicinity never uses material property blocks or visibility callbacks, so the objects it manages stay eligible for it.";
            }

            return new ProjectCheck
            {
                Title = "GPU Resident Drawer",
                Explanation = explanation,
                IsSatisfied = supported && requested,
                IsAdvisory = true,
                FixLabel = supported && !requested && urp != null ? "Turn it on" : null,
                Fix = supported && !requested && urp != null
                    ? () =>
                    {
                        Undo.RecordObject(urp, "Enable GPU Resident Drawer");
                        urp.gpuResidentDrawerMode = GPUResidentDrawerMode.InstancedDrawing;
                        EditorUtility.SetDirty(urp);
                    }
                    : null
            };
        }

        private static ProjectCheck BuildMipmapStreamingCheck()
        {
            bool enabled = QualitySettings.streamingMipmapsActive;

            return new ProjectCheck
            {
                Title = "Texture Mipmap Streaming",
                Explanation = enabled
                    ? "Mipmap Streaming is on. Vicinity handles models; Unity handles textures."
                    : "Mipmap Streaming is off, so every texture loads at full size no matter how far away it is. Vicinity does not stream textures, this is the built-in system that does.",
                IsSatisfied = enabled,
                IsAdvisory = true,
                FixLabel = enabled ? null : "Turn Mipmap Streaming on",
                Fix = enabled ? null : static () => QualitySettings.streamingMipmapsActive = true
            };
        }

        private static ProjectCheck BuildAddressablesCheck()
        {
            bool installed = AddressablesInstalled;

            return new ProjectCheck
            {
                Title = "Addressables",
                Explanation = installed
                    ? "Addressables is installed, so Vicinity can load addressable assets as well as direct references."
                    : "Addressables is not installed. Vicinity works fine without it, using direct references and Resources. Install it only if your project already uses addressable assets.",
                IsSatisfied = installed,
                IsAdvisory = true,
                FixLabel = installed ? null : "Install Addressables",
                Fix = installed ? null : static () => Client.Add(AddressablesPackageName)
            };
        }

        private const string AddressablesPackageName = "com.unity.addressables";

        #endregion
    }
}
