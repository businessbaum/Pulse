using System.Collections.Generic;
using UnityEngine;

namespace Pulse
{
    // Keeps track of the waves during one visit to a dungeon. No scene objects involved.
    public sealed class WaveRun
    {
        readonly DungeonDef def;

        public WaveRun(DungeonDef def) => this.def = def;

        // Index of the wave being fought; -1 before the first one.
        public int Wave { get; private set; } = -1;
        public int WaveCount => def.waves.Length;
        public bool Cleared => Wave >= def.waves.Length;

        // Moves on to the next wave. Returns false when none is left, which means the dungeon is cleared.
        public bool Next(out DungeonDef.Wave wave)
        {
            if (!Cleared) Wave++;
            wave = Cleared ? default : def.waves[Wave];
            return !Cleared;
        }
    }

    public static class WorldRules
    {
        // An entrance is open when it has not been cleared and the entrance it depends on has been.
        public static bool IsOpen(WorldDef world, int index, ICollection<int> cleared)
        {
            if (cleared.Contains(index)) return false;
            int requires = world.entrances[index].requires;
            return requires < 0 || cleared.Contains(requires);
        }

        // Which rank the next open-world spawn gets. roll is a random number from 0 to 1.
        // Bosses are rarer than mini bosses, and neither appears while enough of them are alive.
        public static Rank RollRank(WorldDef world, float roll, int miniBossesAlive, int bossesAlive)
        {
            if (roll < world.bossChance) return bossesAlive < world.maxBosses ? Rank.Boss : Rank.Normal;
            if (roll < world.bossChance + world.miniBossChance)
                return miniBossesAlive < world.maxMiniBosses ? Rank.MiniBoss : Rank.Normal;
            return Rank.Normal;
        }

        // How deep a point is in the storm around the map: 0 inside the map, 1 at the storm's far end.
        public static float StormDepth(WorldDef world, Vector3 pos)
        {
            if (world.stormDepth <= 0f) return 0f;
            float beyond = Mathf.Max(Mathf.Abs(pos.x), Mathf.Abs(pos.z)) - world.halfSize;
            return Mathf.Clamp01(beyond / world.stormDepth);
        }

        // Hull the storm takes per second at that depth: nothing outside it, more the deeper in.
        public static float StormDamage(WorldDef world, float depth) =>
            depth <= 0f ? 0f : Mathf.Lerp(world.stormDamageAtEdge, world.stormDamageAtEnd, depth);
    }
}
