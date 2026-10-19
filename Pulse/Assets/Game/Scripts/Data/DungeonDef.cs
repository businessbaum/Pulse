using System;
using UnityEngine;

namespace Pulse
{
    // A dungeon is a closed place fought in waves, on a map drawn for it. Its difficulty is fixed; it
    // does not scale with time. A dungeon with a boss is a Warden's arena: no waves, one fight.
    [CreateAssetMenu(menuName = "Pulse/Dungeon")]
    public sealed class DungeonDef : ScriptableObject
    {
        [Serializable]
        public struct Wave
        {
            public int grunts;
            public int brutes;
        }

        public string displayName;
        [Tooltip("The map, one row of text per row of cells. See DungeonMap for what each character means. Empty: a plain round arena.")]
        [TextArea(8, 30)] public string map;
        [Tooltip("Only used without a map: the radius of the round arena.")]
        public float arenaRadius = 24f;
        public Wave[] waves = Array.Empty<Wave>();

        [Header("What clearing it pays")]
        [Tooltip("Beats. A harder dungeon pays more.")]
        public int reward = 40;
        [Tooltip("Parts the player finds here the first time: modules, reactors, drives, frames.")]
        public ScriptableObject[] finds = Array.Empty<ScriptableObject>();

        [Header("A Warden's arena")]
        public EnemyDef boss;
        [Tooltip("The line above the Warden's name when it makes its entrance.")]
        public string bossTitle;
        [Tooltip("What its second phase is called.")]
        public string bossSecondName;

        [Header("Its entrance in the open world")]
        [Tooltip("What stands behind the entrance. Empty: the plain arch.")]
        public Mesh landmark;
        [Tooltip("What the landmark is made of. Left empty, it is the grey and chalk of a dungeon's arch.")]
        public Material landmarkStone;
        public Material landmarkTrim;
        [Tooltip("Where embers and smoke rise from the landmark, seen from its own origin: the tops of its chimneys.")]
        public Vector3[] landmarkFires = System.Array.Empty<Vector3>();
        [Tooltip("How far behind the entrance the landmark stands, and how much room is kept clear around it.")]
        public float landmarkDepth;
        public float landmarkRadius;
    }
}
