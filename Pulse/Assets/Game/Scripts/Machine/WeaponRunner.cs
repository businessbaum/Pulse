using System.Collections.Generic;
using UnityEngine;

namespace Pulse
{
    // Fires one mounted weapon. What a shot looks like depends on the weapon's delivery and on its wiring.
    public sealed class WeaponRunner
    {
        const float FrontBonus = 1.5f, SplitAngle = 14f, ConeSplitAngle = 42f, OuterRing = 1.9f, LobTime = 0.55f;
        // A shell flies longer than a glob of oil: it is seen coming down.
        const float ShellTime = 0.8f, ShellFire = 6f;
        // Mines: how far from the machine one is laid, how far apart a split pair lies, how much smaller each of the pair is.
        const float MineBehind = 2.3f, MineApart = 1.2f, MineSplit = 0.7f;
        // A saw bites harder the faster the machine is: from SawStill of its damage standing to
        // SawStill + 1 at top speed, and more with Nitro.
        const float SawStill = 0.6f, SawMost = 1.8f;
        const int MaxArcs = 10;
        static readonly Color CasingColor = new Color(0.85f, 0.7f, 0.35f);

        readonly WeaponDef def;
        readonly SlotDef slot;
        readonly SlotMods mods;
        readonly ModuleStats stats;
        readonly Rig rig;
        readonly Transform muzzle;
        readonly List<int> hits = new List<int>(64);
        float cooldown;

        // stats: what the level and the ability of this very weapon make of it.
        public WeaponRunner(WeaponDef def, SlotDef slot, SlotMods mods, ModuleStats stats, Rig rig, Transform muzzle)
        {
            this.def = def;
            this.slot = slot;
            this.mods = mods;
            this.stats = stats;
            this.rig = rig;
            this.muzzle = muzzle;
        }

        StateType State => Wiring.EffectiveState(def, mods);
        bool Split => mods.Has(Shapers.Splitter);
        // An Echo can add to what every weapon deals.
        float Damage => def.damage * stats.damage * mods.damage * (slot.mount == Mount.Front ? FrontBonus : 1f) * Echoes.Damage;
        // How far it reaches. For a pulse around the machine, that is how wide the pulse is.
        float Reach => stats.range * mods.range;
        float Range => def.range * Reach;
        float Radius => def.radius * (def.anchoredToRig ? Reach : 1f);

        // The state one hit carries. A Kinetic weapon with an ability for it sometimes applies that state.
        StateType Strike()
        {
            var state = State;
            return state == StateType.None && stats.stateChance > 0f && Random.value < stats.stateChance ? stats.state : state;
        }

        public void Tick(float dt)
        {
            cooldown -= dt;
            if (cooldown > 0f) return;

            Vector3 origin = muzzle.position;
            int target = Combat.Enemies.Nearest(origin, Range, sight: true);
            if (target < 0) return;

            Vector3 dir = slot.mount switch
            {
                Mount.Front => rig.transform.forward,
                Mount.Rear => -rig.transform.forward,
                _ => Flat(Combat.Enemies[target].pos - origin),
            };

            if (slot.mount == Mount.Turret) muzzle.rotation = Quaternion.LookRotation(dir);

            switch (def.delivery)
            {
                case Delivery.Projectile: FireProjectiles(origin, dir); break;
                case Delivery.Beam: FireCones(origin, dir); break;
                case Delivery.Area when def.anchoredToRig: PulseAroundRig(); break;
                case Delivery.Area when def.shell: LobShell(origin, dir, target); break;
                case Delivery.Area: DropZone(origin, dir, target); break;
                case Delivery.Deployable: LayMines(dir); break;
                case Delivery.Contact: Bite(origin, dir); break;
            }
            cooldown = def.cooldown / Mathf.Max(0.1f, stats.rate * Echoes.Rate);
        }

