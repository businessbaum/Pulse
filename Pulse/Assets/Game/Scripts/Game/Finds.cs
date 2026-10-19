using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pulse
{
    // What there is to find in the open world besides entrances. They stand somewhere else in every
    // run, so the map is worth driving across each time.
    //
    //   Veins   Beats that have grown out of the ground. Driving into one breaks it, and the Beats
    //           fly out of it.
    //   Caches  Crates the Roundhouse once sent out and never got back. One opens for a machine
    //           that stays next to it for a few seconds. The Strays around hear it being opened
    //           and come. It holds a part, Beats and a heart.
    public sealed class Finds : MonoBehaviour
    {
        struct Vein
        {
            public Vector3 pos;
            public float turn, size;
            // One in five holds a cluster on top of its single Beats.
            public bool rich, broken;
        }

        sealed class Cache
        {
            public Vector3 pos;
            public float turn, progress, lidLeft;
            // roused: the Strays around have heard it. open: it has given up what it held.
            public bool roused, open;
        }

        const int VeinsPerBiome = 9, CachesPerBiome = 2;
        // How near the machine must come to break a vein, and how far it may be from a cache it is opening.
        const float VeinReach = 1f, CacheReach = 4.2f, CacheBlock = 1.2f;
        // Opening takes OpenTime seconds next to the cache. Away from it, the progress runs back.
        const float OpenTime = 6f, RunBack = 0.5f;
        // Nothing stands this close to where the machine arrives, or to another of its kind.
        const float VeinFromStart = 22f, VeinApart = 11f, CacheFromStart = 46f, CacheApart = 34f;
        // The Strays a cache rouses come from just outside the picture.
        const float AmbushNear = 23f, AmbushFar = 27f;
        const float DrawWithin = 75f;

        readonly List<Vein> veins = new List<Vein>();
        readonly List<Cache> caches = new List<Cache>();
        readonly Matrix4x4[] buffer = new Matrix4x4[64];
        GameConfig config;
        Material[] veinLook, cacheLook, openLook;
        Cache opening;

        public int VeinsLeft
        {
            get
            {
                int left = 0;
                foreach (var vein in veins) if (!vein.broken) left++;
                return left;
            }
        }

        public int CachesLeft
        {
            get
            {
                int left = 0;
                foreach (var cache in caches) if (!cache.open) left++;
                return left;
            }
        }

        // From 0 to 1 while a cache is being opened; below zero when none is.
        public float Opening => opening != null ? opening.progress : -1f;

        // Where the nearest unbroken vein and the nearest closed cache are. Tests drive to them.
        public bool NearestVein(Vector3 from, out Vector3 pos)
        {
            pos = default;
            float best = float.MaxValue;
            foreach (var vein in veins)
            {
                float sq = (vein.pos - from).sqrMagnitude;
                if (vein.broken || sq >= best) continue;
                best = sq;
                pos = vein.pos;
            }
            return best < float.MaxValue;
        }

        public bool NearestCache(Vector3 from, out Vector3 pos)
        {
            pos = default;
            float best = float.MaxValue;
            foreach (var cache in caches)
            {
                float sq = (cache.pos - from).sqrMagnitude;
                if (cache.open || sq >= best) continue;
                best = sq;
                pos = cache.pos;
            }
            return best < float.MaxValue;
        }

        // Scatters veins and caches over the map, the same number in every biome. seed decides where.
        public void Build(WorldDef world, GameConfig gameConfig, int seed)
        {
            config = gameConfig;
            veins.Clear();
            caches.Clear();
            var scenery = Combat.Scenery;
            int biomes = Mathf.Max(1, scenery.BiomeCount);
            var random = new System.Random(seed);
            float half = world.halfSize - 8f;
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

            for (int b = 0; b < biomes; b++)
            {
                for (int placed = 0, tries = 0; placed < CachesPerBiome && tries < 400; tries++)
                {
                    var pos = new Vector3(Range(-half, half), 0f, Range(-half, half));
                    if (Biomes.At(pos, biomes) != b || pos.magnitude < CacheFromStart || !scenery.Free(pos, CacheBlock + 2.5f)) continue;
                    if (caches.Exists(c => (c.pos - pos).sqrMagnitude < CacheApart * CacheApart)) continue;
                    caches.Add(new Cache { pos = pos, turn = Range(0f, 360f) });
                    scenery.Block(pos, CacheBlock);
                    placed++;
                }
                for (int placed = 0, tries = 0; placed < VeinsPerBiome && tries < 600; tries++)
                {
                    var pos = new Vector3(Range(-half, half), 0f, Range(-half, half));
                    if (Biomes.At(pos, biomes) != b || pos.magnitude < VeinFromStart || !scenery.Free(pos, 1.6f)) continue;
                    if (veins.Exists(v => (v.pos - pos).sqrMagnitude < VeinApart * VeinApart)) continue;
                    if (caches.Exists(c => (c.pos - pos).sqrMagnitude < 36f)) continue;
                    veins.Add(new Vein { pos = pos, turn = Range(0f, 360f), size = Range(0.85f, 1.25f), rich = random.Next(5) == 0 });
                    placed++;
                }
            }

            if (config.veinMesh != null)
                veinLook = Slots.For(config.veinMesh, Slots.Or(config.dark, config.part), config.part, config.dark, config.pulse);
            if (config.cacheMesh != null)
                cacheLook = Slots.For(config.cacheMesh, config.part, config.body, config.dark, Slots.Or(config.flash, config.body));
            if (config.cacheOpenMesh != null)
                openLook = Slots.For(config.cacheOpenMesh, config.part, config.body, config.dark, config.dark);
        }

        void Update()
        {
            var rig = Combat.Rig;
            if (config == null || rig == null || Combat.Director == null || Combat.Director.Mode != RunMode.OpenWorld)
            {
                opening = null;
                return;
            }
            float dt = Time.deltaTime;
            Vector3 at = rig.transform.position;
            bool free = rig.Alive && !rig.Scripted && !Combat.Director.Busy;

            if (free && dt > 0f) BreakVeins(rig, at);
            TendCaches(at, free, dt);
            Draw(at);
        }

        // A vein the machine drives into breaks, and its Beats fly out. They still have to be picked up.
        void BreakVeins(Rig rig, Vector3 at)
        {
            float reach = VeinReach + rig.Radius * 0.6f;
            for (int i = 0; i < veins.Count; i++)
            {
                var vein = veins[i];
                if (vein.broken || (vein.pos - at).sqrMagnitude > reach * reach) continue;
                vein.broken = true;
                veins[i] = vein;
                RunStats.Veins++;
                int singles = vein.rich ? 4 : Random.Range(3, 6);
                for (int k = 0; k < singles; k++) Combat.Pickups.Give(DropKind.Beat, vein.pos, 1f, scatter: true);
                if (vein.rich) Combat.Pickups.Give(DropKind.Beat, vein.pos, 5f, scatter: true);
                Combat.Vfx.Burst(vein.pos + Vector3.up * 0.6f, Tokens.PulseGreen, 16, 8f, 0.2f, 0.6f, falls: true);
                Combat.Vfx.Burst(vein.pos + Vector3.up * 0.3f, Tokens.Dust, 10, 5f, 0.22f, 0.6f, falls: true);
                Combat.Vfx.Sparks(vein.pos + Vector3.up * 0.7f, Tokens.PulseGreen, 10, 9f);
                if (Combat.Camera != null) Combat.Camera.Shake(0.14f);
            }
        }

        // A cache opens for a machine that stays next to it. The moment the machine starts on it,
        // the Strays of that place hear it and come.
        void TendCaches(Vector3 at, bool free, float dt)
        {
            opening = null;
            foreach (var cache in caches)
            {
                if (cache.open)
                {
                    cache.lidLeft -= dt;
                    continue;
                }
                bool near = free && (cache.pos - at).sqrMagnitude < CacheReach * CacheReach;
                if (!near)
                {
                    cache.progress = Mathf.Max(0f, cache.progress - RunBack * dt);
                    // A spark now and then, so that it is not driven past.
                    if ((cache.pos - at).sqrMagnitude < DrawWithin * DrawWithin && Random.value < dt * 2.5f)
                        Combat.Vfx.Rise(cache.pos + Vector3.up * 0.9f, 0.5f, 0f, Tokens.Chalk, 1, 2.6f, 0.1f, 0.8f);
                    continue;
                }
                if (!cache.roused)
                {
                    cache.roused = true;
                    Rouse(cache.pos);
                }
                cache.progress += dt / OpenTime;
                opening = cache;
                // Light is drawn into its lock, more as it gives.
                if (dt > 0f) Combat.Vfx.Gather(cache.pos + Vector3.up * 0.8f, 2.2f, Tokens.PulseGreen, Vfx.Share(dt * (10f + 40f * cache.progress)), 0.35f);
                if (cache.progress >= 1f) Open(cache);
            }
        }

        // What the cache held jumps out of it.
        void Open(Cache cache)
        {
            cache.open = true;
            cache.lidLeft = 0.5f;
            opening = null;
            RunStats.Caches++;
            var pickups = Combat.Pickups;
            pickups.Give(DropKind.Part, cache.pos, item: Tuning.Roll(config, Rank.MiniBoss, () => Random.value), scatter: true);
            pickups.Give(DropKind.Beat, cache.pos, 5f, scatter: true);
            pickups.Give(DropKind.Beat, cache.pos, 5f, scatter: true);
            for (int k = 0; k < 4; k++) pickups.Give(DropKind.Beat, cache.pos, 1f, scatter: true);
            pickups.Give(DropKind.Heart, cache.pos, scatter: true);
            Combat.Vfx.Burst(cache.pos + Vector3.up * 0.9f, Tokens.Chalk, 26, 9f, 0.24f, 0.8f, falls: true);
            Combat.Vfx.Sparks(cache.pos + Vector3.up, Tokens.PulseGreen, 24, 12f, 0.7f, 0.4f);
            Combat.Vfx.Shockwave(cache.pos, 4f, Tokens.Chalk, 30, 0.35f);
            if (Combat.Camera != null) Combat.Camera.Shake(0.3f);
        }

        // One kind of Stray that lives there, in a ring around the cache, already on its way.
        void Rouse(Vector3 pos)
        {
            var species = Combat.Scenery.SpeciesAt(pos);
            var def = species.Length > 0 ? species[Random.Range(0, species.Length)] : config.grunt;
            if (def == null) return;
            float edge = config.world.halfSize - 2f;
            int count = def.packSize > 1 ? 8 : 5;
            for (int k = 0; k < count; k++)
            {
                float angle = (k + Random.value * 0.6f) * Mathf.PI * 2f / count;
                Vector3 at = pos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(AmbushNear, AmbushFar);
                at.x = Mathf.Clamp(at.x, -edge, edge);
                at.z = Mathf.Clamp(at.z, -edge, edge);
                Combat.Enemies.Spawn(def, at, aggro: true);
            }
        }

        void Draw(Vector3 at)
        {
            if (config.veinMesh != null && veinLook != null)
            {
                // Like every piece of the Pulse, a vein throbs in its rhythm.
                float throb = 1f + 0.05f * Heart.Beat(Time.time);
                int count = 0;
                foreach (var vein in veins)
                {
                    if (vein.broken || count >= buffer.Length || (vein.pos - at).sqrMagnitude > DrawWithin * DrawWithin) continue;
                    buffer[count++] = Matrix4x4.TRS(vein.pos, Quaternion.Euler(0f, vein.turn, 0f), Vector3.one * (vein.size * throb));
                }
                if (count > 0) Instanced(config.veinMesh, veinLook, count, at);
            }

            foreach (var cache in caches)
            {
                if ((cache.pos - at).sqrMagnitude > DrawWithin * DrawWithin) continue;
                var mesh = cache.open && config.cacheOpenMesh != null ? config.cacheOpenMesh : config.cacheMesh;
                var look = cache.open && openLook != null ? openLook : cacheLook;
                if (mesh == null || look == null) continue;
                // It shakes as it gives, and jumps once when it opens.
                float shake = cache.open ? 0f : Mathf.Sin(Time.time * 46f) * 1.6f * cache.progress;
                float jump = cache.open && cache.lidLeft > 0f ? Mathf.Sin(cache.lidLeft / 0.5f * Mathf.PI) * 0.25f : 0f;
                var matrix = Matrix4x4.TRS(cache.pos + Vector3.up * jump, Quaternion.Euler(shake, cache.turn, -shake), Vector3.one);
                for (int sub = 0; sub < mesh.subMeshCount && sub < look.Length; sub++)
                    Graphics.RenderMesh(new RenderParams(look[sub]) { shadowCastingMode = ShadowCastingMode.On, receiveShadows = true }, mesh, sub, matrix);
            }
        }

        void Instanced(Mesh mesh, Material[] look, int count, Vector3 center)
        {
            for (int sub = 0; sub < mesh.subMeshCount && sub < look.Length; sub++)
            {
                var renderParams = new RenderParams(look[sub])
                {
                    shadowCastingMode = ShadowCastingMode.On, receiveShadows = true,
                    worldBounds = new Bounds(center, Vector3.one * 300f),
                };
                Graphics.RenderMeshInstanced(renderParams, mesh, sub, buffer, count);
            }
        }
    }
}
