using System;
using UnityEngine;

namespace Pulse
{
    // The open world of a run: its size, its dungeon entrances and its constant enemy spawns.
    [CreateAssetMenu(menuName = "Pulse/World")]
    public sealed class WorldDef : ScriptableObject
    {
        [Serializable]
        public struct Entrance
        {
            public DungeonDef dungeon;
            public Vector3 position;
            [Tooltip("Index of the entrance that must be cleared first, or -1 if this one is open from the start.")]
            public int requires;
        }

        public float halfSize = 120f;
        public Entrance[] entrances = Array.Empty<Entrance>();

        [Header("Open world spawns: a constant rate that never ramps up")]
        public float spawnPerSecond = 1.2f;
        public int maxEnemies = 45;
        public float spawnDistance = 28f;
        [Tooltip("Enemies farther away than this are removed.")]
        public float despawnDistance = 60f;
        [Tooltip("Only used where a biome lists no species of its own.")]
        [Range(0f, 1f)] public float bruteChance = 0.1f;

        [Header("Mini bosses and bosses: chance per spawn, and how many may be alive at once")]
        [Range(0f, 1f)] public float miniBossChance = 0.03f;
        [Range(0f, 1f)] public float bossChance = 0.008f;
        public int maxMiniBosses = 2;
        public int maxBosses = 1;

        [Header("The storm at the edge of the map")]
        [Tooltip("How far past the edge the machine can drive. Zero switches the storm off and stops the machine at the edge.")]
        public float stormDepth = 24f;
        [Tooltip("Hull lost per second at the edge of the storm and at its far end. In between it rises evenly.")]
        public float stormDamageAtEdge = 3f;
        public float stormDamageAtEnd = 40f;
        [Tooltip("Enemies in the open world ignore the machine until it comes this close. After that they follow it for good.")]
        public float aggroRange = 14f;
    }
}
