using System;
using UnityEngine;

namespace Pulse
{
    // What a world looks like: its biomes with their floor colours and props, the rim of the map
    // and the walls of a dungeon.
    [CreateAssetMenu(menuName = "Pulse/Scenery")]
    public sealed class SceneryDef : ScriptableObject
    {
        [Serializable]
        public struct Prop
        {
            public string name;
            public Mesh mesh;
            public Material material;
            [Tooltip("Second material slot of the model. Falls back to the main material.")]
            public Material accent;
            public int count;
            public float minScale;
            public float maxScale;
            [Tooltip("Radius that blocks the machine and enemies, at scale 1. Zero means it can be driven through.")]
            public float blockRadius;
            [Tooltip("Lies flat on the ground, like a crack or a pool.")]
            public bool flat;
            [Tooltip("Largest random lean in degrees.")]
            public float tilt;
            [Tooltip("Small clutter does not need a shadow.")]
            public bool noShadow;
            [Tooltip("Flattened when the machine drives over it.")]
            public bool crushable;
            [Tooltip("Rock and ice. It does not shake when hit at speed; the machine bounces off it instead.")]
            public bool hard;
        }

        [Serializable]
        public sealed class Biome
        {
            public string name;
            [Tooltip("Most of the floor.")]
            public Color ground = Color.gray;
            [Tooltip("Mixed into the floor in patches.")]
            public Color groundAlt = Color.gray;
            [Tooltip("Rare spots.")]
            public Color groundSpot = Color.white;
            public Color ambient = Color.gray;
            [Tooltip("What the glowing parts of this biome's props are made of.")]
            public Material glow;
            [Tooltip("What the vents in the floor of a dungeon under this biome throw up.")]
            public StateType hazard = StateType.Heat;
            public Prop[] props = Array.Empty<Prop>();
            [Tooltip("Who lives here. The open world spawns these while the machine is in this biome.")]
            public EnemyDef[] species = Array.Empty<EnemyDef>();
        }

        [Tooltip("Raised by 'Create Default Content' when it brings an older asset up to date.")]
        public int version;
        public int seed = 7;
        public Biome[] biomes = Array.Empty<Biome>();
        [Tooltip("Placed along the edge of the world.")]
        public Prop border;
        public float borderSpacing = 6f;
        [Tooltip("Placed around a dungeon arena.")]
        public Prop pillar;
        public float pillarSpacing = 5f;
        [Tooltip("One block of dungeon wall, the size of one cell of a dungeon map.")]
        public Prop wall;
        [Tooltip("One block of the stands around a Warden's arena.")]
        public Prop stand;
        [Tooltip("One leaf of the gate a Warden comes through.")]
        public Mesh bossGate;
        public Mesh gate;
    }
}
