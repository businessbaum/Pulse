using System;
using UnityEngine;

namespace Pulse
{
    // Every model from the Blender script has the same four material slots, in this order.
    public static class Slots
    {
        public static Material[] For(Mesh mesh, Material baseMaterial, Material accent, Material dark, Material glow)
        {
            accent = Or(accent, baseMaterial);
            var all = new[] { baseMaterial, accent, Or(dark, baseMaterial), Or(glow, accent) };
            if (mesh.subMeshCount >= all.Length) return all;
            var fewer = new Material[mesh.subMeshCount];
            Array.Copy(all, fewer, fewer.Length);
            return fewer;
        }

        // An empty material field loaded from an asset is not a plain null, so `??` does not catch it.
        // This does.
        public static Material Or(Material wanted, Material fallback) => wanted != null ? wanted : fallback;
    }

    // How the next run starts. Which machine it is played with is in Profile.
    public static class RunSetup
    {
        // Set when the run starts through the lobby gate: the machine then races into the open world.
        public static bool Arriving;
        // Set when a run ends through the teleporter: the machine then appears on the turntable.
        public static bool Returning;
        // How many Beats the machine brought home with it that time.
        public static int Brought;
    }

    // The open world is split into sectors around the starting point, one per biome, with wavy borders.
    public static class Biomes
    {
        public static int At(Vector3 pos, int count)
        {
            if (count <= 1) return 0;
            float dist = Mathf.Sqrt(pos.x * pos.x + pos.z * pos.z);
            float angle = Mathf.Atan2(pos.z, pos.x) * Mathf.Rad2Deg;
            angle += Mathf.Sin(dist * 0.07f) * 14f + Mathf.Sin(dist * 0.021f + 1.3f) * 9f;
            // The turn of five degrees puts the third of three biomes squarely to the south, where
            // the machine arrives, the first to the north-east and the second to the west.
            return Mathf.FloorToInt(Mathf.Repeat(angle + 5f, 360f) / (360f / count)) % count;
        }
    }
}
