using UnityEngine;

namespace Pulse
{
    // A module for a system slot. It does nothing by itself: it changes whatever it is wired to.
    [CreateAssetMenu(menuName = "Pulse/System")]
    public sealed class SystemDef : ModuleDef
    {
        public SystemKind kind;
        [Tooltip("Used when kind is Shaper.")]
        public Shapers shaper;
        [Tooltip("Used when kind is Feed.")]
        public StateType feedState;
        [Header("Booster: added to what it is wired to. 0.4 means 40 percent more.")]
        public float rangeBonus;
        public float damageBonus;

        public override SlotType Slot => SlotType.System;
    }
}
