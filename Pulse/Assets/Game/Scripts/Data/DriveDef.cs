using UnityEngine;

namespace Pulse
{
    [CreateAssetMenu(menuName = "Pulse/Drive")]
    public sealed class DriveDef : ScriptableObject
    {
        public string displayName;
        [TextArea] public string description;
        public float speed = 9f;
        public float acceleration = 18f;
        [Tooltip("Degrees per second at full speed.")]
        public float turnRate = 150f;
        [Tooltip("Off: the machine has to roll to turn, like wheels.")]
        public bool turnsOnSpot;

        [Header("Nitro: a burst of speed that refills by itself")]
        [Tooltip("Top speed is multiplied by this while Nitro burns.")]
        public float nitroBoost = 1.7f;
        [Tooltip("Seconds a full tank lasts.")]
        public float nitroSeconds = 2.5f;
        [Tooltip("Seconds an empty tank takes to refill.")]
        public float nitroRecharge = 10f;
    }
}
