using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pulse
{
    // The fight against a Warden, in the arena under its keep. This runs everything around the fight:
    // the crowd on the stands, the gate, the Warden's entrance, the change to its second phase and
    // its fall. How the Warden itself fights is in EnemySystem, like every other enemy.
    public sealed class BossFight : MonoBehaviour
    {
        // One spectator on the stands.
        struct Fan
        {
            public Vector3 pos;
            public Quaternion facing;
            public float phase, scale;
            public int kind;
        }

        // One species in the crowd, with what is needed to draw all of its kind at once.
        sealed class Kind
        {
            public EnemyDef def;
            public Material[] materials;
            public readonly List<Matrix4x4> bodies = new List<Matrix4x4>(), legs = new List<Matrix4x4>();
        }

        // The entrance: how long the camera takes to reach the gate, the gate to open, the Warden to
        // walk in, how long it stands and roars, and how long the camera takes back to the machine.
        const float PanTime = 1f, GateTime = 0.8f, WalkTime = 2.6f, RoarTime = 2.3f, ReturnTime = 0.9f;
        const float PhaseTime = 2.2f, FallTime = 1.7f, AfterFall = 1.6f, SlowMotion = 0.35f, CardTime = 2.6f;
        // The crowd. Cheer runs from 0 to 1: how wild it is. It jumps when the Warden lands a hit and settles again.
        const float StandLow = 1f, StandHigh = 1.8f, IdleCheer = 0.22f, CheerFade = 0.5f, GateWidth = 5f;

        readonly List<Fan> fans = new List<Fan>();
        readonly List<Kind> kinds = new List<Kind>();
        readonly Matrix4x4[] buffer = new Matrix4x4[1023];
        DungeonDef def;
        DungeonMap map;
        Transform leftDoor, rightDoor;
        Vector3 gateCenter, lastSeen;
        float cheer, gateOpen, lastHull, lastCrash, sink;
        bool fighting, changing, silent;

        public bool Active { get; private set; }
        // The Warden has fallen and the scene after it is over.
        public bool Won { get; private set; }
        // The second phase lights the vents in the arena floor.
        public bool VentsLit { get; private set; }
        // The title card: two lines across the screen when the Warden enters, changes phase and falls.
        public string CardLabel { get; private set; }
        public string CardName { get; private set; }
        public float CardLeft { get; private set; }
        public float CardTotal => CardTime;
        // The colour of the Warden's strand, for the rule under its name.
        public Color CardColor { get; private set; } = Tokens.Heat;

        // Builds what the map only marks: the crowd on the stands and the gate the Warden comes through.
        public void Begin(DungeonDef dungeon, DungeonMap dungeonMap, Color strand)
        {
            End();
            def = dungeon;
            map = dungeonMap;
            CardColor = strand;
            Active = true;
            cheer = IdleCheer;
            lastCrash = Combat.Enemies.LastCrash;
            var config = Combat.Config;

            // Everyone comes to watch: every species of the open world, and the Strays of the dungeons.
            var species = new List<EnemyDef> { config.grunt, config.brute };
            if (config.scenery != null)
                foreach (var biome in config.scenery.biomes)
                    species.AddRange(biome.species);
            foreach (var one in species)
            {
                if (one == null || one.mesh == null || one.behaviour == Behaviour.Warden) continue;
                kinds.Add(new Kind
                {
                    def = one,
                    materials = new[]
                    {
                        Slots.Or(one.body, config.enemy), Slots.Or(one.plate, Slots.Or(config.enemyPlate, config.part)),
                        Slots.Or(config.dark, config.part), Slots.Or(one.glow, Slots.Or(config.enemyGlow, config.body)),
                    },
                });
            }
            var random = new System.Random(3);
            foreach (var stand in map.Stands)
            {
                if (kinds.Count == 0) break;
                // A stand faces the middle of the arena, by quarter turns, and has a low step in front and a high one behind.
                Vector3 inward = map.Center - stand;
                var facing = Quaternion.Euler(0f, Mathf.Round(Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg / 90f) * 90f, 0f);
                for (int step = 0; step < 2; step++)
                {
                    if (random.NextDouble() > 0.82) continue;
                    int kind = random.Next(kinds.Count);
                    var at = new Vector3((float)random.NextDouble() - 0.5f, step == 0 ? StandLow : StandHigh, step == 0 ? 0.5f : -0.5f);
                    fans.Add(new Fan
                    {
                        pos = stand + facing * at, facing = facing, kind = kind, phase = (float)random.NextDouble() * 6f,
                        // Large species are drawn smaller, so that each fits on one step.
                        scale = Mathf.Clamp(0.55f / kinds[kind].def.radius, 0.5f, 1.3f),
                    });
                }
            }

            // The gate: two leaves that slide apart.
            if (map.Gate.Count == 0) return;
            foreach (var cell in map.Gate) gateCenter += cell;
            gateCenter /= map.Gate.Count;
            leftDoor = Door("Gate leaf", config);
            rightDoor = Door("Gate leaf", config);
            PlaceDoors();
        }

        Transform Door(string label, GameConfig config)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var mesh = config.scenery != null ? config.scenery.bossGate : null;
            if (mesh == null) return go.transform;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = Slots.For(mesh, config.part, config.part, config.dark, config.heat);
            return go.transform;
        }

        void PlaceDoors()
        {
            if (leftDoor == null) return;
            float apart = GateWidth * 0.5f + gateOpen * GateWidth;
            leftDoor.position = gateCenter + Vector3.left * apart;
            rightDoor.position = gateCenter + Vector3.right * apart;
        }

        // Called when the machine leaves, whichever way.
        public void End()
        {
            if (leftDoor != null) Destroy(leftDoor.gameObject);
            if (rightDoor != null) Destroy(rightDoor.gameObject);
            leftDoor = rightDoor = null;
            fans.Clear();
            kinds.Clear();
            gateCenter = Vector3.zero;
            gateOpen = sink = 0f;
            CardLeft = 0f;
            Active = Won = VentsLit = fighting = changing = silent = false;
            Look.Exposure = 0f;
        }

        // ---- The entrance ----

        // The camera leaves the machine and travels to the gate. The gate opens, the Warden walks in,
        // stops, and brings both fists down. The crowd goes wild, its name crosses the screen, the
        // gate shuts behind it, and the camera comes back.
        public IEnumerator Intro()
        {
            var camera = Combat.Camera;
            var rig = Combat.Rig;
            var focus = new GameObject("Boss focus").transform;
            focus.SetParent(transform, false);
            focus.position = rig.transform.position;
            // It comes out of the dark behind the gate and walks to its place in the arena.
            Vector3 inside = map.BossSpawn, outside = gateCenter + Vector3.forward * 6f, start = rig.transform.position;
            Vector3 atGate = gateCenter + Vector3.back * 5f;
            if (camera != null) { camera.target = focus; camera.pin = 0f; }

            for (float t = 0f; t < PanTime; t += Time.deltaTime)
            {
                focus.position = Vector3.Lerp(start, atGate, Mathf.SmoothStep(0f, 1f, t / PanTime));
                cheer = Mathf.Lerp(IdleCheer, 0.55f, t / PanTime);
                yield return null;
            }

            for (float t = 0f; t < GateTime; t += Time.deltaTime)
            {
                gateOpen = Mathf.SmoothStep(0f, 1f, t / GateTime);
                PlaceDoors();
                if (camera != null) camera.Shake(0.06f);
                if (Time.deltaTime > 0f) Combat.Vfx.Burst(gateCenter + Vector3.right * Random.Range(-5f, 5f), Tokens.Dust, 1, 2f, 0.25f, 0.6f, falls: true);
                yield return null;
            }

            // It is real from here on, but nothing can hurt it yet and it does not fight.
            int index = Combat.Enemies.Spawn(def.boss, outside, true, Rank.Warden);
            if (index >= 0)
            {
                Combat.Enemies[index].scripted = true;
                Combat.Enemies[index].shielded = true;
            }
            float stride = 0f;
            for (float t = 0f; t < WalkTime; t += Time.deltaTime)
            {
                Vector3 at = Vector3.Lerp(outside, inside, t / WalkTime);
                int warden = Combat.Enemies.Warden();
                if (warden >= 0) Combat.Enemies[warden].pos = at;
                focus.position = Vector3.Lerp(focus.position, at + Vector3.back * 2f, 1f - Mathf.Exp(-3f * Time.deltaTime));
                cheer = Mathf.Lerp(0.55f, 0.8f, t / WalkTime);
                // Every step lands.
                if ((stride += Time.deltaTime) >= 0.36f)
                {
                    stride = 0f;
                    if (camera != null) camera.Shake(0.22f);
                    Combat.Vfx.Shockwave(at, 3f, Tokens.Dust, 14, 0.3f);
                }
                yield return null;
            }

            // Both fists into the floor.
            Combat.Marks.Crack(inside, 8f);
            Combat.Vfx.Shockwave(inside, 12f, Tokens.Alert, 60, 0.5f);
            Combat.Vfx.Shockwave(inside, 7f, Tokens.Heat, 40, 0.35f);
            Combat.Vfx.Burst(inside + Vector3.up * 0.5f, Tokens.Dust, 40, 10f, 0.4f, 1f, falls: true);
            Combat.Vfx.Sparks(inside + Vector3.up * 2f, Tokens.Heat, 40, 16f, 1f, 0.5f);
            if (camera != null) camera.Shake(0.9f);
            cheer = 1f;
            Show(def.bossTitle, def.boss.displayName);
            for (float t = 0f; t < RoarTime; t += Time.deltaTime)
            {
                int warden = Combat.Enemies.Warden();
                if (warden >= 0 && Mathf.Repeat(t, 0.5f) < Time.deltaTime) Combat.Enemies[warden].lunge = 0.24f;
                gateOpen = Mathf.Clamp01(1f - t / GateTime);
                PlaceDoors();
                cheer = 1f;
                yield return null;
            }
            gateOpen = 0f;
            PlaceDoors();

            Vector3 held = focus.position;
            for (float t = 0f; t < ReturnTime; t += Time.deltaTime)
            {
                focus.position = Vector3.Lerp(held, rig.transform.position, Mathf.SmoothStep(0f, 1f, t / ReturnTime));
                yield return null;
            }
            if (camera != null) { camera.target = null; camera.Snap(); }
            Destroy(focus.gameObject);

            int ready = Combat.Enemies.Warden();
            if (ready >= 0)
            {
                Combat.Enemies[ready].scripted = false;
                Combat.Enemies[ready].shielded = false;
                Combat.Enemies[ready].timer = 1.2f;
            }
            lastHull = rig.Hull;
            fighting = true;
        }

        void Show(string label, string name)
        {
            CardLabel = label;
            CardName = name;
            CardLeft = CardTime;
        }

        // ---- The fight ----

        void Update()
        {
            if (!Active) return;
            float dt = Time.deltaTime;
            CardLeft -= Time.unscaledDeltaTime;
            cheer = Mathf.MoveTowards(cheer, silent ? 0f : IdleCheer, CheerFade * dt);
            if (silent) sink = Mathf.MoveTowards(sink, 1f, dt * 0.5f);
            DrawCrowd();
            if (!fighting || changing) return;

            var rig = Combat.Rig;
            int index = Combat.Enemies.Warden();
            if (index < 0)
            {
                StartCoroutine(Fall());
                return;
            }
            ref var warden = ref Combat.Enemies[index];
            lastSeen = warden.pos;

            // The crowd is on the Warden's side: it roars when the machine is hurt, and groans when
            // the Warden runs itself into a wall.
            if (rig.Hull < lastHull - 0.5f) cheer = Mathf.Max(cheer, 0.95f);
            lastHull = rig.Hull;
            if (Combat.Enemies.LastCrash != lastCrash)
            {
                lastCrash = Combat.Enemies.LastCrash;
                cheer = 0.05f;
            }
            if (warden.stage == 1 && warden.hp <= warden.maxHp * 0.5f) StartCoroutine(SecondPhase());
        }

        // At half its hull the Warden stops, burns its plating off and comes back hotter and faster.
        // Nothing can hurt it while it changes. The vents in the floor wake with it.
        IEnumerator SecondPhase()
        {
            changing = true;
            int index = Combat.Enemies.Warden();
            if (index >= 0)
            {
                Combat.Enemies[index].scripted = true;
                Combat.Enemies[index].shielded = true;
            }
            Show("Second phase", string.IsNullOrEmpty(def.bossSecondName) ? def.boss.displayName : def.bossSecondName);
            float burst = 0f;
            for (float t = 0f; t < PhaseTime; t += Time.deltaTime)
            {
                cheer = 1f;
                index = Combat.Enemies.Warden();
                if (index < 0) break;
                Vector3 at = Combat.Enemies[index].pos;
                if ((burst += Time.deltaTime) >= 0.16f)
                {
                    burst = 0f;
                    Combat.Enemies[index].flash = 0.07f;
                    // Plating flies off in every direction.
                    Combat.Vfx.Burst(at + Vector3.up * 3f, Slots.Or(def.boss.plate, Combat.Config.part).color, 10, 12f, 0.5f, 1f, falls: true);
                    Combat.Vfx.Sparks(at + Vector3.up * 3f, Tokens.Heat, 14, 14f, 0.9f, 0.4f);
                    if (Combat.Camera != null) Combat.Camera.Shake(0.3f);
                }
                yield return null;
            }
            index = Combat.Enemies.Warden();
            if (index >= 0)
            {
                Vector3 at = Combat.Enemies[index].pos;
                Combat.Marks.Scorch(at, 7f);
                Combat.Marks.Crack(at, 6f);
                Combat.Vfx.Shockwave(at, 14f, Tokens.Heat, 70, 0.5f);
                Combat.Vfx.Burst(at + Vector3.up * 2f, Tokens.Heat, 50, 12f, 0.4f, 0.9f);
                if (Combat.Camera != null) Combat.Camera.Shake(0.9f);
                Combat.Enemies[index].stage = 2;
                Combat.Enemies[index].scripted = false;
                Combat.Enemies[index].shielded = false;
                Combat.Enemies[index].timer = 0.8f;
            }
            VentsLit = true;
            changing = false;
        }

        // The Warden is gone. Time slows while it comes apart, the crowd falls silent and sinks
        // away, and its name crosses the screen one last time.
        IEnumerator Fall()
        {
            fighting = false;
            silent = true;
            // The fight is over. Nothing in the arena can hurt the machine any more, and the camera
            // leaves it to watch the Warden go.
            var rig = Combat.Rig;
            var camera = Combat.Camera;
            rig.Scripted = true;
            var focus = new GameObject("Boss focus").transform;
            focus.SetParent(transform, false);
            focus.position = rig.transform.position;
            if (camera != null) camera.target = focus;
            Time.timeScale = SlowMotion;
            Show("The Warden has fallen", def.boss.displayName);
            Color plating = Slots.Or(def.boss.plate, Combat.Config.part).color;
            float burst = 0f;
            for (float t = 0f; t < FallTime; t += Time.unscaledDeltaTime)
            {
                focus.position = Vector3.Lerp(focus.position, lastSeen, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
                if ((burst += Time.unscaledDeltaTime) >= 0.13f)
                {
                    burst = 0f;
                    Vector3 at = lastSeen + new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(0.5f, 4.5f), Random.Range(-2.5f, 2.5f));
                    Combat.Vfx.Burst(at, Tokens.Heat, 26, 11f, 0.5f, 0.8f);
                    Combat.Vfx.Burst(at, plating, 14, 13f, 0.55f, 1.1f, falls: true);
                    Combat.Vfx.Sparks(at, Tokens.PulseGreen, 16, 16f, 1f, 0.5f);
                    Combat.Vfx.Shockwave(lastSeen, 6f, Tokens.Heat, 16, 0.3f);
                    Look.Exposure = 0.5f;
                    if (camera != null) camera.Shake(0.4f);
                }
                else Look.Exposure = Mathf.Lerp(Look.Exposure, 0f, 0.2f);
                yield return null;
            }
            Look.Exposure = 0f;
            if (Time.timeScale > 0f) Time.timeScale = 1f;
            // What is left of its Pulse goes up in one last wave.
            Combat.Marks.Scorch(lastSeen, 9f);
            Combat.Marks.Crack(lastSeen, 7f);
            Combat.Vfx.Shockwave(lastSeen, 16f, Tokens.PulseGreen, 90, 0.6f);
            Combat.Vfx.Rise(lastSeen, 3f, 0f, Tokens.PulseGreen, 50, 5f, 0.24f, 1.1f);
            if (camera != null) camera.Shake(0.8f);
            Vector3 held = focus.position;
            for (float t = 0f; t < AfterFall; t += Time.deltaTime)
            {
                focus.position = Vector3.Lerp(held, rig.transform.position, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, t / AfterFall)));
                yield return null;
            }
            if (camera != null) { camera.target = null; camera.Snap(); }
            Destroy(focus.gameObject);
            Won = true;
        }

        // ---- The crowd ----

        // Everyone on the stands hops, higher and faster the wilder the crowd is, and turns from side
        // to side. Each species is drawn in one go.
        void DrawCrowd()
        {
            if (fans.Count == 0) return;
            foreach (var kind in kinds)
            {
                kind.bodies.Clear();
                kind.legs.Clear();
            }
            float time = Time.time;
            foreach (var fan in fans)
            {
                var kind = kinds[fan.kind];
                var one = kind.def;
                float beat = time * (6f + cheer * 7f) + fan.phase * 7f;
                float hop = Mathf.Abs(Mathf.Sin(beat)) * (0.1f + 0.75f * cheer) - sink * 2.2f;
                var facing = fan.facing * Quaternion.Euler(0f, Mathf.Sin(beat * 0.5f + fan.phase) * 24f * cheer, 0f);
                Vector3 feet = fan.pos + Vector3.up * hop;
                var size = Vector3.one * fan.scale;
                kind.bodies.Add(Matrix4x4.TRS(feet, facing, size));
                if (one.legMesh == null) continue;
                for (int side = -1; side <= 1; side += 2)
                    kind.legs.Add(Matrix4x4.TRS(feet + facing * (new Vector3(side * one.legSpacing, one.hipHeight, 0f) * fan.scale), facing, size));
            }
            foreach (var kind in kinds)
            {
                Draw(kind.def.mesh, kind.bodies, kind.materials);
                if (kind.def.legMesh != null) Draw(kind.def.legMesh, kind.legs, kind.materials);
            }

            // A wild crowd throws sparks.
            if (cheer > 0.7f && Time.deltaTime > 0f && fans.Count > 0)
            {
                var fan = fans[Random.Range(0, fans.Count)];
                Combat.Vfx.Sparks(fan.pos + Vector3.up * 1.5f, Tokens.Alert, 2, 7f, 0.5f, 0.4f);
            }
        }

        void Draw(Mesh mesh, List<Matrix4x4> matrices, Material[] materials)
        {
            if (matrices.Count == 0) return;
            for (int sub = 0; sub < mesh.subMeshCount && sub < materials.Length; sub++)
            {
                var renderParams = new RenderParams(materials[sub])
                {
                    shadowCastingMode = ShadowCastingMode.On, receiveShadows = true,
                    worldBounds = new Bounds(map.Center, Vector3.one * 200f),
                };
                for (int start = 0; start < matrices.Count; start += buffer.Length)
                {
                    int count = Mathf.Min(buffer.Length, matrices.Count - start);
                    matrices.CopyTo(start, buffer, 0, count);
                    Graphics.RenderMeshInstanced(renderParams, mesh, sub, buffer, count);
                }
            }
        }
    }
}
