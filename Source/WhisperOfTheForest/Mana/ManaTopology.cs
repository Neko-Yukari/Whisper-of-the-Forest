using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Mana
{
    /// <summary>The link rule of the mana graph, free of map and comp state.</summary>
    public static class ManaTopology
    {
        /// <summary>
        /// Two nodes link when they are no farther apart than the smaller of their two link radii, and
        /// links chain. Returns each node's network index, numbered in order of first appearance.
        /// </summary>
        public static int[] Group(IReadOnlyList<IntVec3> positions, IReadOnlyList<float> linkRadii)
        {
            int[] parent = new int[positions.Count];
            for (int i = 0; i < parent.Length; i++)
            {
                parent[i] = i;
            }

            for (int i = 0; i < parent.Length; i++)
            {
                for (int j = i + 1; j < parent.Length; j++)
                {
                    float reach = linkRadii[i] < linkRadii[j] ? linkRadii[i] : linkRadii[j];
                    if ((positions[i] - positions[j]).LengthHorizontalSquared <= reach * reach)
                    {
                        Union(parent, i, j);
                    }
                }
            }

            int[] group = new int[parent.Length];
            var byRoot = new Dictionary<int, int>();
            for (int i = 0; i < parent.Length; i++)
            {
                int root = Find(parent, i);
                if (!byRoot.TryGetValue(root, out int index))
                {
                    index = byRoot.Count;
                    byRoot.Add(root, index);
                }

                group[i] = index;
            }

            return group;
        }

        private static int Find(int[] parent, int index)
        {
            while (parent[index] != index)
            {
                parent[index] = parent[parent[index]];
                index = parent[index];
            }

            return index;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int rootA = Find(parent, a);
            int rootB = Find(parent, b);
            if (rootA != rootB)
            {
                parent[rootB] = rootA;
            }
        }
    }
}
