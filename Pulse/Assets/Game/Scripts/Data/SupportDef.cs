using UnityEngine;

namespace Pulse
{
    // A module for an armor slot: a plate that takes damage off hits, adds hull, or answers a hit
    // with a burst. (The class keeps the name these modules had while their slot was called support.)
    [CreateAssetMenu(menuName = "Pulse/Armor")]
    public sealed class SupportDef : ModuleDef
    {
        [Header("Stats")]
        public float armor;
        public float hull;

        [Header("Trigger")]
        public TriggerType trigger;
        public float triggerDamage = 15f;
        public float triggerRadius = 4f;
        public float triggerCooldown = 1.5f;

        public override SlotType Slot => SlotType.Armor;
        public override bool HasOutput => trigger != TriggerType.None;
        public override Delivery OutputDelivery => Delivery.Area;
    }
}