        // Splitter: the shot forks into three.
        void FireProjectiles(Vector3 origin, Vector3 dir)
        {
            Shoot(origin, dir);
            if (!Split) return;
            Shoot(origin, Quaternion.Euler(0f, SplitAngle, 0f) * dir);
            Shoot(origin, Quaternion.Euler(0f, -SplitAngle, 0f) * dir);
        }

        void Shoot(Vector3 origin, Vector3 dir)
        {
            var state = Strike();
            Combat.Projectiles.Fire(origin, dir, def.projectileSpeed, Range, Damage, state, pierce: def.pierce);
            Combat.Vfx.Sparks(origin + dir * 1.05f + Vector3.up * 0.3f, Tokens.Of(state), 4, 7f, 0.3f, 0.1f);
            if (def.pierce > 0)
            {
                // A shot that goes through things leaves its whole path lit for an instant.
                Vector3 from = origin + Vector3.up * 0.3f + dir * 1.1f;
                Combat.Fx.Line(from, from + dir * Range, Tokens.Of(state), 0.1f, 0.07f);
                return;
            }
            // A spent casing drops out to the side.
            Combat.Vfx.Burst(origin + Vector3.up * 0.4f, CasingColor, 1, 3f, 0.09f, 0.6f, falls: true);
        }

        // Splitter: two more cones to the sides.
        void FireCones(Vector3 origin, Vector3 dir)
        {
            Cone(origin, dir);
            if (!Split) return;
            Cone(origin, Quaternion.Euler(0f, ConeSplitAngle, 0f) * dir);
            Cone(origin, Quaternion.Euler(0f, -ConeSplitAngle, 0f) * dir);
        }

        void Cone(Vector3 origin, Vector3 dir)
        {
            Combat.Enemies.InCone(origin, dir, Range, def.coneHalfAngle, hits);
            foreach (int index in hits) Combat.Enemies.Hit(index, Damage, Strike());
            Combat.Vfx.Stream(origin + dir * 0.6f + Vector3.up * 0.3f, dir, def.coneHalfAngle, Range, Tokens.Of(State), 9);
        }

        // A pulse spreads out from the machine as a wave of sparks, and arcs jump to everything it
        // reaches. Splitter: the wave runs on to a second, wider reach.
        void PulseAroundRig()
        {
            Vector3 center = rig.transform.position;
            float radius = Radius;
            Combat.Enemies.Explode(center, radius, Damage, State, hits);
            Arcs();
            Combat.Vfx.Wave(center, rig.Radius * 0.6f, radius, Tokens.Of(State), 44);
            if (!Split) return;
            float outer = radius * OuterRing;
            Combat.Enemies.Explode(center, outer, Damage, State, hits, radius * 1.2f);
            Arcs();
            Combat.Vfx.Wave(center, radius, outer, Tokens.Of(State), 60, 0.3f);
        }

        // Where a lobbed thing comes down: on the target from a turret, ahead of a front mount,
        // close behind a rear one.
        Vector3 LandingSpot(Vector3 origin, Vector3 dir, int target) => slot.mount switch
        {
            Mount.Front => origin + dir * (Range * 0.5f),
            Mount.Rear => origin + dir * (def.radius + 1.5f),
            _ => Combat.Enemies[target].pos,
        };

        void DropZone(Vector3 origin, Vector3 dir, int target)
        {
            Vector3 center = LandingSpot(origin, dir, target);
            // The zone appears where the glob lands, not the instant the weapon fires.
            Vector3 nozzle = origin + Vector3.up * 0.5f;
            Combat.Projectiles.Lob(nozzle, center, LobTime, State, def.radius, def.zoneDuration, Damage, ring: false);
            if (Split) Combat.Projectiles.Lob(nozzle, center, LobTime * 1.15f, State, def.radius * OuterRing, def.zoneDuration, Damage, ring: true);
            Combat.Vfx.Burst(nozzle, Tokens.Of(State), 5, 3f, 0.14f, 0.3f, falls: true);
        }

