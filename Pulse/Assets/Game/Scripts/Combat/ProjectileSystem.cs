using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pulse
{
    // Everything that flies: bolts that go straight and hit the first target, and globs that are lobbed
    // in an arc and leave a zone where they land. Shots fired by enemies are hostile: they hit the
    // machine instead of enemies and are drawn as long shards, so they are never mistaken for its own.
    public sealed class ProjectileSystem : MonoBehaviour
    {
        struct Shot
        {
            public Vector3 pos, vel;
            public float left, damage, age;
            public StateType state;
            public bool hostile;
            public string source;
            // pierce: how many more enemies it goes through. skipA, skipB: the last two it went through.
            public int pierce, skipA, skipB;
        }

        struct Glob
        {
            public Vector3 from, to;
            // blast: a shell of the machine's own. It bursts where it lands for this much.
            public float age, time, radius, duration, dps, trail, impact, blast;
            public StateType state;
            public bool ring, hostile;
            public string source;
        }

        // A ball of fire that turns after the machine. It is slower than the machine driving straight,
        // and it turns in a wide arc: it can be outrun, or led into a wall.
        struct Seeker
        {
            public Vector3 pos, vel;
            public float left, damage, turn, trail;
            public string source;
        }

        // A mine lies where it was laid until something walks onto it, or until its time is up.
        struct Charge
        {
            public Vector3 pos;
            public float radius, damage, left, age;
            public StateType state;
        }

        // Mines: at most MaxMines lie at once. One is armed ArmTime after it was laid, and goes off
        // when an enemy comes within MineTrigger. With a state on it, that state stays for MineStays.
        const int MaxMines = 14;
        const float ArmTime = 0.45f, MineTrigger = 0.9f, MineStays = 2.5f, MineFire = 6f;
        const int States = 6, BatchSize = 1023;
        const float SeekerSize = 0.7f;
        const float HitRadius = 0.2f, Height = 0.75f, SpinSpeed = 900f, TrailEvery = 0.02f, ArcHeight = 3.2f;
        const float TailLength = 1.6f, TailWidth = 0.045f, ChillSlow = 0.7f, ChillTime = 1f;
        static readonly Vector3 ShardSize = new Vector3(0.2f, 0.2f, 1.3f);

        readonly List<Shot> shots = new List<Shot>(256);
        readonly List<Glob> globs = new List<Glob>(32);
        readonly List<Seeker> seekers = new List<Seeker>(16);
        readonly List<Charge> mines = new List<Charge>(MaxMines);
        readonly List<int> hits = new List<int>(64);
        // Each bolt drags one thin tail behind it, so it reads as a single shot.
        // The second half of the lists is for hostile shots.
        readonly Matrix4x4[][] matrices = new Matrix4x4[States * 2][], tails = new Matrix4x4[States * 2][];
        readonly int[] drawn = new int[States * 2];
        Mesh cube;

        void Awake()
        {
            cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            for (int b = 0; b < States * 2; b++)
            {
                matrices[b] = new Matrix4x4[BatchSize];
                tails[b] = new Matrix4x4[BatchSize];
            }
        }

        public void Clear()
        {
            shots.Clear();
            globs.Clear();
            seekers.Clear();
            mines.Clear();
        }

        // How many mines lie on the ground.
        public int Mines => mines.Count;

        // Lays a mine. With all of them out, the oldest goes off to make room.
        public void Mine(Vector3 pos, float radius, float damage, StateType state, float life)
        {
            pos.y = 0f;
            if (Combat.Scenery.Blocks(pos)) return;
            if (mines.Count >= MaxMines)
            {
                Detonate(mines[0]);
                mines.RemoveAt(0);
            }
            mines.Add(new Charge { pos = pos, radius = radius, damage = damage, state = state, left = life });
            Combat.Vfx.Burst(pos + Vector3.up * 0.2f, Tokens.Dust, 4, 2f, 0.14f, 0.35f);
        }

        public void Fire(Vector3 pos, Vector3 dir, float speed, float range, float damage, StateType state,
            bool hostile = false, string source = null, int pierce = 0)
        {
            pos.y = Height;
            shots.Add(new Shot
            {
                pos = pos, vel = dir * speed, left = range / speed, damage = damage, state = state, hostile = hostile, source = source, pierce = pierce,
            });
        }

        // Lets go of a ball of fire that follows the machine. turn is how fast it can change course, in
        // degrees a second; life is how long it burns.
        public void Orb(Vector3 pos, Vector3 dir, float speed, float turn, float life, float damage, string source)
        {
            pos.y = Height + 0.4f;
            dir.y = 0f;
            seekers.Add(new Seeker { pos = pos, vel = dir.normalized * speed, left = life, damage = damage, turn = turn, source = source });
        }

        // Throws a glob that turns into a ground zone when it lands. A hostile glob shows where it
        // will land while it flies: the ground there flickers and throws up motes, more the closer
        // the glob is. It hurts the machine by impact if it lands on it.
        // blast: for a shell of the machine's own, the damage it bursts for where it lands.
        public void Lob(Vector3 from, Vector3 to, float time, StateType state, float radius, float duration, float dps, bool ring,
            bool hostile = false, float impact = 0f, string source = null, float blast = 0f)
        {
            to.y = 0f;
            globs.Add(new Glob
            {
                from = from, to = to, time = time, state = state, radius = radius, duration = duration, dps = dps, ring = ring,
                hostile = hostile, impact = impact, source = source, blast = blast,
            });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var config = Combat.Config;
            var rig = Combat.Rig;

            // ---- Bolts: spin around their flight axis, pulse and leave a streak behind ----
            Array.Clear(drawn, 0, drawn.Length);
            for (int s = shots.Count - 1; s >= 0; s--)
            {
                var shot = shots[s];
                shot.pos += shot.vel * dt;
                shot.left -= dt;
                shot.age += dt;
                var color = Tokens.Of(shot.state);
                // Dungeon walls stop every shot.
                if (Combat.Scenery.Blocks(shot.pos))
                {
                    Combat.Vfx.Sparks(shot.pos, color, 5);
                    shots.RemoveAt(s);
                    continue;
                }

                bool hit;
                if (shot.hostile)
                {
                    Vector3 to = rig.transform.position - shot.pos;
                    to.y = 0f;
                    float reach = rig.Radius * 0.75f + HitRadius;
                    hit = rig.Exposed && to.sqrMagnitude < reach * reach;
                    if (hit)
                    {
                        rig.TakeHit(shot.damage, shot.pos - shot.vel, shot.source);
                        if (shot.state == StateType.Cryo) rig.Slow(ChillSlow, ChillTime, StateType.Cryo);
                    }
                }
                else
                {
                    // A fast shot covers a lot of ground between two frames; what it passed counts as touched.
                    float swept = HitRadius + Mathf.Max(0f, shot.vel.magnitude * dt * 0.5f - 0.3f);
                    int target = Combat.Enemies.Touching(shot.pos, swept, shot.skipA, shot.skipB);
                    hit = target >= 0;
                    if (hit)
                    {
                        int id = Combat.Enemies[target].id;
                        Combat.Enemies.Hit(target, shot.damage, shot.state);
                        // A shot that pierces goes on, and does not hit the same target twice.
                        if (shot.pierce > 0)
                        {
                            shot.pierce--;
                            shot.skipB = shot.skipA;
                            shot.skipA = id;
                            Combat.Vfx.Sparks(shot.pos, color, 6);
                            hit = false;
                        }
                    }
                }
                if (hit) Combat.Vfx.Sparks(shot.pos, color, 6);
                if (hit || shot.left <= 0f) { shots.RemoveAt(s); continue; }
                shots[s] = shot;

                int bucket = (int)shot.state + (shot.hostile ? States : 0);
                if (drawn[bucket] >= BatchSize) continue;
                float pulse = 1f + 0.18f * Mathf.Sin(shot.age * 45f);
                var aim = Quaternion.LookRotation(shot.vel);
                float speed = shot.vel.magnitude, tail = Mathf.Min(TailLength, shot.age * speed);
                tails[bucket][drawn[bucket]] = Matrix4x4.TRS(shot.pos - shot.vel / speed * (tail * 0.5f + 0.12f), aim,
                    new Vector3(TailWidth, TailWidth, tail));
                matrices[bucket][drawn[bucket]++] = shot.hostile
                    ? Matrix4x4.TRS(shot.pos, aim * Quaternion.Euler(0f, 0f, 45f), ShardSize * pulse)
                    : Matrix4x4.TRS(shot.pos, aim * Quaternion.Euler(0f, 0f, shot.age * SpinSpeed),
                        config.boltMesh != null ? new Vector3(pulse, pulse, 1f) : new Vector3(0.16f, 0.16f, 0.55f));
            }
            for (int b = 0; b < States * 2; b++)
            {
                if (drawn[b] == 0) continue;
                var glow = new RenderParams(GlowFor((StateType)(b % States)));
                Graphics.RenderMeshInstanced(glow, b < States && config.boltMesh != null ? config.boltMesh : cube, 0, matrices[b], drawn[b]);
                Graphics.RenderMeshInstanced(glow, cube, 0, tails[b], drawn[b]);
            }

            RunSeekers(dt, rig, config);
            RunMines(dt, config);

            // ---- Globs: fly an arc, wobble, drip, and splash into a zone ----
            for (int g = globs.Count - 1; g >= 0; g--)
            {
                var glob = globs[g];
                glob.age += dt;
                glob.trail += dt;
                float t = Mathf.Clamp01(glob.age / glob.time);
                Vector3 pos = Vector3.Lerp(glob.from, glob.to, t) + Vector3.up * (ArcHeight * 4f * t * (1f - t));
                var color = Tokens.Of(glob.state);

                if (t >= 1f)
                {
                    if (glob.blast > 0f)
                    {
                        Combat.Enemies.Explode(glob.to, glob.radius, glob.blast, glob.state, hits, glob.ring ? glob.radius * 0.6f : 0f);
                        Combat.Marks.Scorch(glob.to, glob.radius * 0.8f);
                        Combat.Vfx.Burst(glob.to + Vector3.up * 0.3f, Tokens.Dust, 14, 8f, 0.26f, 0.6f, falls: true);
                        if (Combat.Camera != null) Combat.Camera.Shake(0.12f);
                    }
                    if (glob.duration > 0f)
                        Combat.Zones.Spawn(glob.state, glob.to, glob.radius, glob.duration, glob.dps, glob.ring, glob.hostile);
                    if (glob.hostile && glob.state == StateType.Heat) Combat.Marks.Scorch(glob.to, glob.radius * 0.9f);
                    Combat.Vfx.Burst(glob.to + Vector3.up * 0.2f, color, 16, 6f, 0.24f, 0.45f, falls: true);
                    Combat.Vfx.Shockwave(glob.to, glob.radius, color, 18);
                    if (glob.hostile && glob.impact > 0f && rig.Exposed)
                    {
                        Vector3 to = rig.transform.position - glob.to;
                        to.y = 0f;
                        float reach = glob.radius + rig.Radius * 0.3f;
                        if (to.sqrMagnitude < reach * reach) rig.TakeHit(glob.impact, glob.to, glob.source);
                    }
                    globs.RemoveAt(g);
                    continue;
                }
                if (glob.trail >= TrailEvery * 2f)
                {
                    glob.trail = 0f;
                    Combat.Vfx.Burst(pos, color, 1, 0.6f, 0.14f, 0.35f, falls: true);
                    if (glob.hostile)
                    {
                        Combat.Vfx.Ground(glob.to, glob.radius, 0f, Tokens.Alert, t > 0.5f ? 3 : 1, 0.45f, 0.3f);
                        Combat.Vfx.Rise(glob.to, glob.radius, 0f, color, t > 0.5f ? 3 : 1, 1.6f, 0.14f, 0.35f);
                    }
                }
                globs[g] = glob;

                float wobble = 1f + 0.22f * Mathf.Sin(glob.age * 28f);
                var matrix = Matrix4x4.TRS(pos, Quaternion.Euler(glob.age * 400f, glob.age * 250f, 0f),
                    new Vector3(wobble, 2f - wobble, wobble) * (config.globMesh != null ? 1f : 0.45f));
                Graphics.RenderMesh(new RenderParams(GlowFor(glob.state)), config.globMesh != null ? config.globMesh : cube, 0, matrix);
            }
        }

        // Seekers: turn toward the machine, trail embers, and go up in a burst when they reach it,
        // hit a wall or burn out. One that misses leaves a small fire where it ends.
        void RunSeekers(float dt, Rig rig, GameConfig config)
        {
            for (int s = seekers.Count - 1; s >= 0; s--)
            {
                var seeker = seekers[s];
                seeker.left -= dt;
                seeker.trail += dt;
                Vector3 to = rig.transform.position - seeker.pos;
                to.y = 0f;
                if (rig.Exposed && to.sqrMagnitude > 0.01f)
                    seeker.vel = Vector3.RotateTowards(seeker.vel, to.normalized * seeker.vel.magnitude, seeker.turn * Mathf.Deg2Rad * dt, 0f);
                seeker.pos += seeker.vel * dt;

                float reach = rig.Radius * 0.75f + SeekerSize;
                bool hit = rig.Exposed && to.sqrMagnitude < reach * reach;
                if (hit) rig.TakeHit(seeker.damage, seeker.pos - seeker.vel, seeker.source);
                if (hit || seeker.left <= 0f || Combat.Scenery.Blocks(seeker.pos))
                {
                    Combat.Vfx.Burst(seeker.pos, Tokens.Heat, 18, 7f, 0.3f, 0.5f);
                    Combat.Vfx.Shockwave(seeker.pos, 2.5f, Tokens.Heat, 14, 0.25f);
                    if (!hit) Combat.Zones.Spawn(StateType.Heat, seeker.pos, 1.3f, 2.5f, 6f, hostile: true);
                    seekers.RemoveAt(s);
                    continue;
                }
                if (seeker.trail >= 0.03f)
                {
                    seeker.trail = 0f;
                    Combat.Vfx.Burst(seeker.pos, Tokens.Heat, 1, 0.8f, 0.3f, 0.45f);
                    Combat.Vfx.Trail(seeker.pos, -seeker.vel, Tokens.Alert, 0.6f, 0.2f);
                }
                seekers[s] = seeker;

                float throb = (1.6f + 0.25f * Mathf.Sin(seeker.left * 18f)) * (config.globMesh != null ? 1f : 0.45f);
                Graphics.RenderMesh(new RenderParams(GlowFor(StateType.Heat)), config.globMesh != null ? config.globMesh : cube, 0,
                    Matrix4x4.TRS(seeker.pos, Quaternion.Euler(seeker.left * 300f, seeker.left * 210f, 0f), Vector3.one * throb));
            }
        }

        // Mines lie and blink. One goes off when an enemy walks onto it, or when its time is up.
        void RunMines(float dt, GameConfig config)
        {
            for (int m = mines.Count - 1; m >= 0; m--)
            {
                var mine = mines[m];
                mine.age += dt;
                mine.left -= dt;
                bool armed = mine.age >= ArmTime;
                if (mine.left <= 0f || (armed && Combat.Enemies.Touching(mine.pos, MineTrigger) >= 0))
                {
                    Detonate(mine);
                    mines.RemoveAt(m);
                    continue;
                }
                mines[m] = mine;

                // Its lamp blinks once it is armed, faster as its time runs out.
                bool lit = armed && Mathf.Sin(mine.age * (mine.left < 3f ? 22f : 7f)) > 0.2f;
                var lamp = lit ? GlowFor(mine.state) : Slots.Or(config.dark, config.part);
                var matrix = Matrix4x4.TRS(mine.pos, Quaternion.Euler(0f, mine.age * 30f, 0f), Vector3.one);
                if (config.mineMesh == null)
                {
                    Graphics.RenderMesh(new RenderParams(lamp), cube, 0, matrix * Matrix4x4.TRS(Vector3.up * 0.15f, Quaternion.identity, new Vector3(0.6f, 0.3f, 0.6f)));
                    continue;
                }
                for (int sub = 0; sub < config.mineMesh.subMeshCount && sub < 4; sub++)
                {
                    var material = sub == 0 ? config.part : sub == 1 ? config.body : sub == 2 ? Slots.Or(config.dark, config.part) : lamp;
                    Graphics.RenderMesh(new RenderParams(material), config.mineMesh, sub, matrix);
                }
            }
        }

        void Detonate(in Charge mine)
        {
            var color = Tokens.Of(mine.state);
            Combat.Enemies.Explode(mine.pos, mine.radius, mine.damage, mine.state, hits);
            if (mine.state != StateType.None)
                Combat.Zones.Spawn(mine.state, mine.pos, mine.radius * 0.8f, MineStays, mine.state == StateType.Heat ? MineFire : 0f);
            Combat.Marks.Scorch(mine.pos, mine.radius * 0.8f);
            Combat.Vfx.Burst(mine.pos + Vector3.up * 0.3f, color, 18, 9f, 0.26f, 0.5f, falls: true);
            Combat.Vfx.Burst(mine.pos + Vector3.up * 0.3f, Tokens.Dust, 10, 6f, 0.24f, 0.6f, falls: true);
            Combat.Vfx.Shockwave(mine.pos, mine.radius, color, 26);
            if (Combat.Camera != null) Combat.Camera.Shake(0.12f);
        }

        // Kinetic shots glow white; everything else glows in its state colour.
        static Material GlowFor(StateType state) =>
            Slots.Or(Combat.Config.StateMaterial(state), Slots.Or(Combat.Config.flash, Combat.Config.body));
    }
}
