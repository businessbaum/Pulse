using UnityEngine;
using UnityEngine.Rendering;

namespace Pulse
{
    // Particle effects made of small glowing cubes. Four kinds: floating cubes (flames, smoke, dust),
    // falling cubes (debris, casings, bubbles), streaks that point along their flight (sparks, trails)
    // and flat tiles that lie on the ground (oil, frost, scorched earth).
    // Particles are for what moves and spreads. A warning with an edge the player has to read, and
    // what a blow leaves on the floor, are flat shapes: see Marks.
    public sealed class Vfx : MonoBehaviour
    {
        static readonly Color Smoke = new Color(0.16f, 0.16f, 0.18f);

        ParticleSystem cubes, debris, streaks, tiles;

        void Awake()
        {
            cubes = Create("Cubes", 0f, false, false);
            debris = Create("Debris", 2.4f, false, false);
            streaks = Create("Streaks", 0.4f, true, false);
            tiles = Create("Tiles", 0f, true, true);
        }

        // Cubes that fly apart from one point. Falling ones are debris.
        public void Burst(Vector3 pos, Color color, int count, float speed, float size, float life, bool falls = false)
        {
            var emit = new ParticleSystem.EmitParams { startColor = color, position = pos };
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * (falls ? 1.4f : 0.5f);
                emit.velocity = dir * (speed * Random.Range(0.5f, 1f));
                emit.startSize = size * Random.Range(0.6f, 1.2f);
                emit.startLifetime = life * Random.Range(0.7f, 1.1f);
                emit.rotation3D = Random.rotation.eulerAngles;
                (falls ? debris : cubes).Emit(emit, 1);
            }
        }