        // A shell bursts where it lands, for its damage. With a state on it, that state stays behind
        // on the ground for a while. Splitter: a second shell bursts in a ring around the first.
        void LobShell(Vector3 origin, Vector3 dir, int target)
        {
            // Aimed at where the target is. A shell is slow, so a crowd is a better target than a runner.
            Vector3 center = LandingSpot(origin, dir, target);
            Vector3 mouth = origin + Vector3.up * 0.7f;
            var state = Strike();
            float stays = state != StateType.None ? def.zoneDuration : 0f, burns = state == StateType.Heat ? ShellFire : 0f;
            Combat.Projectiles.Lob(mouth, center, ShellTime, state, def.radius, stays, burns, ring: false, blast: Damage);
            if (Split) Combat.Projectiles.Lob(mouth, center, ShellTime * 1.15f, state, def.radius * OuterRing, stays, burns, ring: true, blast: Damage);
            Combat.Vfx.Burst(mouth, Tokens.Dust, 8, 5f, 0.2f, 0.4f);
            Combat.Vfx.Sparks(mouth, Tokens.Of(state), 6, 8f);
            if (Combat.Camera != null) Combat.Camera.Shake(0.05f);
        }

        // A mine is laid behind a rear mount, ahead of a front one, and toward the target from a
        // turret. It lies until something walks onto it. Splitter: two smaller ones, side by side.
        void LayMines(Vector3 dir)
        {
            Vector3 at = rig.transform.position + dir * MineBehind;
            float life = def.zoneDuration;
            if (!Split)
            {
                Combat.Projectiles.Mine(at, def.radius, Damage, Strike(), life);
                return;
            }
            Vector3 side = Vector3.Cross(Vector3.up, dir) * MineApart;
            Combat.Projectiles.Mine(at + side, def.radius * MineSplit, Damage * MineSplit, Strike(), life);
            Combat.Projectiles.Mine(at - side, def.radius * MineSplit, Damage * MineSplit, Strike(), life);
        }

        // A saw cuts whatever is right in front of it. The faster the machine, the deeper the cut,
        // so it is a weapon for ramming. Splitter: it also cuts to both sides.
        void Bite(Vector3 origin, Vector3 dir)
        {
            float speed = rig.Velocity.magnitude / Mathf.Max(0.1f, rig.Loadout.drive.speed);
            float damage = Damage * (SawStill + Mathf.Min(speed, SawMost));
            Cut(origin, dir, damage);
            if (!Split) return;
            Cut(origin, Quaternion.Euler(0f, ConeSplitAngle * 2f, 0f) * dir, damage);
            Cut(origin, Quaternion.Euler(0f, -ConeSplitAngle * 2f, 0f) * dir, damage);
        }

        void Cut(Vector3 origin, Vector3 dir, float damage)
        {
            Combat.Enemies.InCone(origin, dir, Range, def.coneHalfAngle, hits);
            if (hits.Count == 0) return;
            foreach (int index in hits) Combat.Enemies.Hit(index, damage, Strike());
            Vector3 teeth = origin + dir * 0.9f + Vector3.up * 0.35f;
            Combat.Vfx.Sparks(teeth, Tokens.Of(State), 7, 10f, 0.4f, 0.16f);
            Combat.Vfx.Burst(teeth, CasingColor, 2, 5f, 0.08f, 0.4f, falls: true);
        }

        // Lightning from the top of the weapon to everything the pulse just hit.
        void Arcs()
        {
            Vector3 top = muzzle.position + Vector3.up * 0.9f;
            var color = Tokens.Of(State);
            for (int k = 0; k < hits.Count && k < MaxArcs; k++)
            {
                Vector3 end = Combat.Enemies[hits[k]].pos + Vector3.up * 0.6f;
                Combat.Fx.Bolt(top, end, color);
                Combat.Vfx.Sparks(end, color, 4);
            }
            Combat.Vfx.Sparks(top, color, 6, 5f);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }
    }
}
