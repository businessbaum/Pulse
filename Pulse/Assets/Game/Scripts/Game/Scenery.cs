using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pulse
{
    // Builds and draws the world around the machine: the floor, the props of each biome, the rim of
    // the map and the pillars of a dungeon. Props are instanced; none is a GameObject.
    // Small props are flattened when the machine drives over them. Big ones block it: soft ones such
    // as trees and wrecks shake when hit, hard ones such as rock and ice throw the machine back.
    public sealed class Scenery : MonoBehaviour
    {
        sealed class Batch
        {
            public Mesh mesh;
            public Material[] materials;
            public bool shadows, crushable;
            public Color color;
            public readonly List<Matrix4x4> matrices = new List<Matrix4x4>();
        }

        sealed class Blocker
        {
            public Vector3 pos;
            public float radius;
            // Rock and ice: it does not move, the machine bounces off instead.
            public bool hard;
            public Batch batch;
            public int index;
            // Where the prop stands when nothing shakes it.
            public Matrix4x4 rest;
            // touching: the machine is against it right now. shaking: its shake is still playing.
            public bool touching, shaking;
        }

        struct Shake
        {
            public Blocker blocker;
            public float start;
        }

        // The floor runs FloorMargin past the edge of the map, far into the storm that surrounds it.
        const int BatchSize = 1023, FloorCells = 204;
        const float ClearAroundStart = 12f, ClearAroundGate = 10f, FloorMargin = 60f;
        // The strip south of the starting point that the machine races along when it arrives.
        // Nothing solid stands in it, and its far part is bare ground, so the machine comes out of
        // the dark onto nothing but floor.
        public const float LaneLength = 48f, LaneHalfWidth = 5f;
        // BumpSpeed: the share of top speed an impact needs before a prop shakes.
        // LetGo: how far the machine must move away before the same prop reacts again. It is larger for
        // hard props, because the bounce itself throws the machine back most of a metre.
        const float CrushRadius = 1.25f, BumpSpeed = 0.5f, LetGo = 0.3f, LetGoHard = 2.5f, ShakeTime = 0.45f, ShakeAngle = 7f;

        readonly List<Batch> worldBatches = new List<Batch>(), dungeonBatches = new List<Batch>();
        readonly List<Blocker> blockers = new List<Blocker>();
        readonly List<Shake> shakes = new List<Shake>();
        readonly Matrix4x4[] buffer = new Matrix4x4[BatchSize];
        // What stands in the world without being scenery, such as a cache: solid, but drawn by its owner.
        readonly Batch loose = new Batch { materials = new Material[0], color = Tokens.Dust };
        SceneryDef def;
        WorldDef land;
        GameConfig config;
        DungeonMap map;
        GameObject dungeonFloor;
        float flowIn, wallFree;
        bool inDungeon, wallTouch;

        public int BiomeCount => def != null ? def.biomes.Length : 0;
        // The biome the machine is in right now. The HUD shows its name.
        public string BiomeName { get; private set; } = "Open world";

        public void BuildWorld(WorldDef world, GameConfig gameConfig)
        {
            config = gameConfig;
            land = world;
            def = config.scenery;
            if (def == null || def.biomes.Length == 0) return;
            var random = new System.Random(def.seed);

            // The floor replaces the plain ground plane of the scene.
            var ground = GameObject.Find("Ground");
            if (ground != null)
            {
                ground.transform.localScale = Vector3.one;
                ground.GetComponent<MeshFilter>().sharedMesh =
                    GroundBuilder.World(world.halfSize + FloorMargin, FloorCells, def.biomes.Length, def.seed);
                ground.GetComponent<MeshRenderer>().sharedMaterial =
                    new Material(config.ground) { mainTexture = GroundBuilder.Palette(def.biomes), color = Color.white };
            }

            float half = world.halfSize - 3f;
            int flatCount = 0;
            for (int b = 0; b < def.biomes.Length; b++)
            {
                var biome = def.biomes[b];
                foreach (var prop in biome.props)
                {
                    if (prop.mesh == null) continue;
                    var batch = NewBatch(prop, biome.glow, worldBatches);
                    for (int placed = 0, tries = 0; placed < prop.count && tries < prop.count * 30; tries++)
                    {
                        var pos = new Vector3(Range(random, -half, half), 0f, Range(random, -half, half));
                        if (Biomes.At(pos, def.biomes.Length) != b) continue;
                        float scale = Range(random, prop.minScale, prop.maxScale);
                        float block = prop.blockRadius * scale;
                        bool solid = block > 0f;
                        if (solid && (pos.magnitude < ClearAroundStart || InLane(pos, block) || NearGate(world, pos) || Overlaps(pos, block + 1.5f))) continue;
                        if (prop.flat && NearGate(world, pos)) continue;
                        if (UnderLandmark(world, pos, block)) continue;
                        if (!solid && pos.z < -ClearAroundStart && InLane(pos, 0f)) continue;

                        // Flat props are stacked a hair apart so overlapping ones do not flicker.
                        if (prop.flat) pos.y = 0.01f + flatCount++ * 0.0003f;
                        var lean = Quaternion.Euler(Range(random, -prop.tilt, prop.tilt), Range(random, 0f, 360f), Range(random, -prop.tilt, prop.tilt));
                        var matrix = Matrix4x4.TRS(pos, lean, Vector3.one * scale);
                        if (solid) blockers.Add(new Blocker { pos = pos, radius = block, hard = prop.hard, batch = batch, index = batch.matrices.Count, rest = matrix });
                        batch.matrices.Add(matrix);
                        placed++;
                    }
                }
            }

            // What stands behind an entrance, such as a keep, is solid: a row of blockers along its
            // front wall, and a short one behind it where the keep itself stands.
            foreach (var entrance in world.entrances)
            {
                var dungeon = entrance.dungeon;
                if (dungeon == null || dungeon.landmark == null || dungeon.landmarkRadius <= 0f) continue;
                var stone = new Batch { materials = new Material[0], color = Tokens.Dust };
                Vector3 middle = Landmark(entrance);
                float reach = dungeon.landmarkRadius - 5f;
                int count = Mathf.CeilToInt(reach * 2f / 4.8f) + 1;
                for (int k = 0; k < count; k++)
                {
                    float x = Mathf.Lerp(-reach, reach, k / (count - 1f));
                    blockers.Add(new Blocker { pos = middle + new Vector3(x, 0f, 1f), radius = 3.4f, hard = true, batch = stone, index = -1 });
                    if (Mathf.Abs(x) < reach * 0.5f)
                        blockers.Add(new Blocker { pos = middle + new Vector3(x, 0f, 6f), radius = 3.4f, hard = true, batch = stone, index = -1 });
                }
            }

            // A wall of rock around the square world, but only where no storm marks its edge.
            if (def.border.mesh != null && world.stormDepth <= 0f)
            {
                var batch = NewBatch(def.border, null, worldBatches);
                float edge = world.halfSize + 3.5f;
                for (float t = -edge; t <= edge; t += def.borderSpacing)
                foreach (var pos in new[] { new Vector3(t, 0f, edge), new Vector3(t, 0f, -edge), new Vector3(edge, 0f, t), new Vector3(-edge, 0f, t) })
                    batch.matrices.Add(Matrix4x4.TRS(
                        pos + new Vector3(Range(random, -1.5f, 1.5f), 0f, Range(random, -1.5f, 1.5f)),
                        Quaternion.Euler(0f, Range(random, 0f, 360f), 0f),
                        Vector3.one * Range(random, def.border.minScale, def.border.maxScale)));
            }
        }

        // Builds a dungeon from its map: a paved floor in the colours of the biome its entrance stands
        // in, a block for every wall cell, pillars, and the stands of an arena. They glow in that biome's colour.
        public void EnterDungeon(DungeonMap dungeonMap, int biomeIndex)
        {
            inDungeon = true;
            map = dungeonMap;
            flowIn = 0f;
            wallTouch = false;
            dungeonBatches.Clear();
            if (def == null || def.biomes.Length == 0) return;
            var biome = def.biomes[Mathf.Clamp(biomeIndex, 0, def.biomes.Length - 1)];

            if (dungeonFloor == null)
            {
                dungeonFloor = new GameObject("Dungeon floor");
                dungeonFloor.transform.SetParent(transform, false);
                dungeonFloor.AddComponent<MeshFilter>();
                dungeonFloor.AddComponent<MeshRenderer>();
            }
            dungeonFloor.SetActive(true);
            dungeonFloor.GetComponent<MeshFilter>().sharedMesh = GroundBuilder.Dungeon(map);
            dungeonFloor.GetComponent<MeshRenderer>().sharedMaterial = new Material(config.ground)
            {
                mainTexture = GroundBuilder.Palette(biome.ground, biome.groundAlt, biome.groundSpot), color = Color.white,
            };

            Fill(def.wall, biome.glow, map.Walls, faceCenter: false);
            Fill(def.pillar, biome.glow, map.Pillars, faceCenter: false);
            Fill(def.stand, biome.glow, map.Stands, faceCenter: true);
        }

        // One instance of a prop on every given cell. Walls are turned by quarter turns so that they do
        // not all look alike; stands face the middle of the map.
        void Fill(SceneryDef.Prop prop, Material glow, List<Vector3> cells, bool faceCenter)
        {
            if (prop.mesh == null || cells.Count == 0) return;
            var batch = NewBatch(prop, glow, dungeonBatches);
            foreach (var cell in cells)
            {
                float turn;
                if (faceCenter)
                {
                    Vector3 inward = map.Center - cell;
                    turn = Mathf.Round(Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg / 90f) * 90f;
                }
                else turn = Mathf.Abs(Mathf.RoundToInt(cell.x * 3f + cell.z * 7f)) % 4 * 90f;
                batch.matrices.Add(Matrix4x4.TRS(cell, Quaternion.Euler(0f, turn, 0f), Vector3.one));
            }
        }

        public void LeaveDungeon()
        {
            inDungeon = false;
            map = null;
            if (dungeonFloor != null) dungeonFloor.SetActive(false);
        }

        // Whether a wall or pillar of the current dungeon is at that point. Shots stop there.
        public bool Blocks(Vector3 pos) => inDungeon && map != null && map.Blocked(pos);

        // Whether a wall of the current dungeon lies between two points.
        public bool Hidden(Vector3 from, Vector3 to) => inDungeon && map != null && !map.Sight(from, to);

        // Which way to walk from one point to reach another. In the open, and wherever the target can
        // be seen, that is straight at it. Behind a wall it is the way around.
        public Vector3 Towards(Vector3 from, Vector3 target, Vector3 straight)
        {
            if (!inDungeon || map == null || map.Sight(from, target)) return straight;
            Vector3 around = map.Direction(from);
            return around == Vector3.zero ? straight : around;
        }

        // What the vents of a dungeon under that biome throw up.
        public StateType Hazard(int biomeIndex) =>
            def == null || def.biomes.Length == 0 ? StateType.Heat : def.biomes[Mathf.Clamp(biomeIndex, 0, def.biomes.Length - 1)].hazard;

        // Moves a circle out of everything solid it touches: props in the open world, walls and pillars in a dungeon.
        // For the machine, pass its speed as a share of top speed. Running into a prop at half speed or
        // more has one effect: a soft prop shakes, a hard one throws the machine back. Nothing more
        // happens until the machine has let go of the prop.
        public Vector3 Resolve(Vector3 pos, float radius, float machineSpeed = -1f)
        {
            bool machine = machineSpeed >= 0f;
            if (inDungeon)
            {
                if (map == null) return pos;
                pos = map.Resolve(pos, radius, out bool hit, out Vector3 normal);
                if (!machine) return pos;
                // A wall is hard: hit squarely at speed it throws the machine back, once per approach.
                if (hit)
                {
                    if (!wallTouch && machineSpeed >= BumpSpeed && Combat.Rig.BounceOff(pos - normal))
                        Combat.Vfx.Burst(pos - normal * radius + Vector3.up * 0.6f, Tokens.Dust, 12, 5f, 0.2f, 0.6f, falls: true);
                    wallTouch = true;
                    wallFree = 0f;
                }
                else if ((wallFree += Time.deltaTime) > 0.4f) wallTouch = false;
                return pos;
            }
            foreach (var blocker in blockers)
            {
                Vector3 away = pos - blocker.pos;
                away.y = 0f;
                float reach = radius + blocker.radius, sq = away.sqrMagnitude;
                if (sq >= reach * reach || sq < 0.0001f)
                {
                    float free = reach + (blocker.hard ? LetGoHard : LetGo);
                    if (machine && sq > free * free) blocker.touching = false;
                    continue;
                }
                float dist = Mathf.Sqrt(sq);
                pos += away / dist * (reach - dist);
                if (!machine) continue;
                if (!blocker.touching && !blocker.shaking && machineSpeed >= BumpSpeed)
                    Bump(blocker, blocker.pos + away / dist * blocker.radius);
                blocker.touching = true;
            }
            return pos;
        }

        void Bump(Blocker blocker, Vector3 contact)
        {
            if (blocker.hard)
            {
                // The prop stays put. A glancing touch does nothing; a square hit chips it and bounces the machine.
                if (Combat.Rig.BounceOff(blocker.pos))
                    Combat.Vfx.Burst(contact + Vector3.up * 0.6f, blocker.batch.color, 12, 5f, 0.2f, 0.6f, falls: true);
                return;
            }
            blocker.shaking = true;
            shakes.Add(new Shake { blocker = blocker, start = Time.time });
            Combat.Vfx.Burst(contact + Vector3.up * 0.6f, blocker.batch.color, 9, 4f, 0.2f, 0.55f, falls: true);
            Combat.Vfx.Sparks(contact + Vector3.up * 0.6f, Tokens.Chalk, 4, 5f);
            if (Combat.Camera != null) Combat.Camera.Shake(0.12f);
        }

        // Whether something can be put down at that spot: nothing solid in the way, not on an
        // entrance, under a landmark or in the lane the machine arrives along.
        public bool Free(Vector3 pos, float radius) =>
            land != null && !InLane(pos, radius) && !NearGate(land, pos) && !UnderLandmark(land, pos, radius) && !Overlaps(pos, radius);

        // Makes a spot solid for the machine and for enemies. It is hard: the machine bounces off it.
        public void Block(Vector3 pos, float radius)
        {
            pos.y = 0f;
            blockers.Add(new Blocker { pos = pos, radius = radius, hard = true, batch = loose, index = -1 });
        }

        // The species that live where this point is. Empty when the biome lists none.
        public EnemyDef[] SpeciesAt(Vector3 pos)
        {
            if (def == null || def.biomes.Length == 0) return System.Array.Empty<EnemyDef>();
            return def.biomes[Biomes.At(pos, def.biomes.Length)].species ?? System.Array.Empty<EnemyDef>();
        }

        void Update()
        {
            if (def == null) return;
            var rig = Combat.Rig;
            Vector3 center = rig != null ? rig.transform.position : Vector3.zero;

            if (!inDungeon && def.biomes.Length > 0)
            {
                // The ambient light drifts toward the colour of the biome the machine is in.
                var biome = def.biomes[Biomes.At(center, def.biomes.Length)];
                BiomeName = biome.name;
                RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, biome.ambient, 1f - Mathf.Exp(-2f * Time.deltaTime));
                if (rig != null && rig.Alive && rig.Moving) Crush(center);
            }
            RunShakes();
            // In a dungeon, enemies find their way around walls. The way is worked out a few times a second.
            if (inDungeon && map != null && (flowIn -= Time.deltaTime) <= 0f)
            {
                flowIn = 0.15f;
                map.Flow(center);
            }

            foreach (var batch in inDungeon ? dungeonBatches : worldBatches)
                for (int sub = 0; sub < batch.materials.Length; sub++)
                    Draw(batch, sub, center);
        }

        // Small props under the machine are flattened: they vanish in a puff of their own colour.
        void Crush(Vector3 center)
        {
            foreach (var batch in worldBatches)
            {
                if (!batch.crushable) continue;
                for (int i = batch.matrices.Count - 1; i >= 0; i--)
                {
                    Vector3 pos = batch.matrices[i].GetColumn(3);
                    float dx = pos.x - center.x, dz = pos.z - center.z;
                    if (dx * dx + dz * dz > CrushRadius * CrushRadius) continue;
                    Combat.Vfx.Burst(pos + Vector3.up * 0.25f, batch.color, 7, 3.5f, 0.16f, 0.5f, falls: true);
                    RunStats.Flattened++;
                    batch.matrices[i] = batch.matrices[batch.matrices.Count - 1];
                    batch.matrices.RemoveAt(batch.matrices.Count - 1);
                }
            }
        }

        // A bumped prop rocks back and forth for a moment and comes to rest where it stood.
        void RunShakes()
        {
            for (int s = shakes.Count - 1; s >= 0; s--)
            {
                var blocker = shakes[s].blocker;
                float t = (Time.time - shakes[s].start) / ShakeTime;
                if (t >= 1f)
                {
                    blocker.batch.matrices[blocker.index] = blocker.rest;
                    blocker.shaking = false;
                    shakes.RemoveAt(s);
                    continue;
                }
                float swing = Mathf.Sin(t * Mathf.PI * 5f) * (1f - t) * ShakeAngle;
                blocker.batch.matrices[blocker.index] = blocker.rest * Matrix4x4.Rotate(Quaternion.Euler(swing, 0f, swing * 0.6f));
            }
        }

        void Draw(Batch batch, int submesh, Vector3 center)
        {
            var renderParams = new RenderParams(batch.materials[submesh])
            {
                shadowCastingMode = batch.shadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows = true,
                worldBounds = new Bounds(center, Vector3.one * 600f),
            };
            for (int start = 0; start < batch.matrices.Count; start += BatchSize)
            {
                int count = Mathf.Min(BatchSize, batch.matrices.Count - start);
                batch.matrices.CopyTo(start, buffer, 0, count);
                Graphics.RenderMeshInstanced(renderParams, batch.mesh, submesh, buffer, count);
            }
        }

        Batch NewBatch(SceneryDef.Prop prop, Material glow, List<Batch> into)
        {
            var batch = new Batch
            {
                mesh = prop.mesh, shadows = !prop.noShadow && !prop.flat, crushable = prop.crushable,
                materials = Slots.For(prop.mesh, prop.material, prop.accent, config.dark, glow),
                // Dust and splinters take the colour most of the prop is made of.
                color = Slots.Or(prop.crushable ? prop.accent : prop.material, prop.material).color,
            };
            into.Add(batch);
            return batch;
        }

        // The far part of that strip. Its floor is one plain shade, the same as the ground outside the
        // lobby gate, so the machine drives from one onto the other without the floor changing.
        public static bool OnArrivalTrack(Vector3 pos) =>
            Mathf.Abs(pos.x) < LaneHalfWidth + 2f && pos.z < -ClearAroundStart && pos.z > -LaneLength - 6f;

        static bool InLane(Vector3 pos, float radius) =>
            Mathf.Abs(pos.x) < LaneHalfWidth + radius && pos.z < 0f && pos.z > -LaneLength - radius;

        // Where the landmark behind an entrance stands.
        static Vector3 Landmark(WorldDef.Entrance entrance) =>
            entrance.position + Vector3.forward * (RunDirector.GateRadius + entrance.dungeon.landmarkDepth);

        // Whether a prop there would stand inside a landmark. Nothing grows under a keep.
        static bool UnderLandmark(WorldDef world, Vector3 pos, float radius)
        {
            foreach (var entrance in world.entrances)
            {
                var dungeon = entrance.dungeon;
                if (dungeon == null || dungeon.landmark == null || dungeon.landmarkRadius <= 0f) continue;
                Vector3 to = Landmark(entrance) - pos;
                to.y = 0f;
                float clear = dungeon.landmarkRadius + radius;
                if (to.sqrMagnitude < clear * clear) return true;
            }
            return false;
        }

        static bool NearGate(WorldDef world, Vector3 pos)
        {
            foreach (var entrance in world.entrances)
            {
                Vector3 to = entrance.position - pos;
                to.y = 0f;
                if (to.sqrMagnitude < ClearAroundGate * ClearAroundGate) return true;
            }
            return false;
        }

        bool Overlaps(Vector3 pos, float radius)
        {
            foreach (var blocker in blockers)
            {
                float reach = radius + blocker.radius;
                if ((blocker.pos - pos).sqrMagnitude < reach * reach) return true;
            }
            return false;
        }

        static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);
    }
}
