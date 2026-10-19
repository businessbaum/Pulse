using System.Collections.Generic;
using UnityEngine;

namespace Pulse
{
    // Areas on the ground that keep applying a state: oil pools, burning ground, frost.
    // An area is shown by what it is made of and nothing else: flames over scorched earth, a slick of
    // oil with bubbles, a crust of frost with ice in the air. There is no outline and no flat fill.
    // Zones made by enemies are hostile: they act on the machine as well. Fire burns it, frost and
    // oil slow it. They still act on enemies, so the machine can turn them against their makers.
    // A hostile zone throws up thin sparks in the colour enemies use for all their warnings.
    public sealed class ZoneSystem : MonoBehaviour
    {
        struct Zone
        {
            public StateType state;
            public Vector3 pos;
            public float radius, inner, left, tick, dps, owed;
            public bool hostile;
        }

        const int Max = 96, Segments = 48;
        const float Interval = 0.4f, RingInner = 0.72f;
        // Particles are fed in every Step seconds. An area thins out over its last FadeOut seconds.
        const float Step = 0.08f, FadeOut = 0.7f;
        // What a hostile zone does to the machine while it stands in it.
        const float FrostSlow = 0.55f, OilSlow = 0.62f, SlowHold = 0.3f, MinBurn = 6f;
        // A paler Heat for the hottest flames. Still Heat, so it is never mistaken for another state.
        static readonly Color FlameTip = Color.Lerp(Tokens.Heat, Color.white, 0.45f);
        static readonly Color Scorch = new Color(0.1f, 0.06f, 0.05f);
        static readonly Color Slick = Tokens.Oil * 0.5f;
        static readonly Color Rime = Color.Lerp(Tokens.Cryo, Color.white, 0.3f) * 0.75f;

        readonly List<Zone> zones = new List<Zone>(Max);
        readonly List<int> hits = new List<int>(128);

        public int Count => zones.Count;

        public void Clear() => zones.Clear();

