using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pulse
{
    // The floor of a dungeon entrance giving way: a dark pit, covered by slabs that crack apart
    // from the middle outward and fall in.
    public sealed class Crumble : MonoBehaviour
    {
        struct Slab
        {
            public Vector3 pos;
            public float delay, yaw, spin;
            public bool dropped;
        }

        // Spread: how long the break takes to reach the rim. FallTime: how long one slab takes to vanish.
        const float SlabSize = 0.78f, Spread = 0.5f, FallTime = 0.42f, Lift = 0.1f;

        readonly List<Slab> slabs = new List<Slab>();
        Matrix4x4[] matrices = new Matrix4x4[128];
        Material pit;
        Mesh cube, disc;
        Vector3 center;
        float radius, started;
        bool active;

        void Awake()
        {
            cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            disc = ZoneSystem.BuildDisc(0f);
            // The colour of the dark around the world, unlit. Taken from the particle material so the
            // shader is always in a build. It has to be exactly that colour: the machine falls from
            // this pit straight into the dark above the dungeon.
            pit = new Material(Combat.Config.particle) { color = Tokens.Soot };
        }

        public void Begin(Vector3 at, float size)
        {
            center = at;
            radius = size * 0.9f;
            started = Time.time;
            active = true;
            slabs.Clear();
            for (float x = -radius; x <= radius; x += SlabSize)
            for (float z = -radius; z <= radius; z += SlabSize)
            {
                float distance = Mathf.Sqrt(x * x + z * z);
                if (distance > radius) continue;
                slabs.Add(new Slab
                {
                    pos = new Vector3(x, 0f, z), delay = distance / radius * Spread + Random.Range(0f, 0.1f),
                    yaw = Random.Range(-7f, 7f), spin = Random.Range(-1f, 1f),
                });
            }
            if (matrices.Length < slabs.Count) matrices = new Matrix4x4[slabs.Count];
            Combat.Vfx.Shockwave(center, size, Tokens.Dust, 30, 0.35f);
        }

        // The floor is whole again.
        public void Clear() => active = false;

        void Update()
        {
            if (!active) return;
            float elapsed = Time.time - started;
            Graphics.RenderMesh(new RenderParams(pit), disc, 0,
                Matrix4x4.TRS(center + Vector3.up * (Lift - 0.02f), Quaternion.identity, new Vector3(radius, 1f, radius)));

            int drawn = 0;
            for (int i = 0; i < slabs.Count; i++)
            {
                var slab = slabs[i];
                float fall = Mathf.Clamp01((elapsed - slab.delay) / FallTime);
                if (fall >= 1f) continue;
                if (fall > 0f && !slab.dropped)
                {
                    slab.dropped = true;
                    slabs[i] = slab;
                    if (i % 3 == 0) Combat.Vfx.Burst(center + slab.pos + Vector3.up * 0.3f, Tokens.Dust, 2, 2.5f, 0.2f, 0.5f);
                }
                // A slab that has let go shrinks away, which from above reads as falling.
                float size = SlabSize * 0.93f * (1f - fall * fall);
                var turn = Quaternion.Euler(fall * 160f * slab.spin, slab.yaw + fall * 90f * slab.spin, fall * 110f);
                matrices[drawn++] = Matrix4x4.TRS(center + slab.pos * (1f - fall * 0.15f) + Vector3.up * Lift, turn,
                    new Vector3(size, 0.06f, size));
            }
            if (drawn == 0) return;
            var renderParams = new RenderParams(Combat.Config.floor)
            {
                shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true,
                worldBounds = new Bounds(center, Vector3.one * 40f),
            };
            Graphics.RenderMeshInstanced(renderParams, cube, 0, matrices, drawn);
        }
    }
}
