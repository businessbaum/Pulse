using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace Pulse
{
    // All enemies live in one array and are drawn with instancing. There is no GameObject per enemy.
    // How a species fights is set by its Behaviour; its mini boss and boss forms add to the same idea.
    public sealed class EnemySystem : MonoBehaviour
    {
        public struct Enemy
        {
            public Vector3 pos, aim;
            public float hp, maxHp, radius, scale, damage, speed;
            public EnemyDef def;
            public Rank rank;
            public StateType state;
            public float stateLeft, reactLock, stun, attackCd, flash, lunge;
            // timer: until its special attack is ready. phase: where in that attack it is.
            public float timer, phaseLeft, travelled;
            public int phase, id, kind;
            // Whether it has noticed the machine. Once true it stays true.
            public bool aggro;
            // It blew itself up, so nobody gets credit for the kill.
            public bool burst;
            // It came out of another enemy. It leaves nothing behind, or a brood would be a farm.
            public bool hatched;
            // Wardens. stage: which phase of its fight it is in. move: the attack it is making.
            // counter: how many parts of that attack are left.
            public int stage, move, counter;
            // lane: how far its charge will run. span: how long the wind-up it is in lasts in all.
            public float lane, span;
            // shielded: nothing can hurt it. scripted: a scene moves it, and it does not act by itself.
            public bool shielded, scripted;
            // brittle: seconds for which it still takes more damage, after a Thermal Shock cracked it.
            public float brittle;
            // On black ice: where it is sliding, whether it wants to or not.
            public Vector3 slide;
        }

        // What the HUD needs to draw a bar for a mini boss or a boss.
        public struct Elite
        {
            public int id;
            public string name, trait;
            public Rank rank;
            public float hull, distance;
            public Vector3 head;
            public bool aggro;
            public int stage;
        }

        struct PendingReaction
        {
            public ReactionType type;
            public Vector3 pos;
            // strength: 1 for a reaction as it is, less for the one an Echo adds. wait: until it goes off.
            public float strength, wait;
        }

        // A spot of ground that will erupt. Until then it flickers, so the machine can get off it.
        struct Burst
        {
            public Vector3 pos;
            public float radius, left, total, damage, burn, tick;
            public string source;
        }

        // One species with its draw lists: one per look, which is a state colour or the white hit flash.
        sealed class Kind
        {
            public EnemyDef def;
            public readonly Material[][] looks = new Material[Looks][];
            public readonly Matrix4x4[][] bodies = new Matrix4x4[Looks][], legs = new Matrix4x4[Looks][];
            public readonly int[] drawn = new int[Looks], legsDrawn = new int[Looks];
        }

        const int Max = 2048, Looks = 7, FlashLook = 6, HashSize = 4096, BatchSize = 1023, HatchCap = 150;
        const int Idle = 0, WindUp = 1, Acting = 2, Recover = 3;
        const float CellSize = 2f, ReactLock = 0.4f, PushStrength = 6f, FlashTime = 0.07f;
        const float StepSpeed = 11f, LegSwing = 34f, BodyBob = 0.05f;
        const float LungeTime = 0.24f, LungeReach = 0.55f, IdlePace = 0.35f, WanderSpeed = 0.7f;

        // Every wind-up time below is the base time. A species multiplies it by its own windUp, so the
        // species of the first biomes announce their attacks for longer.
        // Charger: runs ChargeSpeed times its pace for ChargeTime after showing the line for ChargeWindUp.
        const float ChargeRange = 10f, ChargeWindUp = 0.75f, ChargeTime = 0.85f, ChargeSpeed = 3.2f, ChargeRest = 2.6f, ChargeHit = 1.8f;
        // Spitter: stays between KeepNear and KeepFar and lobs a glob that burns where it lands.
        const float SpitRange = 15f, KeepNear = 7f, KeepFar = 11f, SpitFlight = 0.95f, SpitRadius = 1.7f, SpitBurn = 3.5f;
        // Skirmisher: circles at OrbitRadius and darts in.
        const float OrbitRadius = 7f, DartWindUp = 0.45f, DartSpeed = 2.2f, DartTime = 1.3f, FrostRadius = 2f, FrostTime = 5f;
        // Slammer: the ground it will hit flickers for SlamWindUp before the blow.
        const float SlamWindUp = 0.9f, SlamRest = 2.4f;
        // Lancer: the line shows for LanceWindUp, then a bolt flies along it.
        const float LanceRange = 14f, LanceWindUp = 0.8f, LanceSpeed = 24f, LanceReach = 26f, LanceFan = 12f;
        // Leech: its bite leaves the machine with BiteSlow of its speed.
        const float BiteSlow = 0.55f, BiteTime = 1.4f, PullTime = 1.5f, PullSpeed = 5.5f, TrailRadius = 1.5f;
        // Bloater: FuseTime between reaching the machine and bursting.
        const float FuseTime = 0.7f, PoolTime = 8f;
        // The Warden. Its attacks, the times each one shows before it lands, and their reach.
        const int MoveSlam = 1, MoveCharge = 2, MoveVolley = 3, MoveOrbs = 4, MoveErupt = 5;
        const float ReignSlam = 7.5f, ReignSlamWindUp = 1.3f, ReignChargeWindUp = 1.2f, ReignChargeSpeed = 21f, ReignChargeTime = 1.7f;
        const float ReignVolleyWindUp = 0.9f, ReignOrbWindUp = 1f, ReignEruptWindUp = 0.8f;
        const float EruptDelay = 1.5f, EruptStep = 3.2f, EruptRadius = 1.8f, EruptReach = 42f;
        // Black Ice: each neighbour adds BlackIceCrowd to the damage, up to BlackIceMost on top. SlideFade: how fast a slide ends.
        const float BlackIceCrowd = 0.25f, BlackIceMost = 2f, SlideFade = 3.5f;
        // Echoes. Second Beat: how strong the second reaction is and how long after the first it comes.
        // Afterimage: the lightning every tenth hit sets off. Ember: how far a burning death spreads.
        const float SecondStrength = 0.5f, SecondDelay = 0.4f, AfterimageDamage = 12f, AfterimageRange = 7f, EmberReach = 3.2f;
        const int AfterimageJumps = 3;

        static readonly Color AlertColor = Tokens.Alert;
        static readonly Color WreckColor = new Color(0.45f, 0.5f, 0.57f);

        readonly Enemy[] enemies = new Enemy[Max];
        readonly int[] cellHead = new int[HashSize];
        readonly int[] nextInCell = new int[Max];
        readonly List<Kind> kinds = new List<Kind>();
        readonly Dictionary<EnemyDef, int> kindOf = new Dictionary<EnemyDef, int>();
        readonly Matrix4x4[] buffer = new Matrix4x4[BatchSize];
        readonly List<PendingReaction> pending = new List<PendingReaction>(64);
        readonly List<int> hits = new List<int>(256);
        readonly List<Burst> bursts = new List<Burst>(64);
        // Reactions an Echo repeats, waiting for their moment; the spots lightning will start from; and
        // who a chain of lightning has already been through.
        readonly List<PendingReaction> later = new List<PendingReaction>(32);
        readonly List<Vector3> strikes = new List<Vector3>(16);
        readonly List<int> visited = new List<int>(16);
        bool chaining;
        Mesh cube;
        float auraOwed;
        int nextId = 1;

        public int Count { get; private set; }
        public ref Enemy this[int index] => ref enemies[index];
        // The mini bosses and bosses alive right now, refreshed every frame.
        public readonly List<Elite> Elites = new List<Elite>();
        // When a Warden last ran itself into a wall. The crowd in its arena reacts to it.
        public float LastCrash { get; private set; } = -99f;

        void Awake()
        {
            cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        }

        // Returns the index of the new enemy, or -1 when there is no room.
        public int Spawn(EnemyDef def, Vector3 pos, bool aggro = true, Rank rank = Rank.Normal)
        {
            if (Count >= Max || def == null) return -1;
            var stats = Ranks.Of(rank);
            float hull = Ranks.Hull(def, rank);
            pos.y = 0f;
            enemies[Count] = new Enemy
            {
                pos = pos, def = def, rank = rank, aggro = aggro, id = nextId++, kind = KindOf(def),
                hp = hull, maxHp = hull, scale = stats.size, radius = def.radius * stats.size,
                damage = def.contactDamage * stats.damage, speed = def.speed * stats.speed,
                // The first special attack never comes the instant it appears.
                timer = Random.Range(0.6f, 2f), aim = Vector3.forward, stage = 1,
            };
            return Count++;
        }

        int KindOf(EnemyDef def)
        {
            if (kindOf.TryGetValue(def, out int index)) return index;
            kinds.Add(new Kind { def = def });
            return kindOf[def] = kinds.Count - 1;
        }

        public void Clear()
        {
            Count = 0;
            pending.Clear();
            later.Clear();
            strikes.Clear();
            bursts.Clear();
            Elites.Clear();
        }

        // Removes idle enemies that are far away. Those that follow the machine are never removed.
        public void RemoveBeyond(Vector3 center, float distance)
        {
            float limitSq = distance * distance;
            for (int i = Count - 1; i >= 0; i--)
                if (!enemies[i].aggro && FlatSq(enemies[i].pos - center) > limitSq) enemies[i] = enemies[--Count];
        }

        // Where the Warden is in the list, or -1 when there is none.
        public int Warden()
        {
            for (int i = 0; i < Count; i++)
                if (enemies[i].rank == Rank.Warden && enemies[i].hp > 0f) return i;
            return -1;
        }

        // How many of a rank are alive.
        public int Ranked(Rank rank)
        {
            int found = 0;
            for (int i = 0; i < Count; i++)
                if (enemies[i].rank == rank && enemies[i].hp > 0f) found++;
            return found;
        }

        // ---- Damage and states ----

        public void Hit(int index, float damage, StateType state, bool ignoreArmor = false)
        {
            ref var enemy = ref enemies[index];
            if (enemy.hp <= 0f || enemy.shielded) return;
            if (damage > 0f)
            {
                // Cracked by a Thermal Shock: everything hurts it more.
                if (enemy.brittle > 0f) damage *= 1f + Combat.Config.brittleBonus;
                // Afterimage: every tenth hit starts lightning from where it landed.
                if (!chaining && Echoes.Tenth()) strikes.Add(enemy.pos);
                float dealt = Mathf.Min(enemy.hp, ignoreArmor ? damage : Mathf.Max(1f, damage - enemy.def.armor));
                enemy.hp -= dealt;
                RunStats.DamageDealt += dealt;
                // A Warden is being hit all the time. It only flickers now and then, or it would be white throughout.
                if (enemy.rank != Rank.Warden || enemy.flash < -0.5f) enemy.flash = FlashTime;
                enemy.aggro = true;
                Combat.Vfx.Sparks(enemy.pos + Vector3.up * (0.6f * enemy.scale), Tokens.Of(state), 2, 6f);
            }
            if (enemy.hp > 0f) ApplyState(index, state);
        }

        void ApplyState(int index, StateType state)
        {
            if (state == StateType.None) return;
            ref var enemy = ref enemies[index];
            var config = Combat.Config;

            if (enemy.state == StateType.None || enemy.state == state)
            {
                if (state == StateType.Shock && enemy.state != StateType.Shock) enemy.stun = config.shockStun;
                enemy.state = state;
                // Deep Cold: chill lasts twice as long.
                enemy.stateLeft = config.stateDuration * (state == StateType.Cryo && Echoes.Has(EchoKind.DeepCold) ? 2f : 1f);
                return;
            }

            if (enemy.reactLock > 0f) return;
            var reaction = Reactions.Of(enemy.state, state);
            enemy.state = StateType.None;
            enemy.reactLock = ReactLock;
            // Reactions run at the start of the next frame, so they never fire in the middle of another hit.
            pending.Add(new PendingReaction { type = reaction, pos = enemy.pos, strength = 1f });
        }

        public void Explode(Vector3 pos, float radius, float damage, StateType state, List<int> buffer, float inner = 0f)
        {
            InCircle(pos, radius, buffer, inner);
            foreach (int index in buffer) Hit(index, damage, state);
        }

        void RunReactions(float dt)
        {
            // What an Echo makes of a reaction comes a moment after it.
            for (int n = later.Count - 1; n >= 0; n--)
            {
                var reaction = later[n];
                reaction.wait -= dt;
                if (reaction.wait > 0f) { later[n] = reaction; continue; }
                later.RemoveAt(n);
                React(reaction);
            }

            int count = pending.Count;
            for (int n = 0; n < count; n++)
            {
                var reaction = pending[n];
                // Only reactions that exist yet are counted; the other pairs just cancel each other.
                if (!React(reaction)) continue;
                Combat.ReactionCount++;
                Codex.Saw(reaction.type);
                // Second Beat: the same again, half as strong.
                if (Echoes.Has(EchoKind.SecondBeat))
                    later.Add(new PendingReaction { type = reaction.type, pos = reaction.pos, strength = SecondStrength, wait = SecondDelay });
            }
            pending.RemoveRange(0, count);

            // Afterimage: lightning from the spots where every tenth hit landed.
            if (strikes.Count == 0) return;
            chaining = true;
            foreach (var spot in strikes) Chain(spot, AfterimageJumps, AfterimageDamage, AfterimageRange, StateType.Shock, Tokens.Shock);
            chaining = false;
            strikes.Clear();
        }

        // Makes one reaction happen; strength scales what it does. Returns false for a pair of states
        // whose reaction does not exist yet.
        bool React(in PendingReaction reaction)
        {
            var config = Combat.Config;
            Vector3 pos = reaction.pos;
            float strength = reaction.strength;
            bool first = strength >= 1f;
            switch (reaction.type)
            {
                case ReactionType.Firestorm:
                    if (first) RunStats.Firestorms++;
                    Combat.Zones.Spawn(StateType.Heat, pos, config.firestormRadius, config.firestormDuration, config.firestormDps * strength);
                    Combat.Marks.Scorch(pos, config.firestormRadius * 0.9f);
                    Combat.Vfx.Burst(pos + Vector3.up * 0.4f, Tokens.Heat, 18, 7f, 0.32f, 0.55f);
                    Combat.Vfx.Shockwave(pos, config.firestormRadius, Tokens.Heat);
                    return true;

                case ReactionType.Flash:
                    if (first) RunStats.Flashes++;
                    Explode(pos, config.flashRadius, config.flashDamage * strength, StateType.None, hits);
                    Combat.Vfx.Wave(pos, 0f, config.flashRadius, Tokens.Chalk, 30);
                    Combat.Vfx.Sparks(pos + Vector3.up * 0.4f, Tokens.Shock, 30, 16f, 0.8f, 0.3f);
                    Combat.Vfx.Shockwave(pos, config.flashRadius, Tokens.Shock, 36);
                    return true;

                case ReactionType.Plasma:
                    if (first) RunStats.Plasmas++;
                    InCircle(pos, config.plasmaRange, hits);
                    int arcs = Mathf.Min(config.plasmaArcs + 1, hits.Count);
                    for (int k = 0; k < arcs; k++)
                    {
                        Combat.Fx.Bolt(pos + Vector3.up * 0.6f, enemies[hits[k]].pos + Vector3.up * 0.6f, Tokens.PulseGreen, 0.22f);
                        Combat.Vfx.Sparks(enemies[hits[k]].pos + Vector3.up * 0.6f, Tokens.PulseGreen, 5);
                        Hit(hits[k], config.plasmaDamage * strength, StateType.None, ignoreArmor: true);
                    }
                    return true;

                case ReactionType.ThermalShock:
                    // The sudden change cracks whatever stands there: it is hurt, and for a while
                    // everything hurts it more.
                    if (first) RunStats.ThermalShocks++;
                    InCircle(pos, config.thermalShockRadius, hits);
                    foreach (int index in hits)
                    {
                        Hit(index, config.thermalShockDamage * strength, StateType.None, ignoreArmor: true);
                        if (enemies[index].hp > 0f) enemies[index].brittle = config.brittleTime;
                    }
                    Combat.Vfx.Burst(pos + Vector3.up * 0.6f, Tokens.Chalk, 14, 8f, 0.2f, 0.5f, falls: true);
                    Combat.Vfx.Sparks(pos + Vector3.up * 0.6f, Tokens.Cryo, 12, 11f, 0.5f, 0.25f);
                    Combat.Vfx.Sparks(pos + Vector3.up * 0.6f, Tokens.Heat, 12, 11f, 0.5f, 0.25f);
                    Combat.Vfx.Shockwave(pos, config.thermalShockRadius, Tokens.Chalk, 20, 0.22f);
                    return true;

                case ReactionType.Superconductor:
                    // Lightning that loses nothing on its way from one enemy to the next.
                    if (first) RunStats.Superconductors++;
                    Chain(pos, config.superconductorJumps, config.superconductorDamage * strength, config.superconductorRange, StateType.None, Tokens.Cryo);
                    Combat.Vfx.Sparks(pos + Vector3.up * 0.6f, Tokens.Shock, 16, 12f, 0.6f, 0.25f);
                    return true;

                case ReactionType.BlackIce:
                    // Oil over ice: nothing there keeps its footing. They slide apart, and the closer
                    // they stood together, the harder they ran into each other.
                    if (first) RunStats.BlackIces++;
                    InCircle(pos, config.blackIceRadius, hits);
                    float crowd = 1f + Mathf.Min(BlackIceCrowd * (hits.Count - 1), BlackIceMost);
                    foreach (int index in hits)
                    {
                        Vector3 away = enemies[index].pos - pos;
                        away.y = 0f;
                        // The one in the middle has nowhere to slide to. A Warden is too heavy to lose its footing.
                        away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.zero;
                        if (enemies[index].rank != Rank.Warden)
                        {
                            enemies[index].slide = away * config.blackIceSlide;
                            enemies[index].stun = Mathf.Max(enemies[index].stun, config.blackIceStun);
                        }
                        Hit(index, config.blackIceDamage * crowd * strength, StateType.None);
                    }
                    Combat.Marks.Scorch(pos, config.blackIceRadius * 0.9f, Tokens.Cryo);
                    Combat.Vfx.Ground(pos, config.blackIceRadius, 0f, Color.Lerp(Tokens.Cryo, Tokens.Oil, 0.5f) * 0.6f, 26, 0.7f, 1.6f);
                    Combat.Vfx.Wave(pos, 0f, config.blackIceRadius, Tokens.Cryo, 30);
                    Combat.Vfx.Burst(pos + Vector3.up * 0.3f, Tokens.Oil, 10, 6f, 0.2f, 0.5f, falls: true);
                    return true;
            }
            return false;
        }

        // Lightning that starts at a spot and jumps from one enemy to the nearest it has not been
        // through yet. Every jump deals the same damage.
        void Chain(Vector3 from, int jumps, float damage, float range, StateType state, Color color)
        {
            visited.Clear();
            Vector3 at = from;
            for (int j = 0; j < jumps; j++)
            {
                int next = -1;
                float bestSq = range * range;
                for (int i = 0; i < Count; i++)
                {
                    if (enemies[i].hp <= 0f || visited.Contains(enemies[i].id)) continue;
                    float sq = FlatSq(enemies[i].pos - at);
                    if (sq >= bestSq) continue;
                    bestSq = sq;
                    next = i;
                }
                if (next < 0) return;
                visited.Add(enemies[next].id);
                Vector3 to = enemies[next].pos;
                Combat.Fx.Bolt(at + Vector3.up * 0.7f, to + Vector3.up * 0.7f, color, 0.2f);
                Combat.Vfx.Sparks(to + Vector3.up * 0.6f, color, 5);
                Hit(next, damage, state, ignoreArmor: true);
                at = to;
            }
        }

        // ---- Queries ----

        // sight: only enemies that no dungeon wall hides from that point.
        public int Nearest(Vector3 pos, float range, bool sight = false)
        {
            int best = -1;
            float bestSq = range * range;
            for (int i = 0; i < Count; i++)
            {
                if (enemies[i].hp <= 0f) continue;
                float sq = FlatSq(enemies[i].pos - pos);
                if (sq >= bestSq || (sight && Combat.Scenery.Hidden(pos, enemies[i].pos))) continue;
                bestSq = sq;
                best = i;
            }
            return best;
        }

        // skipA, skipB: the ids of enemies that do not count, for a shot that has been through them.
        public int Touching(Vector3 pos, float radius, int skipA = 0, int skipB = 0)
        {
            for (int i = 0; i < Count; i++)
            {
                if (enemies[i].hp <= 0f || enemies[i].id == skipA || enemies[i].id == skipB) continue;
                float reach = radius + enemies[i].radius;
                if (FlatSq(enemies[i].pos - pos) < reach * reach) return i;
            }
            return -1;
        }

        public void InCircle(Vector3 pos, float radius, List<int> into, float inner = 0f)
        {
            into.Clear();
            float outerSq = radius * radius, innerSq = inner * inner;
            for (int i = 0; i < Count; i++)
            {
                if (enemies[i].hp <= 0f) continue;
                float sq = FlatSq(enemies[i].pos - pos);
                if (sq <= outerSq && sq >= innerSq) into.Add(i);
            }
        }

        public void InCone(Vector3 pos, Vector3 dir, float range, float halfAngle, List<int> into)
        {
            into.Clear();
            float rangeSq = range * range, minDot = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
            for (int i = 0; i < Count; i++)
            {
                if (enemies[i].hp <= 0f) continue;
                Vector3 to = enemies[i].pos - pos;
                to.y = 0f;
                float sq = to.sqrMagnitude;
                if (sq > rangeSq) continue;
                if (sq < 0.01f || Vector3.Dot(to / Mathf.Sqrt(sq), dir) >= minDot) into.Add(i);
            }
        }

        static float FlatSq(Vector3 v) => v.x * v.x + v.z * v.z;

        // ---- Per frame ----

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                RunReactions(dt);
                BuildGrid();
                Move(dt);
                RunBursts(dt);
                RemoveDead();
            }
            Draw();
        }

        void Move(float dt)
        {
            var config = Combat.Config;
            var rig = Combat.Rig;
            Vector3 target = rig.transform.position;
            target.y = 0f;
            bool openWorld = Combat.Director != null && Combat.Director.Mode == RunMode.OpenWorld;
            float aggroRange = openWorld ? config.world.aggroRange : float.MaxValue;
            float edge = openWorld ? config.world.halfSize : float.MaxValue;
            // Mini bosses and bosses give off a steady crown of motes; a boss twice as many.
            auraOwed += dt * 16f;
            int aura = (int)auraOwed;
            auraOwed -= aura;

            for (int i = 0; i < Count; i++)
            {
                ref var enemy = ref enemies[i];
                if (enemy.hp <= 0f) continue;
                if (aura > 0 && enemy.rank >= Rank.MiniBoss)
                    Combat.Vfx.Rise(enemy.pos, enemy.radius * 1.3f, enemy.radius * 0.9f, AlertColor,
                        aura * (enemy.rank == Rank.Boss ? 2 : 1), 2.4f, 0.13f, 0.6f);
                // A Warden in its second phase burns: embers and smoke pour off it.
                if (aura > 0 && enemy.rank == Rank.Warden && enemy.stage >= 2)
                    Combat.Vfx.Rise(enemy.pos + Vector3.up * (enemy.def.height * 0.5f), enemy.radius, 0f, Tokens.Heat, aura * 2, 3.4f, 0.24f, 0.8f, smoky: true);

                enemy.reactLock -= dt;
                enemy.attackCd -= dt;
                enemy.flash -= dt;
                enemy.lunge -= dt;
                enemy.timer -= dt;
                enemy.brittle -= dt;
                // What a Thermal Shock cracked keeps shedding splinters.
                if (aura > 0 && enemy.brittle > 0f)
                    Combat.Vfx.Rise(enemy.pos + Vector3.up * (0.5f * enemy.scale), enemy.radius, 0f, Tokens.Chalk, 1, 1.6f, 0.1f, 0.4f);
                if (enemy.state != StateType.None)
                {
                    enemy.stateLeft -= dt;
                    if (enemy.state == StateType.Heat)
                    {
                        float burn = Mathf.Min(enemy.hp, config.burnDps * dt);
                        enemy.hp -= burn;
                        RunStats.DamageDealt += burn;
                    }
                    if (enemy.stateLeft <= 0f) enemy.state = StateType.None;
                }
                if (enemy.scripted) continue;
                if (enemy.stun > 0f)
                {
                    enemy.stun -= dt;
                    // On black ice it still moves, only not of its own will.
                    if (enemy.slide.sqrMagnitude > 0.04f)
                    {
                        Vector3 slid = Combat.Scenery.Resolve(enemy.pos + enemy.slide * dt, enemy.radius);
                        slid.x = Mathf.Clamp(slid.x, -edge, edge);
                        slid.z = Mathf.Clamp(slid.z, -edge, edge);
                        enemy.pos = slid;
                        enemy.slide = Vector3.Lerp(enemy.slide, Vector3.zero, 1f - Mathf.Exp(-SlideFade * dt));
                    }
                    continue;
                }
                enemy.slide = Vector3.zero;

                Vector3 to = target - enemy.pos;
                to.y = 0f;
                float dist = to.magnitude;
                Vector3 dir = dist > 0.001f ? to / dist : Vector3.forward;
                // In a dungeon the way to the machine may lead around a wall.
                dir = Combat.Scenery.Towards(enemy.pos, target, dir);
                Vector3 velocity = Separation(i);
                if (!enemy.aggro)
                {
                    // Idle enemies drift about until the machine comes close. Once they have noticed it,
                    // they follow it for good.
                    if (dist < aggroRange && rig.Alive)
                    {
                        enemy.aggro = true;
                        Combat.Vfx.Sparks(enemy.pos + Vector3.up * (enemy.def.height * enemy.scale + 0.5f), AlertColor, 8, 4f, 0.35f, 0.3f);
                    }
                    else velocity += WanderDirection(i) * WanderSpeed;
                }
                else velocity += Act(ref enemy, dir, dist, dt, rig);

                Vector3 pos = Combat.Scenery.Resolve(enemy.pos + velocity * dt, enemy.radius);
                pos.x = Mathf.Clamp(pos.x, -edge, edge);
                pos.z = Mathf.Clamp(pos.z, -edge, edge);
                enemy.pos = pos;
            }
        }

        // What an enemy that has noticed the machine does this frame. Returns where it wants to move.
        Vector3 Act(ref Enemy e, Vector3 dir, float dist, float dt, Rig rig)
        {
            // Oil clings and cold stiffens: both slow it down.
            var config = Combat.Config;
            float pace = e.speed * (e.state == StateType.Oil ? config.oilSlow
                : e.state == StateType.Cryo ? (Echoes.Has(EchoKind.DeepCold) ? config.deepChillSlow : config.chillSlow) : 1f);
            float reach = rig.Radius + e.radius;
            bool elite = e.rank >= Rank.MiniBoss, boss = e.rank == Rank.Boss;

            switch (e.def.behaviour)
            {
                case Behaviour.Swarm:
                    // Mini boss and boss: keep hatching more of their kind.
                    if (elite && e.timer <= 0f && Count < HatchCap)
                    {
                        e.timer = boss ? 3f : 4.5f;
                        Hatch(e.def, e.pos, boss ? 3 : 2, Rank.Normal, e.radius);
                    }
                    return Chase(ref e, dir, dist, pace, reach, rig);

                case Behaviour.Charger: return Charge(ref e, dir, dist, dt, pace, reach, rig);
                case Behaviour.Spitter: return Spit(ref e, dir, dist, pace, rig);
                case Behaviour.Skirmisher: return Skirmish(ref e, dir, dist, dt, pace, reach, rig);
                case Behaviour.Slammer: return Slam(ref e, dir, dist, dt, pace, reach, rig);
                case Behaviour.Lancer: return Lance(ref e, dir, dist, dt, pace, rig);
                case Behaviour.Leech: return Bite(ref e, dir, dist, dt, pace, reach, rig);
                case Behaviour.Bloater: return Bloat(ref e, dir, dist, dt, pace, reach, rig);
                case Behaviour.Warden: return Reign(ref e, dir, dist, dt, pace, reach, rig);
                default: return Chase(ref e, dir, dist, pace, reach, rig);
            }
        }

        // Walks at the machine and hits it once a second while touching it.
        Vector3 Chase(ref Enemy e, Vector3 dir, float dist, float pace, float reach, Rig rig)
        {
            if (dist > reach) return dir * pace;
            if (rig.Exposed && e.attackCd <= 0f) Strike(ref e, rig);
            return Vector3.zero;
        }

        void Strike(ref Enemy e, Rig rig, float scale = 1f)
        {
            e.attackCd = 1f;
            e.lunge = LungeTime;
            rig.TakeHit(e.damage * scale, e.pos, Ranks.Name(e.def, e.rank));
        }

        // Charger. Shows a line, then rams along it. Mini boss: the ram ends in a shockwave.
        // Boss: it also leaves a burning trail.
        Vector3 Charge(ref Enemy e, Vector3 dir, float dist, float dt, float pace, float reach, Rig rig)
        {
            bool elite = e.rank >= Rank.MiniBoss, boss = e.rank == Rank.Boss;
            switch (e.phase)
            {
                case WindUp:
                    e.phaseLeft -= dt;
                    if (e.phaseLeft <= 0f) { e.phase = Acting; e.phaseLeft = ChargeTime; e.travelled = 0f; }
                    return Vector3.zero;

                case Acting:
                    e.phaseLeft -= dt;
                    float step = pace * ChargeSpeed;
                    e.travelled += step * dt;
                    if (boss && e.travelled >= 1.8f)
                    {
                        e.travelled = 0f;
                        Combat.Zones.Spawn(StateType.Heat, e.pos, 1.4f, 4f, e.damage * 0.5f, hostile: true);
                    }
                    bool hit = rig.Exposed && dist < reach + 0.2f;
                    if (hit)
                    {
                        rig.TakeHit(e.damage * ChargeHit, e.pos, Ranks.Name(e.def, e.rank));
                        rig.Shove(e.aim * 7f);
                    }
                    if (hit || e.phaseLeft <= 0f)
                    {
                        e.phase = Idle;
                        e.timer = ChargeRest;
                        e.attackCd = 0.8f;
                        if (elite) Quake(ref e, boss ? 6.5f : 4.5f, e.damage, rig, StateType.None);
                    }
                    return e.aim * step;

                default:
                    if (e.timer <= 0f && rig.Exposed && dist < ChargeRange && dist > reach + 1f)
                    {
                        e.phase = WindUp;
                        e.phaseLeft = ChargeWindUp * e.def.windUp;
                        e.aim = dir;
                        Vector3 from = e.pos + Vector3.up * 0.15f;
                        Combat.Fx.Line(from, from + dir * (pace * ChargeSpeed * ChargeTime), AlertColor, e.phaseLeft, 0.5f * e.scale, hold: true);
                        return Vector3.zero;
                    }
                    return Chase(ref e, dir, dist, pace, reach, rig);
            }
        }

        // Spitter. Keeps its distance and lobs a glob that burns where it lands.
        // Mini boss: three globs side by side. Boss: five, and more often.
        Vector3 Spit(ref Enemy e, Vector3 dir, float dist, float pace, Rig rig)
        {
            bool elite = e.rank >= Rank.MiniBoss, boss = e.rank == Rank.Boss;
            if (e.timer <= 0f && rig.Exposed && dist < SpitRange)
            {
                e.timer = boss ? 2f : 2.8f;
                e.lunge = LungeTime;
                // Aimed a little ahead of the machine, so standing still or driving straight both get hit.
                // The ground where it will land flickers for the whole flight, and the flight is the warning.
                float flight = SpitFlight * e.def.windUp;
                Vector3 at = rig.transform.position + rig.Velocity * (flight * 0.5f);
                Vector3 side = Vector3.Cross(Vector3.up, dir) * 3.4f;
                LobFire(e, at, flight);
                if (elite) { LobFire(e, at + side, flight); LobFire(e, at - side, flight); }
                if (boss) { LobFire(e, at + dir * 3.4f, flight); LobFire(e, at - dir * 3.4f, flight); }
            }
            if (dist > KeepFar) return dir * pace;
            if (dist < KeepNear) return dir * (-pace * 0.8f);
            return Vector3.Cross(Vector3.up, dir) * (pace * 0.5f * Side(e.id));
        }

        static void LobFire(in Enemy e, Vector3 at, float flight) =>
            Combat.Projectiles.Lob(e.pos + Vector3.up * (e.def.height * e.scale), at, flight, StateType.Heat, SpitRadius, SpitBurn,
                e.damage, ring: false, hostile: true, impact: e.damage, source: Ranks.Name(e.def, e.rank));

        // Skirmisher. Circles the machine, darts in for a hit and backs off.
        // Mini boss: every dart leaves frost behind. Boss: it darts twice as often and its hit chills.
        Vector3 Skirmish(ref Enemy e, Vector3 dir, float dist, float dt, float pace, float reach, Rig rig)
        {
            bool elite = e.rank >= Rank.MiniBoss, boss = e.rank == Rank.Boss;
            switch (e.phase)
            {
                case WindUp:
                    // It crouches with a line pointing at the machine; then it comes.
                    e.phaseLeft -= dt;
                    if (e.phaseLeft > 0f) return Vector3.zero;
                    e.phase = Acting;
                    e.phaseLeft = DartTime;
                    if (elite) Frost(e.pos, e.scale);
                    return Vector3.zero;

                case Acting:
                    e.phaseLeft -= dt;
                    if (dist <= reach)
                    {
                        if (rig.Exposed)
                        {
                            Strike(ref e, rig);
                            if (boss) rig.Slow(0.55f, 1.8f, StateType.Cryo);
                        }
                        if (elite) Frost(e.pos, e.scale);
                        e.phase = Recover;
                        e.phaseLeft = 0.7f;
                        return Vector3.zero;
                    }
                    if (e.phaseLeft <= 0f) { e.phase = Recover; e.phaseLeft = 0.5f; }
                    return dir * (pace * DartSpeed);

                case Recover:
                    e.phaseLeft -= dt;
                    if (e.phaseLeft <= 0f) { e.phase = Idle; e.timer = boss ? 1.3f : 2.6f; }
                    return dir * (-pace * 1.2f);

                default:
                    if (e.timer <= 0f && rig.Exposed && dist < OrbitRadius + 3f)
                    {
                        e.phase = WindUp;
                        e.phaseLeft = DartWindUp * e.def.windUp;
                        Vector3 from = e.pos + Vector3.up * 0.15f;
                        Combat.Fx.Line(from, from + dir * dist, AlertColor, e.phaseLeft, 0.22f * e.scale, hold: true);
                        return Vector3.zero;
                    }
                    float inward = Mathf.Clamp((dist - OrbitRadius) * 0.8f, -1f, 1f);
                    return (dir * inward + Vector3.Cross(Vector3.up, dir) * Side(e.id)).normalized * pace;
            }
        }

        static void Frost(Vector3 pos, float scale) =>
            Combat.Zones.Spawn(StateType.Cryo, pos, FrostRadius * Mathf.Sqrt(scale), FrostTime, 0f, hostile: true);

        // Slammer. Walks up, the ground around it flickers, and then it pounds that ground.
        // Mini boss: a wider reach, and it slows. Boss: wider still, and frost breaks out around it.
        Vector3 Slam(ref Enemy e, Vector3 dir, float dist, float dt, float pace, float reach, Rig rig)
        {
            bool elite = e.rank >= Rank.MiniBoss, boss = e.rank == Rank.Boss;
            float radius = boss ? 7.5f : elite ? 5.6f : 3.8f;
            if (e.phase == WindUp)
            {
                e.phaseLeft -= dt;
                if (e.phaseLeft > 0f)
                {
                    Warn(ref e, radius, SlamWindUp * e.def.windUp, dt);
                    return Vector3.zero;
                }
                e.phase = Idle;
                e.timer = SlamRest;
                Quake(ref e, radius, e.damage, rig, elite ? StateType.Cryo : StateType.None);
                if (boss)
                    for (int k = 0; k < 6; k++)
                    {
                        float angle = k * Mathf.PI / 3f;
                        Frost(e.pos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (radius * 0.85f), 1f);
                    }
                return Vector3.zero;
            }
            if (e.timer <= 0f && rig.Exposed && dist < radius - 0.8f)
            {
                e.phase = WindUp;
                e.phaseLeft = SlamWindUp * e.def.windUp;
                e.travelled = 0f;
                return Vector3.zero;
            }
            return dist > reach ? dir * pace : Vector3.zero;
        }

        // Shows where a blow is about to land: a circle as wide as its reach, with a second one that
        // grows inside it and meets it at the moment the blow lands. Dust is drawn in from its edge.
        // How far a stomp reaches is something to read at a glance, so it is drawn and not hinted at.
        void Warn(ref Enemy e, float radius, float total, float dt)
        {
            float coming = 1f - Mathf.Clamp01(e.phaseLeft / total);
            Combat.Marks.Ring(e.pos, radius, coming, AlertColor);
            e.travelled += dt;
            if (e.travelled < 0.05f) return;
            e.travelled = 0f;
            Combat.Vfx.Gather(e.pos, radius, AlertColor, 3 + (int)(coming * 6f), 0.3f);
        }

        // A blow to the ground: everything of the machine within reach is hit, and the floor cracks.
        void Quake(ref Enemy e, float radius, float damage, Rig rig, StateType chill)
        {
            Combat.Marks.Crack(e.pos, radius * 0.92f);
            Combat.Vfx.Shockwave(e.pos, radius, AlertColor, 40);
            Combat.Vfx.Burst(e.pos + Vector3.up * 0.3f, WreckColor, 14, 7f, 0.3f, 0.6f, falls: true);
            if (Combat.Camera != null) Combat.Camera.Shake(0.3f);
            float reach = radius + rig.Radius * 0.5f;
            if (!rig.Exposed || FlatSq(rig.transform.position - e.pos) > reach * reach) return;
            rig.TakeHit(damage, e.pos, Ranks.Name(e.def, e.rank));
            if (chill != StateType.None) rig.Slow(0.55f, 1.6f, chill);
        }

        // Lancer. Stands off, shows a line and shoots along it.
        // Mini boss: a fan of three. Boss: a fan of five, twice in a row.
        Vector3 Lance(ref Enemy e, Vector3 dir, float dist, float dt, float pace, Rig rig)
        {
            bool boss = e.rank == Rank.Boss;
            switch (e.phase)
            {
                case WindUp:
                    e.phaseLeft -= dt;
                    if (e.phaseLeft > 0f) return Vector3.zero;
                    Volley(e);
                    if (boss) { e.phase = Recover; e.phaseLeft = 0.35f; }
                    else { e.phase = Idle; e.timer = 3.2f; }
                    return Vector3.zero;

                case Recover:
                    e.phaseLeft -= dt;
                    if (e.phaseLeft > 0f) return Vector3.zero;
                    Volley(e);
                    e.phase = Idle;
                    e.timer = 2.8f;
                    return Vector3.zero;

                default:
                    if (e.timer <= 0f && rig.Exposed && dist < LanceRange)
                    {
                        // It aims where the machine will be if it keeps going as it is.
                        float shown = LanceWindUp * e.def.windUp;
                        Vector3 ahead = rig.transform.position + rig.Velocity * ((shown + dist / LanceSpeed) * 0.9f) - e.pos;
                        ahead.y = 0f;
                        e.aim = ahead.sqrMagnitude > 0.01f ? ahead.normalized : dir;
                        e.phase = WindUp;
                        e.phaseLeft = shown;
                        Vector3 from = e.pos + Vector3.up * 0.15f;
                        for (int k = -Fan(e.rank); k <= Fan(e.rank); k++)
                            Combat.Fx.Line(from, from + Quaternion.Euler(0f, k * LanceFan, 0f) * e.aim * LanceReach, Tokens.Cryo, shown, 0.12f, hold: true);
                        return Vector3.zero;
                    }
                    if (dist > LanceRange - 2f) return dir * pace;
                    return dist < 6f ? dir * (-pace * 0.7f) : Vector3.zero;
            }
        }

        static int Fan(Rank rank) => rank == Rank.Boss ? 2 : rank == Rank.MiniBoss ? 1 : 0;

        static void Volley(in Enemy e)
        {
            Vector3 muzzle = e.pos + e.aim * (e.radius + 0.4f);
            for (int k = -Fan(e.rank); k <= Fan(e.rank); k++)
                Combat.Projectiles.Fire(muzzle, Quaternion.Euler(0f, k * LanceFan, 0f) * e.aim, LanceSpeed, LanceReach, e.damage,
                    StateType.Cryo, hostile: true, source: Ranks.Name(e.def, e.rank));
            Combat.Vfx.Sparks(muzzle + Vector3.up * 0.8f, Tokens.Cryo, 8, 8f);
        }

        // Leech. Fast; its bite slows the machine. Mini boss: it leaves oil where it goes.
        // Boss: every few seconds it also drags the machine toward itself.
        Vector3 Bite(ref Enemy e, Vector3 dir, float dist, float dt, float pace, float reach, Rig rig)
        {
            bool elite = e.rank >= Rank.MiniBoss, boss = e.rank == Rank.Boss;
            if (elite)
            {
                e.travelled += pace * dt;
                if (e.travelled > 2.6f)
                {
                    e.travelled = 0f;
                    Combat.Zones.Spawn(StateType.Oil, e.pos, TrailRadius, 6f, 0f, hostile: true);
                }
            }
            if (boss && e.phase == Acting)
            {
                e.phaseLeft -= dt;
                rig.Shove(dir * -PullSpeed);
                if (Time.frameCount % 4 == 0)
                    Combat.Fx.Bolt(e.pos + Vector3.up * e.scale, rig.transform.position + Vector3.up * 0.8f, Tokens.Oil, 0.12f, 0.3f);
                if (e.phaseLeft <= 0f || !rig.Exposed) { e.phase = Idle; e.timer = 6f; }
            }
            else if (boss && e.timer <= 0f && rig.Exposed && dist < 13f && dist > reach + 2f)
            {
                e.phase = Acting;
                e.phaseLeft = PullTime;
            }

            if (dist > reach) return dir * pace;
            if (rig.Exposed && e.attackCd <= 0f)
            {
                Strike(ref e, rig);
                rig.Slow(BiteSlow, BiteTime, StateType.Oil);
            }
            return Vector3.zero;
        }

        // Bloater. Walks up, swells and bursts; the burst is in OnDeath, so shooting it sets it off too.
        // The boss does not blow itself up: it hits, and throws oil around the machine.
        Vector3 Bloat(ref Enemy e, Vector3 dir, float dist, float dt, float pace, float reach, Rig rig)
        {
            if (e.rank == Rank.Boss)
            {
                if (e.timer <= 0f && rig.Exposed && dist < 14f)
                {
                    e.timer = 4.5f;
                    for (int k = 0; k < 3; k++)
                    {
                        float angle = (k + Random.value) * Mathf.PI * 2f / 3f;
                        Vector3 at = rig.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 3f;
                        Combat.Projectiles.Lob(e.pos + Vector3.up * (e.def.height * e.scale), at, 1.1f, StateType.Oil, 2.4f, 7f, 0f,
                            ring: false, hostile: true);
                    }
                }
                return Chase(ref e, dir, dist, pace, reach, rig);
            }

            if (e.phase == WindUp)
            {
                e.phaseLeft -= dt;
                Warn(ref e, BurstRadius(e.rank), FuseTime * e.def.windUp, dt);
                if (e.phaseLeft <= 0f) { e.burst = true; e.hp = 0f; }
                return Vector3.zero;
            }
            if (dist <= reach + 0.4f && rig.Exposed)
            {
                e.phase = WindUp;
                e.travelled = 0f;
                e.phaseLeft = FuseTime * e.def.windUp;
                return Vector3.zero;
            }
            return dir * pace;
        }

        // ---- The Warden ----
        //
        // It walks after the machine and, each time it has caught its breath, makes one attack. Every
        // attack shows before it lands, as with every other enemy.
        //   Slam     the ground around it flickers, then it pounds it and leaves fire in a circle
        //   Charge   a line, then it rams along it; running into a wall leaves it dazed
        //   Volley   it rears, then lobs fire at and around the machine
        // The second phase is faster and adds:
        //   Orbs     balls of fire that turn after the machine until they burn out or hit a wall
        //   Erupt    two lines of ground through the machine's position flicker, then burst
        // and a missed charge is followed at once by a second.
        Vector3 Reign(ref Enemy e, Vector3 dir, float dist, float dt, float pace, float reach, Rig rig)
        {
            bool second = e.stage >= 2;
            float haste = second ? 0.8f : 1f;
            switch (e.phase)
            {
                case WindUp:
                    e.phaseLeft -= dt;
                    if (e.move == MoveSlam) Warn(ref e, ReignSlam, e.span, dt);
                    else Brace(ref e, dt);
                    return e.phaseLeft > 0f ? Vector3.zero : Unleash(ref e, rig, second);

                case Acting:
                    return e.move == MoveCharge ? Rampage(ref e, dist, dt, reach, rig, second) : Barrage(ref e, dir, dt);

                case Recover:
                    e.phaseLeft -= dt;
                    if (e.phaseLeft <= 0f) { e.phase = Idle; e.timer = second ? 0.9f : 1.6f; }
                    return Vector3.zero;
            }

            if (e.timer <= 0f && rig.Exposed)
            {
                Choose(ref e, dir, dist, haste, second);
                return Vector3.zero;
            }
            if (dist > reach + 1f) return dir * (pace * (second ? 1.3f : 1f));
            if (rig.Exposed && e.attackCd <= 0f) Strike(ref e, rig);
            return Vector3.zero;
        }

        // Picks the next attack: one that makes sense at this distance, and not the same one twice.
        void Choose(ref Enemy e, Vector3 dir, float dist, float haste, bool second)
        {
            int pick = MoveVolley;
            for (int tries = 0; tries < 5; tries++)
            {
                float roll = Random.value;
                if (dist < ReignSlam - 1.5f && roll < 0.45f) pick = MoveSlam;
                else if (dist > 9f && roll < 0.5f) pick = MoveCharge;
                else if (second && roll < 0.8f) pick = Random.value < 0.5f ? MoveOrbs : MoveErupt;
                else pick = MoveVolley;
                if (pick != e.move) break;
            }
            e.move = pick;
            e.phase = WindUp;
            e.travelled = 0f;
            e.counter = 0;
            switch (pick)
            {
                case MoveSlam: e.phaseLeft = ReignSlamWindUp * haste; break;
                case MoveCharge: Aim(ref e, dir, ReignChargeWindUp * haste); break;
                case MoveVolley: e.phaseLeft = ReignVolleyWindUp * haste; break;
                case MoveOrbs: e.phaseLeft = ReignOrbWindUp * haste; break;
                default: e.phaseLeft = ReignEruptWindUp * haste; break;
            }
            e.span = e.phaseLeft;
        }

        // Fixes where a charge will go, as far as the next wall. The lane it will run down is shown by Brace.
        void Aim(ref Enemy e, Vector3 dir, float time)
        {
            e.aim = dir;
            e.phaseLeft = e.span = time;
            e.travelled = 0f;
            float length = 2f;
            while (length < 46f && !Combat.Scenery.Blocks(e.pos + dir * (length + 1f))) length += 1f;
            e.lane = length;
        }

        // What shows while the Warden winds up anything but its slam. Before a charge the lane it
        // will run down is drawn, and fills from its feet to the far end as the moment comes.
        // Before it throws fire, fire is drawn into it.
        void Brace(ref Enemy e, float dt)
        {
            float coming = 1f - Mathf.Clamp01(e.phaseLeft / Mathf.Max(0.01f, e.span));
            if (e.move == MoveCharge) Combat.Marks.Lane(e.pos, e.aim, e.lane, e.radius * 0.9f, coming, AlertColor);
            e.travelled += dt;
            if (e.travelled < 0.05f) return;
            e.travelled = 0f;
            if (e.move != MoveCharge)
            {
                Combat.Vfx.Gather(e.pos + Vector3.up * (e.def.height * 0.5f), e.radius * 2.6f, Tokens.Heat, 4 + (int)(coming * 8f), 0.3f);
                return;
            }
            Combat.Vfx.Gather(e.pos, e.radius * 2.2f, AlertColor, 3 + (int)(coming * 6f), 0.3f);
        }

        // The wind-up is over: the attack happens.
        Vector3 Unleash(ref Enemy e, Rig rig, bool second)
        {
            string name = e.def.displayName;
            switch (e.move)
            {
                case MoveSlam:
                    Quake(ref e, ReignSlam, e.damage * 1.7f, rig, StateType.None);
                    int fires = second ? 9 : 6;
                    for (int k = 0; k < fires; k++)
                    {
                        float angle = k * Mathf.PI * 2f / fires;
                        Combat.Zones.Spawn(StateType.Heat, e.pos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ReignSlam * 0.7f),
                            1.7f, 4.5f, 7f, hostile: true);
                    }
                    Rest(ref e, 0.9f);
                    break;

                case MoveCharge:
                    e.phase = Acting;
                    e.phaseLeft = ReignChargeTime;
                    e.travelled = 0f;
                    break;

                case MoveVolley:
                    int globs = second ? 8 : 5;
                    Vector3 mouth = e.pos + Vector3.up * (e.def.height * 0.8f);
                    for (int k = 0; k < globs; k++)
                    {
                        // The first goes where the machine is heading; the rest fall around that.
                        float angle = Random.value * Mathf.PI * 2f, spread = k == 0 ? 0f : Random.Range(2.5f, 7.5f);
                        Vector3 at = rig.transform.position + rig.Velocity * 0.8f + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spread;
                        Combat.Projectiles.Lob(mouth, at, 1.5f + k * 0.06f, StateType.Heat, 1.9f, 4f, 7f, ring: false, hostile: true,
                            impact: e.damage, source: name);
                    }
                    e.lunge = LungeTime;
                    Rest(ref e, 0.9f);
                    break;

                case MoveOrbs:
                    e.phase = Acting;
                    e.phaseLeft = 0f;
                    e.counter = 4;
                    break;

                default:
                    // A cross of ground through where the machine is heading. It bursts from the middle outward.
                    Vector3 middle = rig.transform.position + rig.Velocity * 0.5f;
                    for (float along = -EruptReach; along <= EruptReach; along += EruptStep)
                    {
                        float delay = EruptDelay + Mathf.Abs(along) * 0.012f;
                        Erupt(middle + Vector3.right * along, EruptRadius, delay, e.damage * 1.4f, 3f, name);
                        if (Mathf.Abs(along) > 0.1f) Erupt(middle + Vector3.forward * along, EruptRadius, delay, e.damage * 1.4f, 3f, name);
                    }
                    e.lunge = LungeTime;
                    Rest(ref e, 1.4f);
                    break;
            }
            return Vector3.zero;
        }

        static void Rest(ref Enemy e, float seconds)
        {
            e.phase = Recover;
            e.phaseLeft = seconds;
        }

        // The charge. Fire is left along the way. Hitting the machine ends it; so does a wall, which
        // leaves the Warden dazed for a moment. That is what dodging a charge is rewarded with.
        Vector3 Rampage(ref Enemy e, float dist, float dt, float reach, Rig rig, bool second)
        {
            e.phaseLeft -= dt;
            float step = ReignChargeSpeed * (second ? 1.15f : 1f);
            e.travelled += step * dt;
            if (e.travelled >= 2.6f)
            {
                e.travelled = 0f;
                Combat.Zones.Spawn(StateType.Heat, e.pos, 1.6f, 4f, 7f, hostile: true);
            }
            bool hit = rig.Exposed && dist < reach + 0.3f;
            if (hit)
            {
                rig.TakeHit(e.damage * 2.2f, e.pos, e.def.displayName);
                rig.Shove(e.aim * 9f);
            }
            if (Combat.Scenery.Blocks(e.pos + e.aim * (e.radius + 0.8f)))
            {
                e.stun = 1.5f;
                Rest(ref e, 0.3f);
                LastCrash = Time.time;
                Vector3 wall = e.pos + e.aim * e.radius;
                Combat.Marks.Crack(wall, 4.2f);
                Combat.Vfx.Burst(wall + Vector3.up, Tokens.Dust, 30, 10f, 0.4f, 0.9f, falls: true);
                Combat.Vfx.Shockwave(wall, 7f, Tokens.Dust, 30, 0.35f);
                if (Combat.Camera != null) Combat.Camera.Shake(0.6f);
                return Vector3.zero;
            }
            if (!hit && e.phaseLeft > 0f) return e.aim * step;

            // In the second phase a charge that missed is followed at once by another.
            Vector3 again = rig.transform.position - e.pos;
            again.y = 0f;
            if (second && !hit && e.counter == 0 && again.sqrMagnitude > 16f)
            {
                e.counter = 1;
                e.phase = WindUp;
                Aim(ref e, again.normalized, ReignChargeWindUp * 0.6f);
            }
            else Rest(ref e, 0.7f);
            return Vector3.zero;
        }

        // Orbs leave it one after another, fanned out so that they come at the machine from different sides.
        Vector3 Barrage(ref Enemy e, Vector3 dir, float dt)
        {
            e.phaseLeft -= dt;
            if (e.phaseLeft > 0f) return Vector3.zero;
            e.phaseLeft = 0.4f;
            e.counter--;
            Vector3 fan = Quaternion.Euler(0f, (e.counter - 1.5f) * 40f, 0f) * dir;
            Combat.Projectiles.Orb(e.pos + fan * (e.radius + 0.5f), fan, 7.2f, 105f, 6f, e.damage * 0.9f, e.def.displayName);
            e.lunge = LungeTime;
            if (e.counter <= 0) Rest(ref e, 0.9f);
            return Vector3.zero;
        }

        // Marks a spot of ground to burst after a delay. Nothing is marked inside a wall.
        public void Erupt(Vector3 pos, float radius, float delay, float damage, float burn, string source)
        {
            pos.y = 0f;
            if (Combat.Scenery.Blocks(pos)) return;
            bursts.Add(new Burst { pos = pos, radius = radius, left = delay, total = delay, damage = damage, burn = burn, source = source });
        }

        // Ground that is about to burst cracks open, and the cracks glow hotter and hotter. When
        // its time is up it throws up fire, hits the machine if it is still there, burns on for a
        // while, and leaves the floor broken and black.
        void RunBursts(float dt)
        {
            var rig = Combat.Rig;
            for (int b = bursts.Count - 1; b >= 0; b--)
            {
                var burst = bursts[b];
                burst.left -= dt;
                burst.tick += dt;
                if (burst.left > 0f)
                {
                    float coming = 1f - burst.left / burst.total;
                    // The same cracks for as long as the spot is there: they are told apart by where they are.
                    int seed = Mathf.RoundToInt(burst.pos.x * 7.3f + burst.pos.z * 13.1f);
                    Combat.Marks.Fissure(burst.pos, burst.radius * 1.15f, coming, Tokens.Heat, seed);
                    if (burst.tick >= 0.12f)
                    {
                        burst.tick = 0f;
                        // A little heat rises out of them.
                        Combat.Vfx.Rise(burst.pos, burst.radius * 0.7f, 0f, AlertColor, Vfx.Share(coming * 1.5f), 2.4f, 0.12f, 0.4f);
                    }
                    bursts[b] = burst;
                    continue;
                }
                Combat.Marks.Scorch(burst.pos, burst.radius * 1.25f);
                Combat.Marks.Crack(burst.pos, burst.radius * 1.1f);
                Combat.Vfx.Rise(burst.pos, burst.radius, 0f, Tokens.Heat, 14, 6f, 0.3f, 0.5f);
                Combat.Vfx.Shockwave(burst.pos, burst.radius * 1.4f, Tokens.Heat, 10, 0.25f);
                float reach = burst.radius + rig.Radius * 0.4f;
                if (rig.Exposed && FlatSq(rig.transform.position - burst.pos) < reach * reach) rig.TakeHit(burst.damage, burst.pos, burst.source);
                if (burst.burn > 0f) Combat.Zones.Spawn(StateType.Heat, burst.pos, burst.radius * 0.85f, burst.burn, 6f, hostile: true);
                bursts.RemoveAt(b);
            }
        }

        // How far the burst of a Bloater reaches.
        static float BurstRadius(Rank rank) => rank == Rank.Boss ? 6.5f : rank == Rank.MiniBoss ? 5f : 3.2f;

        void Hatch(EnemyDef def, Vector3 pos, int count, Rank rank, float spread)
        {
            for (int k = 0; k < count; k++)
            {
                float angle = (k + Random.value) * Mathf.PI * 2f / count;
                int born = Spawn(def, pos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (spread + 0.4f), true, rank);
                if (born < 0) break;
                enemies[born].hatched = true;
            }
            Combat.Vfx.Burst(pos + Vector3.up * 0.4f, AlertColor, 6 + count * 2, 5f, 0.18f, 0.4f);
        }

        // Some enemies circle one way and some the other.
        static float Side(int id) => (id & 1) == 0 ? 1f : -1f;

        void RemoveDead()
        {
            for (int i = Count - 1; i >= 0; i--)
            {
                if (enemies[i].hp > 0f) continue;
                var dead = enemies[i];
                enemies[i] = enemies[--Count];

                bool elite = dead.rank >= Rank.MiniBoss;
                if (!dead.burst)
                {
                    Combat.Kills++;
                    Echoes.Kill();
                    Codex.Destroyed(dead.def, dead.rank);
                    if (dead.rank == Rank.MiniBoss) RunStats.MiniBosses++;
                    if (dead.rank >= Rank.Boss) RunStats.Bosses++;
                    if (!dead.hatched && Combat.Pickups != null)
                        Combat.Pickups.Drop(dead.def.drops != null ? dead.def.drops : Combat.Config.drops, dead.rank, dead.pos);
                }
                // The wreck flies apart in the colour of the state it died in.
                var color = dead.state == StateType.None ? WreckColor : Tokens.Of(dead.state);
                Combat.Vfx.Burst(dead.pos + Vector3.up * (0.4f * dead.scale), color, elite ? 40 : dead.radius > 0.7f ? 16 : 8,
                    elite ? 10f : 6f, dead.radius * 0.55f, elite ? 1f : 0.6f, falls: true);
                if (elite)
                {
                    // Where something that large went down, the ground stays black for a while.
                    Combat.Marks.Scorch(dead.pos, dead.radius * 2.4f);
                    Combat.Vfx.Shockwave(dead.pos, dead.radius * 3f, color, 36);
                    if (Combat.Camera != null) Combat.Camera.Shake(dead.rank == Rank.Boss ? 0.5f : 0.3f);
                }
                // Ember: what dies burning sets its neighbours alight.
                if (dead.state == StateType.Heat && Echoes.Has(EchoKind.Ember))
                {
                    InCircle(dead.pos, EmberReach, hits);
                    foreach (int index in hits) ApplyState(index, StateType.Heat);
                    if (hits.Count > 0) Combat.Vfx.Wave(dead.pos, 0f, EmberReach, Tokens.Heat, 16);
                }
                // Children are added after the dead one is gone, so they land safely at the end of the list.
                OnDeath(dead);
            }
        }

        // What a species leaves behind.
        void OnDeath(in Enemy dead)
        {
            bool elite = dead.rank >= Rank.MiniBoss, boss = dead.rank == Rank.Boss;
            switch (dead.def.behaviour)
            {
                case Behaviour.Swarm when boss:
                    Hatch(dead.def, dead.pos, 6, Rank.Normal, dead.radius);
                    break;

                case Behaviour.Bloater:
                    // The burst hits the machine if it is close, and the oil stays. Fire will find it.
                    float radius = BurstRadius(dead.rank);
                    Combat.Marks.Scorch(dead.pos, radius * 0.9f, Tokens.Oil);
                    Combat.Zones.Spawn(StateType.Oil, dead.pos, boss ? 5f : elite ? 4f : 2.6f, PoolTime, 0f, hostile: true);
                    Combat.Vfx.Shockwave(dead.pos, radius, Tokens.Oil, 36);
                    Combat.Vfx.Burst(dead.pos + Vector3.up * 0.5f, Tokens.Oil, 26, 9f, 0.3f, 0.7f, falls: true);
                    var rig = Combat.Rig;
                    float reach = radius + rig.Radius * 0.5f;
                    if (rig.Exposed && FlatSq(rig.transform.position - dead.pos) < reach * reach)
                        rig.TakeHit(dead.damage, dead.pos, Ranks.Name(dead.def, dead.rank));
                    if (elite) Hatch(dead.def, dead.pos, boss ? 5 : 3, Rank.Normal, dead.radius);
                    break;

                case Behaviour.Splitter:
                    // Boss into two mini bosses, mini boss into four, each of those into two small ones.
                    if (boss) Hatch(dead.def, dead.pos, 2, Rank.MiniBoss, dead.radius * 0.5f);
                    else if (elite) Hatch(dead.def, dead.pos, 4, Rank.Normal, dead.radius * 0.5f);
                    else if (dead.rank == Rank.Normal) Hatch(dead.def, dead.pos, 2, Rank.Lesser, dead.radius * 0.5f);
                    break;
            }
        }

        // ---- Separation: a hashed grid so enemies push apart without checking every pair ----

        static int CellKey(int cx, int cz) => ((cx * 73856093) ^ (cz * 19349663)) & (HashSize - 1);

        void BuildGrid()
        {
            Array.Fill(cellHead, -1);
            for (int i = 0; i < Count; i++)
            {
                int key = CellKey(Mathf.FloorToInt(enemies[i].pos.x / CellSize), Mathf.FloorToInt(enemies[i].pos.z / CellSize));
                nextInCell[i] = cellHead[key];
                cellHead[key] = i;
            }
        }

        Vector3 Separation(int index)
        {
            Vector3 pos = enemies[index].pos, push = Vector3.zero;
            float radius = enemies[index].radius;
            int cx = Mathf.FloorToInt(pos.x / CellSize), cz = Mathf.FloorToInt(pos.z / CellSize);

            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            for (int j = cellHead[CellKey(cx + dx, cz + dz)]; j >= 0; j = nextInCell[j])
            {
                if (j == index || enemies[j].hp <= 0f) continue;
                Vector3 away = pos - enemies[j].pos;
                away.y = 0f;
                float reach = radius + enemies[j].radius, sq = away.sqrMagnitude;
                if (sq >= reach * reach || sq < 0.0001f) continue;
                float dist = Mathf.Sqrt(sq);
                push += away / dist * (reach - dist);
            }
            return push * PushStrength;
        }

        // ---- Drawing: one instanced batch per species and state colour ----

        void Draw()
        {
            Vector3 target = Combat.Rig.transform.position;
            foreach (var kind in kinds)
            {
                Array.Clear(kind.drawn, 0, Looks);
                Array.Clear(kind.legsDrawn, 0, Looks);
            }
            Elites.Clear();

            for (int i = 0; i < Count; i++)
            {
                ref var enemy = ref enemies[i];
                var def = enemy.def;
                var kind = kinds[enemy.kind];
                var behaviour = def.behaviour;
                bool windUp = enemy.phase == WindUp;
                // Hit enemies are white for an instant. One that is about to attack blinks white. A Warden
                // does not: it is too large for that, and shows what is coming on the ground instead.
                bool white = enemy.flash > 0f || (windUp && behaviour != Behaviour.Warden && Mathf.Sin(Time.time * 38f) > 0f);
                // A Warden in its second phase glows as if it were burning.
                bool molten = behaviour == Behaviour.Warden && enemy.stage >= 2 && enemy.state == StateType.None;
                int look = white ? FlashLook : molten ? (int)StateType.Heat : (int)enemy.state;

                Vector3 to = target - enemy.pos;
                to.y = 0f;
                float distance = to.magnitude;
                if (!enemy.aggro) to = WanderDirection(i);
                else if (enemy.phase != Idle && (behaviour == Behaviour.Charger || behaviour == Behaviour.Lancer
                    || (behaviour == Behaviour.Warden && enemy.move == MoveCharge))) to = enemy.aim;
                var facing = to.sqrMagnitude > 0.01f ? Quaternion.LookRotation(to) : Quaternion.identity;
                // An attacker jabs forward and back within a quarter of a second.
                Vector3 ground = enemy.pos;
                if (enemy.lunge > 0f) ground += facing * Vector3.forward * (Mathf.Sin(enemy.lunge / LungeTime * Mathf.PI) * LungeReach * enemy.scale);

                // Walking: the legs swing in turn and the body bobs with each step. Stunned enemies freeze.
                float pace = enemy.aggro ? 1f : IdlePace;
                bool still = enemy.stun > 0f || windUp;
                float step = still ? 0f : Time.time * StepSpeed * pace + i * 1.7f;
                float swing = still ? 0f : Mathf.Sin(step) * LegSwing * (enemy.aggro ? 1f : 0.5f);
                Vector3 size = Vector3.one * enemy.scale;
                Vector3 pos = ground;
                if (def.wobble)
                {
                    // No legs: the body squashes and stretches instead.
                    float wave = still ? 0f : Mathf.Sin(step * 0.6f) * 0.1f;
                    size = new Vector3(size.x * (1f - wave * 0.5f), size.y * (1f + wave), size.z * (1f - wave * 0.5f));
                }
                else pos += Vector3.up * (Mathf.Abs(Mathf.Cos(step)) * BodyBob * enemy.scale);
                // A Bloater swells before it bursts.
                if (windUp && behaviour == Behaviour.Bloater) size *= 1f + 0.4f * (1f - Mathf.Clamp01(enemy.phaseLeft / (FuseTime * def.windUp)));

                if (enemy.rank >= Rank.MiniBoss)
                {
                    Elites.Add(new Elite
                    {
                        id = enemy.id, name = Ranks.Name(def, enemy.rank), rank = enemy.rank, stage = enemy.stage,
                        // A Warden's line changes with its phase. For the others it says what their boss form adds.
                        trait = enemy.rank == Rank.Warden ? (enemy.stage >= 2 ? def.bossTrait : def.trait)
                            : string.IsNullOrEmpty(def.bossTrait) ? def.trait : def.bossTrait,
                        hull = Mathf.Clamp01(enemy.hp / enemy.maxHp), distance = distance, aggro = enemy.aggro,
                        head = enemy.pos + Vector3.up * (def.height * enemy.scale + 0.8f),
                    });
                }

                if (def.mesh == null)
                {
                    // No model yet: a cube stands in, lifted and scaled to the enemy's size.
                    float side = enemy.radius * 2f;
                    Push(ref kind.bodies[look], ref kind.drawn[look], Matrix4x4.TRS(pos + Vector3.up * (side * 0.5f), facing, new Vector3(side, side, side)));
                    continue;
                }
                Push(ref kind.bodies[look], ref kind.drawn[look], Matrix4x4.TRS(pos, facing, size));
                if (def.legMesh == null) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 hip = ground + facing * (new Vector3(side * def.legSpacing, def.hipHeight, 0f) * enemy.scale);
                    Push(ref kind.legs[look], ref kind.legsDrawn[look], Matrix4x4.TRS(hip, facing * Quaternion.Euler(swing * side, 0f, 0f), size));
                }
            }

            foreach (var kind in kinds)
                for (int look = 0; look < Looks; look++)
                {
                    if (kind.drawn[look] == 0) continue;
                    var materials = MaterialsFor(kind, look);
                    DrawBatch(kind.def.mesh != null ? kind.def.mesh : cube, kind.bodies[look], kind.drawn[look], materials, target);
                    if (kind.legsDrawn[look] > 0) DrawBatch(kind.def.legMesh, kind.legs[look], kind.legsDrawn[look], materials, target);
                }
        }

        static void Push(ref Matrix4x4[] list, ref int count, Matrix4x4 matrix)
        {
            if (list == null) list = new Matrix4x4[64];
            else if (count == list.Length) Array.Resize(ref list, count * 2);
            list[count++] = matrix;
        }

        // The four materials of a species in one look. The body takes the state colour; a hit turns
        // the whole model white.
        static Material[] MaterialsFor(Kind kind, int look)
        {
            if (kind.looks[look] != null) return kind.looks[look];
            var config = Combat.Config;
            if (look == FlashLook)
            {
                var white = Slots.Or(config.flash, config.body);
                return kind.looks[look] = new[] { white, white, white, white };
            }
            return kind.looks[look] = new[]
            {
                Slots.Or(config.StateMaterial((StateType)look), Slots.Or(kind.def.body, config.enemy)),
                Slots.Or(kind.def.plate, Slots.Or(config.enemyPlate, config.part)),
                Slots.Or(config.dark, config.part),
                Slots.Or(kind.def.glow, Slots.Or(config.enemyGlow, config.body)),
            };
        }

        // Where an idle enemy drifts. It changes slowly and differs from one enemy to the next.
        static Vector3 WanderDirection(int index)
        {
            float heading = Mathf.PerlinNoise(index * 0.37f, Time.time * 0.05f) * Mathf.PI * 4f;
            return new Vector3(Mathf.Cos(heading), 0f, Mathf.Sin(heading));
        }

        void DrawBatch(Mesh mesh, Matrix4x4[] source, int count, Material[] materials, Vector3 center)
        {
            for (int sub = 0; sub < mesh.subMeshCount && sub < materials.Length; sub++)
            {
                var renderParams = new RenderParams(materials[sub])
                {
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true,
                    worldBounds = new Bounds(center, Vector3.one * 300f),
                };
                for (int start = 0; start < count; start += BatchSize)
                {
                    int batch = Mathf.Min(BatchSize, count - start);
                    Array.Copy(source, start, buffer, 0, batch);
                    Graphics.RenderMeshInstanced(renderParams, mesh, sub, buffer, batch);
                }
            }
        }
    }
}
