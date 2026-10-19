using UnityEngine;

namespace Pulse
{
    [CreateAssetMenu(menuName = "Pulse/Weapon")]
    public sealed class WeaponDef : ModuleDef
    {
        public Delivery delivery;
        public StateType state;
        public float damage = 5f;
        public float cooldown = 0.5f;
        public float range = 12f;

        [Header("Projectile")]
        public float projectileSpeed = 30f;
        [Tooltip("How many enemies a shot goes through before the one that stops it.")]
        public int pierce;

        [Header("Beam")]
        public float coneHalfAngle = 18f;

        [Header("Area")]
        [Tooltip("On: a pulse around the machine. Off: a zone dropped at the target.")]
        public bool anchoredToRig;
        public float radius = 3f;
        [Tooltip("How long a dropped zone stays. Ignored for pulses.")]
        public float zoneDuration = 5f;
        [Tooltip("On: what it lobs is a shell. It bursts where it lands for its damage, and only leaves a zone when it carries a state.")]
        public bool shell;

        // Deployable: radius is how far a mine's blast reaches, zoneDuration how long a mine lies,
        // range how near an enemy must be before one is laid.
        // Contact: range is how far in front of the muzzle it cuts, coneHalfAngle how wide.

        public override SlotType Slot => SlotType.Hardpoint;
        public override bool HasOutput => true;
        public override Delivery OutputDelivery => delivery;
        public override StateType OutputState => state;
    }
}
