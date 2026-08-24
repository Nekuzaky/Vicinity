using Nekuzaky.Vicinity.Graph;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor
{
    internal struct DistancePreview
    {
        internal float LoadDistance;
        internal float ReleaseDistance;
        internal string Source;
    }

    internal static class VicinityDistancePreview
    {
        #region Main Methods

        internal static DistancePreview Resolve(VicinityObject managed)
        {
            if (managed == null)
            {
                return Built(ResidencySettings.DefaultLoadDistance, ResidencySettings.DefaultUnloadDistance, "nothing");
            }

            if (managed.OverridesDistances)
            {
                return Built(managed.LoadDistance, managed.UnloadDistance, "this object's own distances");
            }

            VicinityVolume covering = VicinityVolume.FindCovering(managed.transform.position);
            VicinityProfile profile = covering != null && covering.Profile != null
                ? covering.Profile
                : VicinitySceneSetup.FindManager()?.Profile;

            if (profile == null)
            {
                return Built(
                    ResidencySettings.DefaultLoadDistance,
                    ResidencySettings.DefaultUnloadDistance,
                    "Vicinity's built-in values, because no profile is assigned");
            }

            string place = covering != null && covering.Profile != null
                ? $"the volume '{covering.name}'"
                : "the manager";

            return ResolveThroughProfile(managed, profile, place);
        }

        #endregion

        #region Privates

        private static DistancePreview ResolveThroughProfile(VicinityObject managed, VicinityProfile profile, string place)
        {
            float load = profile.LoadDistance;
            float release = profile.UnloadDistance;

            if (profile.ResidencyGraph == null)
            {
                return Built(load, release, $"the profile '{profile.name}' on {place}");
            }

            CompiledResidencyRules compiled = profile.ResidencyGraph.Compile();

            if (!compiled.IsValid)
            {
                compiled.Dispose();
                return Built(load, release, $"the profile '{profile.name}', because its graph does not compile");
            }

            ObjectFacts facts = new ObjectFacts
            {
                SizeMeters = managed.BoundsRadius * 2f,
                MemoryMegabytes = managed.EstimatedMemoryBytes / (1024f * 1024f),
                TagMatch = MatchesTag(managed, compiled.Tag) ? 1f : 0f
            };

            ResolvedRule fallback = new ResolvedRule
            {
                LoadDistance = load,
                ReleaseDistance = release,
                PriorityScale = 1f
            };

            ResolvedRule rule = compiled.Evaluate(facts, fallback);
            compiled.Dispose();

            return Built(
                rule.LoadDistance,
                rule.ReleaseDistance,
                $"the graph '{profile.ResidencyGraph.name}' on {place}");
        }

        private static bool MatchesTag(VicinityObject managed, string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return false;
            }

            try
            {
                return managed.CompareTag(tag);
            }
            catch (UnityException)
            {
                return false;
            }
        }

        private static DistancePreview Built(float load, float release, string source)
        {
            return new DistancePreview
            {
                LoadDistance = load,
                ReleaseDistance = release,
                Source = source
            };
        }

        #endregion
    }
}
