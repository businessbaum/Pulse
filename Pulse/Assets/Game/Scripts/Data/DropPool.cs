using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pulse
{
    // Heart: repairs the machine. Beat: the currency, a piece of the Pulse. Part: a module for the
    // machine, with an ability; which module and how good is rolled when it drops. Echo: a choice
    // of Echoes for this run.
    public enum DropKind { Heart, Beat, Part, Echo }

    // What enemies leave behind when they are destroyed: hearts that repair the machine, and Beats.
    // New kinds are added as further entries.
    [CreateAssetMenu(menuName = "Pulse/Drop Pool")]
    public sealed class DropPool : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public DropKind kind;
            [Tooltip("Chance that an ordinary enemy leaves one of these.")]
            [Range(0f, 1f)] public float chance;
            [Tooltip("What one of these is worth. For a heart: hull repaired.")]
            public float amount;
            public Mesh mesh;
            [Tooltip("What its glowing faces are made of. This is its colour in the world.")]
            public Material material;
            [Tooltip("How many a mini boss, a boss and a Warden always leave.")]
            public int fromMiniBoss;
            public int fromBoss;
            public int fromWarden;
        }

        public Entry[] entries = Array.Empty<Entry>();

        // Adds what an enemy of this rank leaves to the list. roll returns a random number from 0 to 1.
        // Ordinary enemies roll once per entry; mini bosses, bosses and Wardens leave fixed numbers.
        public void Roll(Rank rank, Func<float> roll, List<Entry> into)
        {
            foreach (var entry in entries)
            {
                int count = rank == Rank.Warden ? entry.fromWarden
                    : rank == Rank.Boss ? entry.fromBoss
                    : rank == Rank.MiniBoss ? entry.fromMiniBoss
                    : roll() < entry.chance ? 1 : 0;
                for (int i = 0; i < count; i++) into.Add(entry);
            }
        }
    }
}
