using System.Collections.Generic;
using UnityEngine;

namespace Pulse
{
    // The systems of the current run. GameRoot fills this in when a run starts.
    public static class Combat
    {
        public static GameConfig Config;
        public static EnemySystem Enemies;
        public static ZoneSystem Zones;
        public static ProjectileSystem Projectiles;
        public static Fx Fx;
        public static Vfx Vfx;
        public static Marks Marks;
        public static PickupSystem Pickups;
        public static Storm Storm;
        public static Scenery Scenery;
        public static Finds Finds;
        public static Rig Rig;
        public static RunDirector Director;
        public static Recall Recall;
        public static CameraRig Camera;

        public static float Time;
        public static int Kills;
        public static int ReactionCount;

        public static void Reset()
        {
            Time = 0f;
            Kills = 0;
            ReactionCount = 0;
            RunStats.Reset();
            Loot.Reset();
            Echoes.Reset();
        }
    }

    // What the machine brought in, for the log on the HUD: Beats and parts. A heart, or any other
    // power-up, is used up on the spot and is not listed.
    public static class Loot
    {
        public struct Entry
        {
            // The part, and the item when it is one of the player's own now. Null: Beats.
            public ScriptableObject part;
            public Item item;
            public int beats;
        }

        // Not shown yet. The log takes them out.
        public static readonly Queue<Entry> Waiting = new Queue<Entry>();

        public static void Beats(int amount)
        {
            if (amount > 0) Waiting.Enqueue(new Entry { beats = amount });
        }

        public static void Part(ScriptableObject part, Item item = null)
        {
            if (part != null) Waiting.Enqueue(new Entry { part = part, item = item });
        }

        public static void Reset() => Waiting.Clear();
    }

    // What the end-of-run screen reports, counted while the run goes on.
    //
    // Beats is more than a count: it is what the machine carries. Beats picked up in a run are not
    // the player's yet. They become the player's when the machine comes home through the
    // teleporter, and they are gone when it is destroyed.
    public static class RunStats
    {
        public static float DamageDealt, DamageTaken, Distance;
        public static int MiniBosses, Bosses, Flattened, Firestorms, Flashes, Plasmas, Hearts, Beats, Parts;
        public static int ThermalShocks, Superconductors, BlackIces, EchoCount, Veins, Caches;
        // Who or what landed the last hit on the machine.
        public static string LastHitBy;

        public static void Reset()
        {
            DamageDealt = DamageTaken = Distance = 0f;
            MiniBosses = Bosses = Flattened = Firestorms = Flashes = Plasmas = Hearts = Beats = Parts = 0;
            ThermalShocks = Superconductors = BlackIces = EchoCount = Veins = Caches = 0;
            LastHitBy = null;
        }
    }
}
