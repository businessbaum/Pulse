using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pulse
{
    // Things enemies drop. A drop hops out of the wreck and lies on the ground, turning slowly.
    // The machine has to drive onto it to take it. Only a machine with a magnet draws drops in from
    // a distance, and nothing gives it one yet. What drops is decided by a DropPool.
    public sealed class PickupSystem : MonoBehaviour
    {
        struct Pickup
        {
            public Vector3 pos, vel;
            public float age, amount, turn;
            public DropKind kind;
            public Mesh mesh;
            public Material glow;
            public bool landed;
            // For a part: what it is.
            public Item item;
        }

        // Touch: how far past its own edge the machine reaches a drop. Life: seconds before a drop is
        // gone; it blinks for the last Warn seconds.
        const float Touch = 0.4f, MagnetSpeed = 16f, Gravity = 22f, Life = 25f, Warn = 5f;
        const float Hover = 0.55f, Size = 1.5f;
        // A part lies longer than anything else, and throws up a spark now and then so it is not missed.
        const float PartLife = 90f, PartSparkle = 4f, EchoSparkle = 12f, EchoSize = 1.9f;

        readonly List<Pickup> pickups = new List<Pickup>(64);
        readonly List<DropPool.Entry> rolled = new List<DropPool.Entry>(8);
        Mesh cube;

        public int Count => pickups.Count;

        void Awake() => cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

        public void Clear() => pickups.Clear();

        // Rolls the pool for a destroyed enemy and scatters what comes out around the wreck.
        public void Drop(DropPool pool, Rank rank, Vector3 pos)
        {
            if (pool == null) return;
            rolled.Clear();
            pool.Roll(rank, () => Random.value, rolled);
            foreach (var entry in rolled)
            {
                // Heavy Purse: Beats count double, and in return nothing leaves a heart.
                if (entry.kind == DropKind.Heart && Echoes.Has(EchoKind.HeavyPurse)) continue;
                // Which part it is, and how good, depends on who dropped it.
                var item = entry.kind == DropKind.Part ? Tuning.Roll(Combat.Config, rank, () => Random.value) : null;
                if (entry.kind != DropKind.Part || item != null) Spawn(entry, pos, rolled.Count > 1, item);
            }
        }

        // Puts one drop of a kind on the ground, as the common pool of the game describes it. For
        // Beats, amount picks the single one or the cluster.
        public void Give(DropKind kind, Vector3 pos, float amount = 0f, Item item = null, bool scatter = false)
        {
            var pool = Combat.Config.drops;
            if (pool == null || (kind == DropKind.Part && item == null)) return;
            foreach (var entry in pool.entries)
            {
                if (entry.kind != kind || (amount > 0f && !Mathf.Approximately(entry.amount, amount))) continue;
                Spawn(entry, pos, scatter, item);
                return;
            }
        }

        // scatter: thrown further, for several drops out of one wreck.
        public void Spawn(DropPool.Entry entry, Vector3 pos, bool scatter = false, Item item = null)
        {
            float angle = Random.value * Mathf.PI * 2f, push = scatter ? Random.Range(2f, 4.5f) : Random.Range(0.5f, 1.5f);
            pickups.Add(new Pickup
            {
                pos = pos + Vector3.up * 0.6f, kind = entry.kind, amount = entry.amount, mesh = entry.mesh,
                glow = Slots.Or(entry.material, Combat.Config.pulse), item = item,
                vel = new Vector3(Mathf.Cos(angle) * push, Random.Range(6f, 8f), Mathf.Sin(angle) * push),
                turn = Random.value * 360f,
            });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var rig = Combat.Rig;
            var config = Combat.Config;
            Vector3 target = rig.transform.position;

            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                var pickup = pickups[i];
                pickup.age += dt;
                pickup.turn += dt * 120f;
                bool part = pickup.kind == DropKind.Part, echo = pickup.kind == DropKind.Echo;
                // An Echo lies until it is taken.
                float life = part ? PartLife : echo ? float.MaxValue : Life;
                if (pickup.age > life) { pickups.RemoveAt(i); continue; }

                if (!pickup.landed)
                {
                    pickup.vel.y -= Gravity * dt;
                    pickup.pos += pickup.vel * dt;
                    if (pickup.pos.y <= 0f) { pickup.pos.y = 0f; pickup.landed = true; }
                }
                else if (rig.Alive && Wanted(pickup.kind, rig))
                {
                    Vector3 to = target - pickup.pos;
                    to.y = 0f;
                    float dist = to.magnitude;
                    if (dist < rig.Radius + Touch)
                    {
                        Collect(pickup, rig);
                        pickups.RemoveAt(i);
                        continue;
                    }
                    // With a magnet it flies to the machine, faster the closer it is.
                    if (dist < rig.Magnet) pickup.pos += to / dist * (MagnetSpeed * (1.2f - dist / rig.Magnet) * dt);
                }
                pickups[i] = pickup;

                if (part && pickup.landed && Random.value < dt * PartSparkle)
                    Combat.Vfx.Rise(pickup.pos, 0.35f, 0f, Tokens.Chalk, 1, 2.4f, 0.1f, 0.7f);
                // An Echo gives off a thin, steady stream of light.
                if (echo && pickup.landed && Random.value < dt * EchoSparkle)
                    Combat.Vfx.Rise(pickup.pos + Vector3.up * 0.8f, 0.4f, 0f, Tokens.PulseGreen, 1, 3.6f, 0.12f, 0.9f);
                // Blinks before it disappears.
                if (pickup.age > life - Warn && Mathf.Sin(pickup.age * 18f) < 0f) continue;
                float bob = pickup.landed ? Mathf.Sin(pickup.age * 3f) * 0.12f : 0f;
                // Every Beat throbs in the one rhythm of the Pulse. A cluster of five is larger than a single one.
                // A part is larger the better its ability is.
                float size = pickup.kind == DropKind.Beat ? (0.85f + 0.22f * Mathf.Sqrt(pickup.amount)) * (1f + 0.3f * Heart.Beat(Time.time))
                    : part ? 1.1f + 0.12f * (int)pickup.item.rarity
                    : echo ? EchoSize * (1f + 0.16f * Heart.Beat(Time.time)) : Size;
                var body = Matrix4x4.TRS(pickup.pos + Vector3.up * (Hover + bob), Quaternion.Euler(0f, pickup.turn, 0f), Vector3.one * size);
                if (pickup.mesh != null)
                {
                    var materials = Slots.For(pickup.mesh, config.body, config.part, config.dark, pickup.glow);
                    for (int sub = 0; sub < pickup.mesh.subMeshCount && sub < materials.Length; sub++)
                        Graphics.RenderMesh(new RenderParams(materials[sub]) { shadowCastingMode = ShadowCastingMode.On }, pickup.mesh, sub, body);
                }
                else Graphics.RenderMesh(new RenderParams(pickup.glow), cube, 0, body * Matrix4x4.Scale(Vector3.one * 0.4f));
            }
        }

        // A heart is left lying while the hull is whole, so it is still there when it is needed.
        static bool Wanted(DropKind kind, Rig rig) => kind != DropKind.Heart || rig.Hull < rig.MaxHull;

        static void Collect(in Pickup pickup, Rig rig)
        {
            if (pickup.kind == DropKind.Heart)
            {
                rig.Heal(pickup.amount);
                RunStats.Hearts++;
            }
            else if (pickup.kind == DropKind.Beat)
            {
                // Carried, not owned: see RunStats.
                int beats = Mathf.RoundToInt(pickup.amount) * (Echoes.Has(EchoKind.HeavyPurse) ? 2 : 1);
                RunStats.Beats += beats;
                Loot.Beats(beats);
            }
            else if (pickup.kind == DropKind.Echo)
            {
                // A choice of Echoes, or Beats for a machine that cannot hold another.
                if (Echoes.Full)
                {
                    RunStats.Beats += Echoes.Spare;
                    Loot.Beats(Echoes.Spare);
                }
                else Echoes.Owed++;
            }
            else if (pickup.item != null)
            {
                // It goes to the Roundhouse. The machine is built there, not out here.
                var item = Profile.Keep(pickup.item);
                RunStats.Parts++;
                Loot.Part(item.def, item);
            }
            // It breaks up into sparks of its own colour that rise off the machine.
            var color = pickup.glow.color;
            Combat.Vfx.Sparks(rig.transform.position + Vector3.up, color, 16, 6f, 0.4f, 0.35f);
            Combat.Vfx.Rise(rig.transform.position, rig.Radius, 0f, color, 8, 3f, 0.14f, 0.5f);
        }
    }
}
