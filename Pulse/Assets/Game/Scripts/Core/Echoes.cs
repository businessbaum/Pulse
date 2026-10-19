using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pulse
{
    // The Echoes the machine holds in this run. An Echo bends one rule until the run ends; a run
    // holds at most three. Each rule is checked where it applies (a weapon asks for its damage, the
    // pickups ask what a Beat is worth): this only says which are in force, and keeps the few
    // numbers that belong to an Echo and to nothing else.
    //
    // A run gives a choice of Echoes each time something large is beaten: a dungeon, a boss of the
    // open world, a Warden. What is offered comes from the pool, which grows as dungeons are
    // cleared for the first time.
    public static class Echoes
    {
        public const int Max = 3, Choices = 3;
        // What an Echo is worth in Beats to a machine that cannot hold another.
        public const int Spare = 25;
        // Undertow: from how far drops fly in. Double Time: how much faster weapons fire.
        public const float MagnetReach = 9f, Haste = 1.25f;
        // Crescendo: damage added by each kill, and how many kills count. Afterimage: every how many hits.
        public const float CrescendoStep = 0.01f;
        public const int CrescendoCap = 40, AfterimageEvery = 10;
        // Encore: the share of its hull the machine is left with.
        public const float EncoreHull = 0.3f;

        public static readonly List<EchoDef> Held = new List<EchoDef>(Max);
        static readonly List<EchoDef> pool = new List<EchoDef>();
        static int mask, hits;
        static bool encoreSpent;

        // Choices the machine has earned and not made yet.
        public static int Owed;
        // Crescendo: kills since the machine last lost hull, up to the cap.
        public static int Streak { get; private set; }

        public static bool Has(EchoKind kind) => (mask & (1 << (int)kind)) != 0;
        // Whether there is room for another, counting the choices still to be made.
        public static bool Full => Held.Count + Owed >= Max;
        public static float Damage => Has(EchoKind.Crescendo) ? 1f + CrescendoStep * Streak : 1f;
        public static float Rate => Has(EchoKind.DoubleTime) ? Haste : 1f;
        public static bool EncoreReady => Has(EchoKind.Encore) && !encoreSpent;

        public static void Reset()
        {
            Held.Clear();
            mask = hits = Owed = Streak = 0;
            encoreSpent = false;
        }

        // The machine takes an Echo. Returns false when it holds it already or has no room.
        public static bool Take(EchoDef echo)
        {
            if (echo == null || Held.Count >= Max || Held.Contains(echo)) return false;
            Held.Add(echo);
            mask |= 1 << (int)echo.kind;
            return true;
        }

        public static void Kill()
        {
            if (Streak < CrescendoCap) Streak++;
        }

        public static void Hurt() => Streak = 0;

        // Uses the Encore up. True once in a run, for a machine that holds it.
        public static bool Encore()
        {
            if (!EncoreReady) return false;
            encoreSpent = true;
            return true;
        }

        // Counts one hit. True on every tenth, for a machine that holds Afterimage.
        public static bool Tenth()
        {
            if (!Has(EchoKind.Afterimage) || ++hits < AfterimageEvery) return false;
            hits = 0;
            return true;
        }

        // Whether an Echo is in the pool: one that belongs to a dungeon joins once that dungeon has
        // been cleared for the first time.
        public static bool InPool(EchoDef echo) => echo != null && (echo.joinsAfter == null || Profile.HasCleared(echo.joinsAfter));

        // Whether an Echo would change anything for this machine. A Long Wire is nothing to a
        // machine whose systems reach every weapon already.
        public static bool Useful(EchoDef echo, Loadout loadout) =>
            echo.kind != EchoKind.LongWire || Wiring.GainsFromLongWire(loadout);

        // Up to three Echoes to choose from: in the pool, not held, of use. roll returns random
        // numbers from 0 to 1. Empty when nothing is left to offer.
        public static List<EchoDef> Offer(GameConfig config, Loadout loadout, Func<float> roll)
        {
            pool.Clear();
            if (config.echoes != null)
                foreach (var echo in config.echoes)
                    if (InPool(echo) && !Held.Contains(echo) && Useful(echo, loadout)) pool.Add(echo);
            var offer = new List<EchoDef>(Choices);
            while (offer.Count < Choices && pool.Count > 0)
            {
                int pick = Mathf.Min((int)(roll() * pool.Count), pool.Count - 1);
                offer.Add(pool[pick]);
                pool.RemoveAt(pick);
            }
            return offer;
        }
    }
}
