using System.Collections.Generic;
using UnityEngine;

namespace Pulse
{
    // Runs what one armor module does when the machine is hit: the burst of a reactive plate, and
    // the spikes an ability can give any plate. Plain plates have nothing to run.
    public sealed class SupportRunner
    {
        const float OuterRing = 1.9f, ThornReach = 1.6f;

        readonly SupportDef def;
        readonly SlotMods mods;
        readonly ModuleStats stats;
        readonly Rig rig;
        readonly List<int> hits = new List<int>(64);
        float cooldown;

        public SupportRunner(SupportDef def, SlotMods mods, ModuleStats stats, Rig rig)
        {
            this.def = def;
            this.mods = mods;
            this.stats = stats;
            this.rig = rig;
        }

        public void Tick(float dt) => cooldown -= dt;

        // from: where the blow came from.
        public void OnRigHit(Vector3 from)
        {
            // Spikes: whatever stands where the blow came from is hurt in turn.
            if (stats.thorns > 0f)
            {
                Combat.Enemies.Explode(from, ThornReach, stats.thorns, StateType.None, hits);
                if (hits.Count > 0) Combat.Vfx.Sparks(from + Vector3.up * 0.6f, Tokens.Chalk, 8, 7f);
            }

            // The burst counts as a Kinetic area hit, so feeds and shapers on its wire change it.
            if (def.trigger != TriggerType.BurstWhenHit || cooldown > 0f) return;
            cooldown = def.triggerCooldown;
            var center = rig.transform.position;
            var state = mods.feed;
            float damage = def.triggerDamage * stats.damage * mods.damage, radius = def.triggerRadius * mods.range;
            Combat.Enemies.Explode(center, radius, damage, state, hits);
            Combat.Vfx.Shockwave(center, radius, Tokens.Of(state));
            if (!mods.Has(Shapers.Splitter)) return;
            float outer = radius * OuterRing;
            Combat.Enemies.Explode(center, outer, damage, state, hits, radius * 1.2f);
            Combat.Vfx.Shockwave(center, outer, Tokens.Of(state), 44, 0.4f);
        }
    }
}
