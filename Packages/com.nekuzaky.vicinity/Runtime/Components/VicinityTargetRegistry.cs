using System.Collections.Generic;

namespace Nekuzaky.Vicinity
{
    internal static class VicinityTargetRegistry
    {
        private static readonly List<VicinityTarget> _targets = new List<VicinityTarget>();

        internal static IReadOnlyList<VicinityTarget> Targets => _targets;

        internal static void Add(VicinityTarget target)
        {
            if (target != null && !_targets.Contains(target))
            {
                _targets.Add(target);
            }
        }

        internal static void Remove(VicinityTarget target)
        {
            _targets.Remove(target);
        }

        internal static void Clear()
        {
            _targets.Clear();
        }

        internal static VicinityTarget Best()
        {
            VicinityTarget best = null;

            for (int i = 0; i < _targets.Count; i++)
            {
                VicinityTarget candidate = _targets[i];
                if (candidate == null)
                {
                    continue;
                }

                if (best == null || candidate.Priority > best.Priority)
                {
                    best = candidate;
                }
            }

            return best;
        }
    }
}
