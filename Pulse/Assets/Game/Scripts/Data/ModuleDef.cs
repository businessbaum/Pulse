using UnityEngine;

namespace Pulse
{
    // Base for everything that goes into a slot: weapons, systems and support modules.
    public abstract class ModuleDef : ScriptableObject
    {
        public string displayName;
        [TextArea] public string description;
        public int powerCost = 1;
        [Tooltip("Optional. Without a mesh the module is shown as a plain block.")]
        public Mesh mesh;

        public abstract SlotType Slot { get; }

        // A module has an output when it produces a hit that a system can change.
        public virtual bool HasOutput => false;
        public virtual Delivery OutputDelivery => Delivery.Area;
        public virtual StateType OutputState => StateType.None;
    }
}