        public void Spawn(StateType state, Vector3 pos, float radius, float duration, float dps = 0f, bool asRing = false, bool hostile = false)
        {
            if (zones.Count >= Max) zones.RemoveAt(0);
            pos.y = 0f;
            // Seep: oil the machine lays lies three times as long.
            if (state == StateType.Oil && !hostile && Echoes.Has(EchoKind.Seep)) duration *= 3f;
            var zone = new Zone
            {
                state = state, pos = pos, radius = radius, left = duration, dps = dps, hostile = hostile,
                inner = asRing ? radius * RingInner : 0f,
            };
            zones.Add(zone);
            // Enough at once that the whole area is there the moment it appears.
            for (int i = 0; i < 8; i++) Show(zone, 1f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var config = Combat.Config;

            for (int z = zones.Count - 1; z >= 0; z--)
            {
                var zone = zones[z];
                zone.left -= dt;
                zone.tick -= dt;
                if (zone.left <= 0f) { zones.RemoveAt(z); continue; }

                if (zone.tick <= 0f)
                {
                    zone.tick = Interval;
                    Combat.Enemies.InCircle(zone.pos, zone.radius, hits, zone.inner);
                    foreach (int index in hits)
                        Combat.Enemies.Hit(index, zone.dps * Interval, zone.state, ignoreArmor: true);
                    if (zone.hostile) TouchRig(zone);
                    if (zone.state == StateType.Heat) IgniteOil(z, zone, config);
                }

                zone.owed += dt;
                while (zone.owed >= Step)
                {
                    zone.owed -= Step;
                    Show(zone, Mathf.Clamp01(zone.left / FadeOut));
                }
                zones[z] = zone;
            }
        }

        // One helping of the particles an area is made of. strength thins it out as the area ends.
        static void Show(in Zone zone, float strength)
        {
            var vfx = Combat.Vfx;
            // Square metres, so a larger area is as dense as a small one.
            float area = Mathf.PI * (zone.radius * zone.radius - zone.inner * zone.inner) * strength * Step;
            switch (zone.state)
            {
                case StateType.Heat:
                    vfx.Ground(zone.pos, zone.radius, zone.inner, Scorch, Share(area * 1.3f), 0.7f, 0.9f);
                    vfx.Rise(zone.pos, zone.radius, zone.inner, Tokens.Heat, Share(area * 4.5f), smoky: true);
                    vfx.Rise(zone.pos, zone.radius, zone.inner, FlameTip, Share(area * 1.2f), 3f, 0.16f, 0.5f);
                    break;

                case StateType.Oil:
                    vfx.Ground(zone.pos, zone.radius, zone.inner, Slick, Share(area * 2.6f), 0.75f, 1.5f);
                    vfx.Ground(zone.pos, zone.radius, zone.inner, Tokens.Oil, Share(area * 0.5f), 0.3f, 0.8f);
                    vfx.Bubbles(zone.pos, zone.radius, zone.inner, Tokens.Oil, Share(area * 0.8f));
                    break;

                case StateType.Cryo:
                    vfx.Ground(zone.pos, zone.radius, zone.inner, Rime, Share(area * 2.4f), 0.6f, 1.3f);
                    vfx.Rise(zone.pos, zone.radius, zone.inner, Tokens.Cryo, Share(area * 1.1f), 0.7f, 0.12f, 0.7f);
                    break;

                default:
                    vfx.Rise(zone.pos, zone.radius, zone.inner, Tokens.Of(zone.state), Share(area * 2f), 3.5f, 0.1f, 0.3f);
                    break;
            }
            if (zone.hostile)
                vfx.Rise(zone.pos, zone.radius, zone.inner, Tokens.Alert, Share(area * 0.9f), 5f, 0.1f, 0.4f);
        }

        static int Share(float expected) => Vfx.Share(expected);

        // What a hostile zone does to the machine while it is inside.
        void TouchRig(in Zone zone)
        {
            var rig = Combat.Rig;
            if (rig == null || !rig.Exposed) return;
            Vector3 to = rig.transform.position - zone.pos;
            to.y = 0f;
            float reach = zone.radius + rig.Radius * 0.3f, sq = to.sqrMagnitude;
            if (sq > reach * reach || sq < zone.inner * zone.inner) return;
            switch (zone.state)
            {
                case StateType.Heat: rig.Scorch(Mathf.Max(zone.dps, MinBurn) * Interval, StateType.Heat, "Burning ground"); break;
                case StateType.Shock: rig.Scorch(Mathf.Max(zone.dps, MinBurn) * Interval, StateType.Shock, "Live ground"); break;
                case StateType.Cryo: rig.Slow(FrostSlow, Interval + SlowHold, StateType.Cryo); break;
                case StateType.Oil: rig.Slow(OilSlow, Interval + SlowHold, StateType.Oil); break;
            }
        }

        // Fire spreads to oil it touches. The delay makes the chain visible.
        void IgniteOil(int self, Zone fire, GameConfig config)
        {
            for (int o = 0; o < zones.Count; o++)
            {
                if (o == self || zones[o].state != StateType.Oil) continue;
                var oil = zones[o];
                float reach = fire.radius + oil.radius * 0.6f;
                if ((oil.pos - fire.pos).sqrMagnitude > reach * reach) continue;
                oil.state = StateType.Heat;
                oil.left = config.firestormDuration;
                oil.dps = config.firestormDps;
                oil.tick = Interval;
                zones[o] = oil;
            }
        }

        // A flat disc of radius 1 facing up. innerRatio > 0 leaves a hole in the middle.
        // Used for the things that really are round: a dungeon entrance and the pit under it.
        public static Mesh BuildDisc(float innerRatio)
        {
            var vertices = new Vector3[Segments * 2];
            var triangles = new int[Segments * 6];
            for (int s = 0; s < Segments; s++)
            {
                float angle = s * Mathf.PI * 2f / Segments;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[s * 2] = dir * innerRatio;
                vertices[s * 2 + 1] = dir;

                int i0 = s * 2, o0 = i0 + 1, i1 = (s + 1) % Segments * 2, o1 = i1 + 1, t = s * 6;
                triangles[t] = i0; triangles[t + 1] = o1; triangles[t + 2] = o0;
                triangles[t + 3] = i0; triangles[t + 4] = i1; triangles[t + 5] = o1;
            }
            var mesh = new Mesh { name = innerRatio > 0f ? "Ring" : "Disc", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
