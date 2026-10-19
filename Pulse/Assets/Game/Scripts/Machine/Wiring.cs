namespace Pulse
{
    // What the systems wired to one slot do to it.
    public struct SlotMods
    {
        public Shapers shapers;
        public StateType feed;
        // Multipliers from boosters, and from the levels and abilities of every system on the wire.
        public float damage, range;

        public bool Has(Shapers shaper) => (shapers & shaper) != 0;
    }

    // Rules from machine-concept.md, sections 3.2 and 4.4. No scene objects involved.
    public static class Wiring
    {
        // longWire: the Echo of that name is in force, and every system also reaches every weapon
        // it is not wired to.
        public static SlotMods Resolve(Loadout loadout, string slotId, bool longWire = false)
        {
            var mods = new SlotMods { damage = 1f, range = 1f };
            var target = loadout.In(slotId);
            if (target == null || !target.HasOutput) return mods;

            foreach (var slot in loadout.frame.slots)
            {
                if (!(loadout.In(slot.id) is SystemDef system)) continue;
                if (!Wired(loadout.frame, slot.id, slotId) && !(longWire && target is WeaponDef)) continue;
                var stats = Tuning.Of(system, loadout.ItemIn(slot.id));
                mods.damage *= stats.wiredDamage;
                mods.range *= stats.wiredRange;
                switch (system.kind)
                {
                    case SystemKind.Shaper:
                        mods.shapers |= system.shaper;
                        break;
                    case SystemKind.Feed:
                        // A feed only reaches outputs that have no state of their own.
                        if (target.OutputState == StateType.None && mods.feed == StateType.None)
                            mods.feed = system.feedState;
                        break;
                }
            }
            return mods;
        }

        // Whether a wire runs from that system slot to that slot.
        public static bool Wired(FrameDef frame, string system, string slot)
        {
            foreach (var wire in frame.wires)
                if (wire.system == system && wire.slot == slot) return true;
            return false;
        }

        // Whether a machine has a system and a weapon with no wire between them: only then does a
        // Long Wire change anything.
        public static bool GainsFromLongWire(Loadout loadout)
        {
            foreach (var from in loadout.frame.slots)
            {
                if (!(loadout.In(from.id) is SystemDef)) continue;
                foreach (var to in loadout.frame.slots)
                    if (loadout.In(to.id) is WeaponDef && !Wired(loadout.frame, from.id, to.id)) return true;
            }
            return false;
        }

        // The state a module's output carries once feeds are applied.
        public static StateType EffectiveState(ModuleDef module, SlotMods mods) =>
            module.OutputState != StateType.None ? module.OutputState : mods.feed;

        // A wire is live when its system changes something at the other end.
        public static bool IsLive(Loadout loadout, WireDef wire)
        {
            var target = loadout.In(wire.slot);
            if (target == null || !target.HasOutput) return false;
            if (!(loadout.In(wire.system) is SystemDef system)) return false;
            return system.kind switch
            {
                SystemKind.Shaper => system.shaper != Shapers.None,
                SystemKind.Feed => target.OutputState == StateType.None,
                SystemKind.Booster => true,
                _ => false,
            };
        }
    }
}
