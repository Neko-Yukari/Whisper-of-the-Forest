using System.Collections.Generic;

namespace WhisperOfTheForest.Mana
{
    /// <summary>
    /// One connected group of mana nodes. The pool lives in the attached batteries, so it saves with
    /// the map; this object is only the link graph and the transfer rules over it.
    /// </summary>
    public sealed class ManaNetwork
    {
        public readonly List<CompManaNode> nodes = new List<CompManaNode>();
        internal readonly List<CompManaBattery> batteries = new List<CompManaBattery>();

        public float Capacity
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < batteries.Count; i++)
                {
                    total += batteries[i].Capacity;
                }

                return total;
            }
        }

        public float Stored
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < batteries.Count; i++)
                {
                    total += batteries[i].Stored;
                }

                return total;
            }
        }

        /// <summary>Adds what fits and returns the amount actually stored.</summary>
        public float Add(float amount)
        {
            if (amount <= 0f)
            {
                return 0f;
            }

            float remaining = amount;
            for (int i = 0; i < batteries.Count && remaining > 0.0001f; i++)
            {
                CompManaBattery node = batteries[i];
                float room = node.Capacity - node.Stored;
                if (room <= 0f)
                {
                    continue;
                }

                float added = room < remaining ? room : remaining;
                node.SetStored(node.Stored + added);
                remaining -= added;
            }

            return amount - remaining;
        }

        /// <summary>Draws the whole amount or nothing, so a consumer never half-spends a cast.</summary>
        public bool TryDraw(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (Stored + 0.0001f < amount)
            {
                return false;
            }

            float remaining = amount;
            for (int i = 0; i < batteries.Count; i++)
            {
                CompManaBattery node = batteries[i];
                float taken = node.Stored < remaining ? node.Stored : remaining;
                if (taken <= 0f)
                {
                    continue;
                }

                node.SetStored(node.Stored - taken);
                remaining -= taken;
                if (remaining <= 0.0001f)
                {
                    break;
                }
            }

            return true;
        }
    }
}
