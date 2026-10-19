using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Pulse
{
    public enum RunMode { OpenWorld, Dungeon }

    // The structure of a run: an open world with constant spawns, dungeons that are fought in waves
    // on a map drawn for each of them, and the keep where a Warden waits.
    public sealed class RunDirector : MonoBehaviour
    {
        public const float GateRadius = 3.5f;
        const float EnterSeconds = 1.7f, WaveDelay = 1.5f, BannerSeconds = 4f, NeedleDistance = 3.4f;
        // Entering a dungeon. The floor breaks and the machine sinks into the pit for SinkTime, shrinking
        // to SinkScale while the dark closes in around it. Then it drops FallHeight onto the arena, and
        // the camera moves in to CloseUp. FallHeight is large enough that the arena starts out of sight.
        // SinkIris: how far the dark closes in. Small enough that only the machine and the pit under it show.
        const float SinkTime = 1.1f, SinkScale = 0.5f, SinkZoom = 0.8f, IrisFrom = 0.3f, SinkIris = 1.9f;
        const float FallHeight = 90f, FallSpeed = 34f, FallGravity = 40f, CloseUp = 0.55f, RevealTime = 0.5f, LandTime = 0.7f;
        const float WipeTime = 0.35f;
        // How far from the machine the Echo of a cleared dungeon lies.
        const float EchoAhead = 4f;
        // Vents in a dungeon floor: every VentEvery seconds each one burns, freezes or oozes again.
        const float VentEvery = 2.2f, VentRadius = 1.5f, VentLasts = 2.6f, VentDamage = 7f;
        static readonly Vector3 DungeonCenter = new Vector3(500f, 0f, 0f);
        static readonly Quaternion Tumbled = Quaternion.Euler(55f, 120f, 30f);

        readonly HashSet<int> cleared = new HashSet<int>();
        WorldDef world;
        MeshRenderer[] gates, landmarks;
        Transform needle;
        Crumble crumble;
        DungeonMap map;
        Vector3 returnPos;
        string banner;
        float owed, waveTimer, bannerLeft, ventTimer, fireOwed;
        int dungeonBiome;

        public RunMode Mode { get; private set; }
        // The dungeon being fought, or -1 in the open world.
        public int DungeonIndex { get; private set; } = -1;
        public WaveRun Waves { get; private set; }
        // The fight against a Warden, while one is going on. See BossFight.
        public BossFight Boss { get; private set; }
        // The nearest open entrance, or -1 when none is left.
        public int TargetGate { get; private set; } = -1;
        public float TargetDistance { get; private set; }
        // Fills from 0 to 1 while the machine stands in an open entrance.
        public float EnterProgress { get; private set; }
        public string Banner => bannerLeft > 0f ? banner : null;
        public int ClearedCount => cleared.Count;
        // A scripted scene is playing: nothing spawns, and the machine is out of the player's hands.
        public bool Busy { get; private set; }

        // How far the picture is open around the machine, in metres. The camera draws it; see CameraRig.iris.
        static float IrisRadius
        {
            set { if (Combat.Camera != null) Combat.Camera.iris = value; }
        }

        public DungeonDef Dungeon(int index) => world.entrances[index].dungeon;

        void Start()
        {
            var config = Combat.Config;
            world = config.world;

            // An entrance is a disc on the ground with something standing behind it: an arch for a
            // dungeon, the keep for a Warden. Rim and landmark show its status by colour.
            gates = new MeshRenderer[world.entrances.Length];
            landmarks = new MeshRenderer[world.entrances.Length];
            var archMesh = config.scenery != null ? config.scenery.gate : null;
            var rimMesh = ZoneSystem.BuildDisc(0.9f);
            for (int i = 0; i < gates.Length; i++)
            {
                var def = Dungeon(i);
                // The disc itself stays dim; only its rim and the landmark carry the status colour.
                Disc("Gate: " + def.displayName, world.entrances[i].position, GateRadius, 0.04f, config.floor);
                var rim = new GameObject("Gate rim");
                rim.transform.SetParent(transform, false);
                rim.transform.position = world.entrances[i].position + Vector3.up * 0.07f;
                rim.transform.localScale = new Vector3(GateRadius, 1f, GateRadius);
                rim.AddComponent<MeshFilter>().sharedMesh = rimMesh;
                gates[i] = rim.AddComponent<MeshRenderer>();

                var mesh = def.landmark != null ? def.landmark : archMesh;
                if (mesh == null) continue;
                var landmark = new GameObject("Landmark: " + def.displayName);
                landmark.transform.SetParent(transform, false);
                landmark.transform.position = world.entrances[i].position + Vector3.forward * (GateRadius + def.landmarkDepth);
                landmark.AddComponent<MeshFilter>().sharedMesh = mesh;
                landmarks[i] = landmark.AddComponent<MeshRenderer>();
            }
            crumble = gameObject.AddComponent<Crumble>();
            Boss = gameObject.AddComponent<BossFight>();

            needle = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            needle.name = "Gate Needle";
            Destroy(needle.GetComponent<Collider>());
            needle.GetComponent<MeshRenderer>().sharedMaterial = config.pulse;
            needle.localScale = new Vector3(0.22f, 0.06f, 1.1f);
            needle.SetParent(transform, false);

            // The machine can leave the map, into the storm around it, but not the storm.
            Combat.Rig.SetBounds(Vector3.zero, world.halfSize + world.stormDepth, circular: false);
            PaintGates();
        }

        void Update()
        {
            if (!Combat.Rig.Alive || Busy) return;
            float dt = Time.deltaTime;
            bannerLeft -= dt;
            if (Mode == RunMode.OpenWorld) TickOpenWorld(dt);
            else TickDungeon(dt);
        }

        // ---- Open world ----

        void TickOpenWorld(float dt)
        {
            Vector3 rigPos = Combat.Rig.transform.position;

            // A constant rate with a cap. The open world never gets harder over time.
            owed += world.spawnPerSecond * dt;
            while (owed >= 1f)
            {
                owed -= 1f;
                if (Combat.Enemies.Count < world.maxEnemies) SpawnInOpenWorld(rigPos);
            }
            Combat.Enemies.RemoveBeyond(rigPos, world.despawnDistance);
            Fires(rigPos, dt);

            TargetGate = -1;
            TargetDistance = float.MaxValue;
            for (int i = 0; i < world.entrances.Length; i++)
            {
                if (!WorldRules.IsOpen(world, i, cleared)) continue;
                Vector3 to = world.entrances[i].position - rigPos;
                to.y = 0f;
                if (to.magnitude < TargetDistance) { TargetDistance = to.magnitude; TargetGate = i; }
            }

            // Standing in an open entrance for a moment enters it. No button needed.
            bool inGate = TargetGate >= 0 && TargetDistance <= GateRadius && !Combat.Rig.Launching;
            EnterProgress = inGate ? EnterProgress + dt / EnterSeconds : 0f;
            if (EnterProgress >= 1f) StartCoroutine(FallInto(TargetGate));
            else PointNeedle(rigPos, inGate);
        }

        // The chimneys of a keep burn for as long as the machine is near enough to see them: a steady
        // stream of embers and smoke, never a puff.
        void Fires(Vector3 rigPos, float dt)
        {
            fireOwed += dt * 9f;
            int count = (int)fireOwed;
            if (count == 0) return;
            fireOwed -= count;
            for (int i = 0; i < world.entrances.Length; i++)
            {
                var def = Dungeon(i);
                if (landmarks[i] == null || def.landmarkFires == null || def.landmarkFires.Length == 0) continue;
                Vector3 at = landmarks[i].transform.position;
                if ((at - rigPos).sqrMagnitude > 75f * 75f) continue;
                foreach (var fire in def.landmarkFires)
                    Combat.Vfx.Rise(at + fire, 0.5f, 0f, Tokens.Heat, count, 3.4f, 0.36f, 1.4f, smoky: true);
            }
        }

        // Something of the biome at that spot appears, scattered around the machine, and stays idle
        // until the machine comes within its range. Now and then it is a mini boss, rarely a boss.
        void SpawnInOpenWorld(Vector3 rigPos)
        {
            float angle = Random.value * Mathf.PI * 2f, edge = world.halfSize - 2f;
            Vector3 pos = rigPos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (world.spawnDistance * Random.Range(0.75f, 1.5f));
            pos.x = Mathf.Clamp(pos.x, -edge, edge);
            pos.z = Mathf.Clamp(pos.z, -edge, edge);

            var species = Combat.Scenery.SpeciesAt(pos);
            var def = species.Length > 0 ? species[Random.Range(0, species.Length)] : null;
            if (def == null)
            {
                Combat.Enemies.Spawn(Random.value < world.bruteChance ? Combat.Config.brute : Combat.Config.grunt, pos, aggro: false);
                return;
            }
            var rank = WorldRules.RollRank(world, Random.value, Combat.Enemies.Ranked(Rank.MiniBoss), Combat.Enemies.Ranked(Rank.Boss));
            int pack = rank == Rank.Normal ? Mathf.Max(1, def.packSize) : 1;
            for (int k = 0; k < pack; k++)
            {
                Vector3 scatter = k == 0 ? Vector3.zero : new Vector3(Random.Range(-1.6f, 1.6f), 0f, Random.Range(-1.6f, 1.6f));
                Combat.Enemies.Spawn(def, pos + scatter, aggro: false, rank);
            }
        }

        // A small marker next to the machine that points at the nearest open entrance.
        void PointNeedle(Vector3 rigPos, bool inGate)
        {
            // Not while the machine is still racing in: nothing may appear next to it then.
            bool show = TargetGate >= 0 && !inGate && !Combat.Rig.Launching;
            needle.gameObject.SetActive(show);
            if (!show) return;
            Vector3 dir = world.entrances[TargetGate].position - rigPos;
            dir.y = 0f;
            dir.Normalize();
            needle.SetPositionAndRotation(rigPos + dir * NeedleDistance + Vector3.up * 0.1f, Quaternion.LookRotation(dir));
        }

        // ---- Dungeon ----

        // The way in, as one unbroken shot. The floor of the entrance breaks up and the machine tumbles
        // into the pit while the dark closes in around it. With nothing left in the picture but the
        // machine on darkness, it is moved to the shaft above the dungeon, where it looks exactly the
        // same. From there it falls, the camera moves in, and the dungeon comes up out of the dark.
        IEnumerator FallInto(int index)
        {
            var rig = Combat.Rig;
            var camera = Combat.Camera;
            Busy = true;
            EnterProgress = 0f;
            rig.Scripted = true;
            needle.gameObject.SetActive(false);
            Vector3 pit = world.entrances[index].position, from = rig.transform.position;
            crumble.Begin(pit, GateRadius);
            if (camera != null) { camera.pin = 1f; camera.zoom = 1f; camera.Shake(0.35f); }

            for (float t = 0f; t < SinkTime; t += Time.deltaTime)
            {
                float u = t / SinkTime, drop = u * u;
                rig.transform.position = Vector3.Lerp(from, pit, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u * 1.6f)));
                rig.Visuals.localScale = Vector3.one * Mathf.Lerp(1f, SinkScale, drop);
                rig.Visuals.localRotation = Quaternion.Slerp(Quaternion.identity, Tumbled, drop);
                if (camera != null) camera.zoom = Mathf.Lerp(1f, SinkZoom, Mathf.SmoothStep(0f, 1f, u));
                IrisRadius = u < IrisFrom ? float.PositiveInfinity
                    : Mathf.Lerp(CameraRig.IrisWide, SinkIris, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(IrisFrom, 1f, u)));
                yield return null;
            }

            // The move. The machine keeps its size in the picture: it is drawn at full size again, and
            // the camera stands that much further back.
            EnterDungeon(index);
            Vector3 landing = map.Start;
            float startZoom = SinkZoom / SinkScale, startIris = SinkIris / SinkScale;
            rig.Visuals.localScale = Vector3.one;
            rig.Visuals.localPosition = Vector3.zero;
            rig.Visuals.localRotation = Tumbled;
            rig.transform.position = landing + Vector3.up * FallHeight;
            IrisRadius = startIris;
            if (camera != null) { camera.zoom = startZoom; camera.Snap(); }

            float height = FallHeight, speed = FallSpeed, falling = 0f;
            while (height > 0f)
            {
                float dt = Time.deltaTime;
                falling += dt;
                speed += FallGravity * dt;
                height = Mathf.Max(0f, height - speed * dt);
                float reveal = Mathf.Clamp01(falling / RevealTime);
                rig.transform.position = landing + Vector3.up * height;
                rig.Visuals.localRotation = Quaternion.Slerp(Tumbled, Quaternion.identity, Mathf.SmoothStep(0f, 1f, 1f - height / FallHeight));
                if (camera != null) camera.zoom = Mathf.Lerp(startZoom, CloseUp, Mathf.SmoothStep(0f, 1f, reveal));
                IrisRadius = reveal >= 1f ? float.PositiveInfinity : Mathf.Lerp(startIris, CameraRig.IrisWide, reveal * reveal);
                // Dust hanging in the shaft, which the fall turns into streaks.
                if (dt > 0f) Combat.Vfx.Rush(rig.transform.position + Vector3.down * 16f, new Vector3(9f, 12f, 9f), Vector3.up * 3f, Tokens.Dust, 3, 3.2f, 0.7f);
                yield return null;
            }

            // It lands.
            IrisRadius = float.PositiveInfinity;
            rig.transform.position = landing;
            rig.Visuals.localRotation = Quaternion.identity;
            rig.Thud();
            // The floor it comes down on does not take that well.
            Combat.Marks.Crack(landing, 4.6f);
            Combat.Vfx.Shockwave(landing, 8f, Tokens.Dust, 46, 0.4f);
            Combat.Vfx.Burst(landing + Vector3.up * 0.3f, Tokens.Dust, 34, 8f, 0.32f, 0.8f, falls: true);
            if (camera != null) camera.Shake(0.7f);
            for (float t = 0f; t < LandTime; t += Time.deltaTime)
            {
                if (camera != null) camera.zoom = Mathf.Lerp(CloseUp, 1f, Mathf.SmoothStep(0f, 1f, t / LandTime));
                yield return null;
            }
            if (camera != null) { camera.zoom = 1f; camera.pin = 0f; }

            // A Warden makes its entrance before the fight begins.
            if (Boss.Active) yield return Boss.Intro();
            rig.Scripted = false;
            waveTimer = 0f;
            Busy = false;
        }

        // Switches to the dungeon at once, without the fall. Also what a test calls.
        public void EnterDungeon(int index)
        {
            var def = Dungeon(index);
            DungeonIndex = index;
            Mode = RunMode.Dungeon;
            Waves = new WaveRun(def);
            EnterProgress = 0f;
            waveTimer = ventTimer = 0f;
            returnPos = world.entrances[index].position + Vector3.back * (GateRadius + 3f);
            dungeonBiome = Biomes.At(world.entrances[index].position, Combat.Scenery.BiomeCount);
            // A dungeon without a map of its own is a plain round arena.
            map = new DungeonMap(string.IsNullOrWhiteSpace(def.map) ? DungeonMap.Round(def.arenaRadius) : def.map, DungeonCenter);

            ClearField();
            needle.gameObject.SetActive(false);
            Combat.Scenery.EnterDungeon(map, dungeonBiome);
            MoveRig(map.Start, map.Center, map.Radius, circular: true);
            if (def.boss != null) Boss.Begin(def, map, Tokens.Of(Combat.Scenery.Hazard(dungeonBiome)));
        }

        void TickDungeon(float dt)
        {
            // In a Warden's arena the vents stay cold until the Warden lights them.
            if (!Boss.Active || Boss.VentsLit) Vents(dt);
            if (Boss.Active)
            {
                if (Boss.Won) StartCoroutine(ClimbOut());
                return;
            }

            // The next wave starts a moment after the last enemy of the current one is gone.
            if (Combat.Enemies.Count > 0) { waveTimer = 0f; return; }
            waveTimer += dt;
            if (waveTimer < WaveDelay) return;
            waveTimer = 0f;

            if (!Waves.Next(out var wave)) { StartCoroutine(ClimbOut()); return; }
            for (int i = 0; i < wave.grunts; i++) SpawnAtDoor(Combat.Config.grunt);
            for (int i = 0; i < wave.brutes; i++) SpawnAtDoor(Combat.Config.brute);
        }

        // Each vent throws up fire, frost or oil again, depending on the biome the dungeon lies under.
        void Vents(float dt)
        {
            if (map.Vents.Count == 0 || (ventTimer -= dt) > 0f) return;
            ventTimer = VentEvery;
            var state = Combat.Scenery.Hazard(dungeonBiome);
            foreach (var vent in map.Vents)
                Combat.Zones.Spawn(state, vent, VentRadius, VentLasts, VentDamage, hostile: true);
        }

        // Enemies of a wave come in at the places the map marks for them, away from the machine where possible.
        void SpawnAtDoor(EnemyDef def)
        {
            Vector3 rig = Combat.Rig.transform.position, door = map.Spawns[Random.Range(0, map.Spawns.Count)];
            for (int tries = 0; tries < 4 && (door - rig).sqrMagnitude < 100f; tries++)
                door = map.Spawns[Random.Range(0, map.Spawns.Count)];
            door += new Vector3(Random.Range(-1.6f, 1.6f), 0f, Random.Range(-1.6f, 1.6f));
            Combat.Enemies.Spawn(def, map.Resolve(door, def.radius, out _, out _));
        }

        // The way out: the dark closes over the machine, and opens again at the entrance.
        IEnumerator ClimbOut()
        {
            Busy = true;
            Combat.Rig.Scripted = true;
            for (float t = 0f; t < WipeTime; t += Time.deltaTime)
            {
                IrisRadius = Mathf.Lerp(CameraRig.IrisWide, 0f, Mathf.SmoothStep(0f, 1f, t / WipeTime));
                yield return null;
            }
            IrisRadius = 0f;
            LeaveDungeon();
            yield return null;
            for (float t = 0f; t < WipeTime; t += Time.deltaTime)
            {
                IrisRadius = Mathf.Lerp(0f, CameraRig.IrisWide, Mathf.SmoothStep(0f, 1f, t / WipeTime));
                yield return null;
            }
            IrisRadius = float.PositiveInfinity;
            Combat.Rig.Scripted = false;
            Busy = false;
        }

        void LeaveDungeon()
        {
            var def = Dungeon(DungeonIndex);
            cleared.Add(DungeonIndex);
            Profile.NoteCleared(def);

            // What a cleared dungeon pays: Beats, more the harder it is, and whatever was kept in it.
            // The line at the top only says that it is cleared; what it paid is in the log. The Beats
            // are carried like any others; the parts go home at once.
            RunStats.Beats += def.reward;
            Loot.Beats(def.reward);
            foreach (var part in def.finds)
                if (Profile.Find(part)) Loot.Part(part);
            banner = $"{def.displayName} cleared";
            bannerLeft = BannerSeconds;

            ClearField();
            Boss.End();
            crumble.Clear();
            Combat.Scenery.LeaveDungeon();
            map = null;
            Mode = RunMode.OpenWorld;
            DungeonIndex = -1;
            Waves = null;
            Combat.Rig.Repair();
            MoveRig(returnPos, Vector3.zero, world.halfSize + world.stormDepth, circular: false);
            // The Beats arrive as a shower of sparks in their colour.
            Combat.Vfx.Rise(returnPos, 3f, 0f, Tokens.PulseGreen, 40, 4f, 0.2f, 0.9f);
            // What the dungeon held besides: an Echo, lying where the machine comes out.
            if (Combat.Pickups != null) Combat.Pickups.Give(DropKind.Echo, returnPos + Vector3.back * EchoAhead);
            PaintGates();
        }

        // ---- Helpers ----

        static void ClearField()
        {
            Combat.Enemies.Clear();
            Combat.Zones.Clear();
            Combat.Projectiles.Clear();
            if (Combat.Pickups != null) Combat.Pickups.Clear();
            if (Combat.Marks != null) Combat.Marks.Clear();
        }

        static void MoveRig(Vector3 pos, Vector3 boundsCenter, float boundsRadius, bool circular)
        {
            Combat.Rig.SetBounds(boundsCenter, boundsRadius, circular);
            Combat.Rig.Teleport(pos);
            if (Combat.Camera != null) Combat.Camera.Snap();
        }

        // Open entrances are Pulse green, cleared ones chalk, locked ones grey.
        void PaintGates()
        {
            var config = Combat.Config;
            for (int i = 0; i < gates.Length; i++)
            {
                var status = cleared.Contains(i) ? config.body
                    : WorldRules.IsOpen(world, i, cleared) ? config.pulse : config.part;
                gates[i].sharedMaterial = status;
                if (landmarks[i] == null) continue;
                var def = Dungeon(i);
                landmarks[i].sharedMaterials = Slots.For(landmarks[i].GetComponent<MeshFilter>().sharedMesh,
                    Slots.Or(def.landmarkStone, config.part), Slots.Or(def.landmarkTrim, config.body), config.dark, status);
            }
        }

        MeshRenderer Disc(string label, Vector3 pos, float radius, float height, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = label;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.position = pos + Vector3.up * height;
            go.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }
    }
}
