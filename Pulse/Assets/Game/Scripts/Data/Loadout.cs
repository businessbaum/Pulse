using System;
using UnityEngine;

namespace Pulse
{
    // One complete machine: frame, reactor, drive and what sits in each slot.
    [CreateAssetMenu(menuName = "Pulse/Loadout")]
    public sealed class Loadout : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string slot;
            public ModuleDef module;
            // Which of the player's parts it is: that decides its level and its ability. Empty on a
            // machine that is only a list of kinds, such as the starting machines in the project.
            [NonSerialized] public Item item;
        }

        public string displayName;
        public FrameDef frame;
        public ReactorDef reactor;
        public DriveDef drive;
        public Entry[] modules = Array.Empty<Entry>();

        public ModuleDef In(string slot)
        {
            foreach (var entry in modules)
                if (entry.slot == slot) return entry.module;
            return null;
        }

        public Item ItemIn(string slot)
        {
            foreach (var entry in modules)
                if (entry.slot == slot) return entry.item;
            return null;
        }

        public int PowerUsed
        {
            get
            {
                int sum = 0;
                foreach (var entry in modules)
                    if (entry.module != null) sum += entry.module.powerCost;
                return sum;
            }
        }
    }
}