        // Thin streaks that shoot away from a point, for impacts and electricity.
        public void Sparks(Vector3 pos, Color color, int count, float speed = 9f, float length = 0.45f, float life = 0.22f)
        {
            var emit = new ParticleSystem.EmitParams { startColor = color, position = pos };
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.7f;
                emit.velocity = dir * (speed * Random.Range(0.5f, 1f));
                emit.startSize3D = new Vector3(0.05f, 0.05f, length * Random.Range(0.6f, 1.2f));
                emit.startLifetime = life * Random.Range(0.7f, 1.2f);
                streaks.Emit(emit, 1);
            }
        }

        // One streak left behind by something that flies.
        public void Trail(Vector3 pos, Vector3 velocity, Color color, float length = 0.5f, float life = 0.18f)
        {
            streaks.Emit(new ParticleSystem.EmitParams
            {
                startColor = color, position = pos, velocity = velocity * 0.08f,
                startSize3D = new Vector3(0.07f, 0.07f, length), startLifetime = life,
            }, 1);
        }

        // Long streaks scattered through a box around a point, all flying the same way: the air
        // rushing past something that falls or races.
        public void Rush(Vector3 center, Vector3 spread, Vector3 velocity, Color color, int count, float length = 2.4f, float life = 0.5f)
        {
            var emit = new ParticleSystem.EmitParams { startColor = color, velocity = velocity, startLifetime = life };
            for (int i = 0; i < count; i++)
            {
                emit.position = center + new Vector3(Random.Range(-spread.x, spread.x), Random.Range(-spread.y, spread.y), Random.Range(-spread.z, spread.z));
                emit.startSize3D = new Vector3(0.06f, 0.06f, length * Random.Range(0.6f, 1.3f));
                streaks.Emit(emit, 1);
            }
        }

        // Cubes that rise from random spots inside a disc or ring: flames, embers, frost in the air.
        // smoky mixes in darker, slower cubes that start higher up.
        public void Rise(Vector3 center, float radius, float inner, Color color, int count, float speed = 2.2f, float size = 0.26f,
            float life = 0.75f, bool smoky = false)
        {
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector3 at = InDisc(center, radius, inner);
                bool smoke = smoky && Random.value < 0.18f;
                emit.startColor = smoke ? Smoke : color;
                emit.position = at + Vector3.up * (smoke ? 0.9f : 0.1f);
                emit.velocity = new Vector3(Random.Range(-0.4f, 0.4f), speed * Random.Range(0.6f, 1.2f), Random.Range(-0.4f, 0.4f));
                emit.startSize = size * Random.Range(0.6f, 1.2f) * (smoke ? 1.5f : 1f);
                emit.startLifetime = life * Random.Range(0.7f, 1.2f) * (smoke ? 1.6f : 1f);
                emit.rotation3D = Random.rotation.eulerAngles;
                cubes.Emit(emit, 1);
            }
        }

        // Flat tiles lying on the ground at random spots inside a disc or ring: a slick of oil, a crust
        // of frost, scorched earth. Each appears, lies there for a moment and shrinks away, so an
        // area that keeps being fed has a ragged, living edge and no outline.
        public void Ground(Vector3 center, float radius, float inner, Color color, int count, float size = 0.6f, float life = 1.2f)
        {
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                float side = size * Random.Range(0.6f, 1.3f), shade = Random.Range(0.8f, 1.1f);
                emit.position = InDisc(center, radius, inner) + Vector3.up * Random.Range(0.02f, 0.07f);
                emit.startColor = new Color(color.r * shade, color.g * shade, color.b * shade, 1f);
                emit.startSize3D = new Vector3(side, 0.03f, side * Random.Range(0.6f, 1.2f));
                emit.rotation3D = new Vector3(0f, Random.Range(0f, 360f), 0f);
                emit.startLifetime = life * Random.Range(0.7f, 1.3f);
                tiles.Emit(emit, 1);
            }
        }

        // Small cubes that pop up out of the ground and drop back: bubbles in a pool.
        public void Bubbles(Vector3 center, float radius, float inner, Color color, int count)
        {
            var emit = new ParticleSystem.EmitParams { startColor = color };
            for (int i = 0; i < count; i++)
            {
                emit.position = InDisc(center, radius, inner) + Vector3.up * 0.08f;
                emit.velocity = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(1.6f, 3.4f), Random.Range(-0.3f, 0.3f));
                emit.startSize = Random.Range(0.1f, 0.24f);
                emit.startLifetime = Random.Range(0.3f, 0.5f);
                emit.rotation3D = Random.rotation.eulerAngles;
                debris.Emit(emit, 1);
            }
        }

        // A jet of cubes inside a cone, for the flamethrower. Hot and pale at the nozzle, smoke at the far end.
        public void Stream(Vector3 origin, Vector3 dir, float halfAngle, float range, Color color, int count)
        {
            var emit = new ParticleSystem.EmitParams { position = origin };
            Color hot = Color.Lerp(color, Color.white, 0.5f);
            for (int i = 0; i < count; i++)
            {
                Vector3 aim = Quaternion.Euler(Random.Range(-4f, 4f), Random.Range(-halfAngle, halfAngle), 0f) * dir;
                float life = Random.Range(0.28f, 0.42f);
                emit.velocity = aim * (range / life) + Vector3.up * Random.Range(0f, 1.2f);
                emit.startColor = i % 6 == 5 ? Smoke : i % 3 == 0 ? hot : color;
                emit.startSize = Random.Range(0.16f, 0.34f);
                emit.startLifetime = life;
                emit.rotation3D = Random.rotation.eulerAngles;
                cubes.Emit(emit, 1);
            }
        }

        // Cubes thrown outward along the ground in every direction: the blast of something that
        // bursts or lands. They are uneven in speed and size, so it reads as a wave of debris.
        public void Shockwave(Vector3 center, float radius, Color color, int count = 28, float life = 0.3f)
        {
            var emit = new ParticleSystem.EmitParams { startColor = color };
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.value) * Mathf.PI * 2f / count;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                emit.position = center + dir * 0.6f + Vector3.up * Random.Range(0.15f, 0.4f);
                emit.velocity = dir * (radius / life * Random.Range(0.6f, 1f));
                emit.startSize = 0.24f * Random.Range(0.6f, 1.4f);
                emit.startLifetime = life * Random.Range(0.8f, 1.1f);
                emit.rotation3D = Random.rotation.eulerAngles;
                cubes.Emit(emit, 1);
            }
        }

        // Sparks racing outward along the ground from one distance to another: a pulse of electricity
        // spreading. Each takes a slightly crooked path.
        public void Wave(Vector3 center, float from, float to, Color color, int count, float life = 0.24f)
        {
            var emit = new ParticleSystem.EmitParams { startColor = color };
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.value) * Mathf.PI * 2f / count;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                emit.position = center + dir * (from + 0.4f) + Vector3.up * Random.Range(0.2f, 0.9f);
                emit.velocity = Quaternion.Euler(0f, Random.Range(-16f, 16f), 0f) * dir * ((to - from) / life * Random.Range(0.75f, 1f));
                emit.startSize3D = new Vector3(0.07f, 0.07f, Random.Range(0.6f, 1.5f));
                emit.startLifetime = life * Random.Range(0.8f, 1.1f);
                streaks.Emit(emit, 1);
            }
        }

        // Cubes that start at a distance all around a point and are drawn into it: something gathering
        // strength. Where they start shows how far it will reach.
        public void Gather(Vector3 center, float radius, Color color, int count, float time)
        {
            var emit = new ParticleSystem.EmitParams { startColor = color, startLifetime = time };
            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                emit.position = center + dir * radius + Vector3.up * Random.Range(0.1f, 0.7f);
                emit.velocity = dir * (-radius / time);
                emit.startSize = Random.Range(0.24f, 0.44f);
                emit.rotation3D = Random.rotation.eulerAngles;
                cubes.Emit(emit, 1);
            }
        }

        // Turns an expected number into a whole one that is right on average: 2.3 is 2, and 3 three times in ten.
        public static int Share(float expected)
        {
            int whole = (int)expected;
            return Random.value < expected - whole ? whole + 1 : whole;
        }

        // A random spot on the ground inside a disc, or inside a ring when inner is above zero.
        static Vector3 InDisc(Vector3 center, float radius, float inner)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float dist = Mathf.Sqrt(Mathf.Lerp(inner * inner, radius * radius, Random.value));
            return new Vector3(center.x + Mathf.Cos(angle) * dist, center.y, center.z + Mathf.Sin(angle) * dist);
        }

        // flat: tiles that lie on the ground. They grow in, stay and shrink away, and keep their colour.
        // Every other particle shrinks to nothing and cools to a darker shade over its life.
        ParticleSystem Create(string label, float gravity, bool sized, bool flat)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 8000;
            main.gravityModifier = gravity;
            main.startSpeed = 0f;
            main.startRotation3D = true;
            main.startSize3D = sized;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, flat
                ? new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f))
                : AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            if (!flat)
            {
                var fade = system.colorOverLifetime;
                fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 0.45f), new GradientColorKey(new Color(0.35f, 0.3f, 0.3f), 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                fade.color = gradient;
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            renderer.sharedMaterial = Combat.Config.particle;
            renderer.alignment = sized && !flat ? ParticleSystemRenderSpace.Velocity : ParticleSystemRenderSpace.Local;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            system.Play();
            return system;
        }
    }
}
