using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Pulse
{
    [Serializable]
    public struct SlotDef
    {
        public string id;
        public SlotType type;
        [Tooltip("Hardpoints only: where the weapon points.")]
        public Mount mount;
        [Tooltip("Position on the machine.")]
        public Vector3 localPos;
        [Tooltip("Position in the wiring diagram.")]
        public Vector2 schematicPos;
    }

    [Serializable]
    // A wire runs from a system slot or a socket to the slot its system acts on.
    public struct WireDef
    {
        [FormerlySerializedAs("bay")] public string system;
        public string slot;
    }

    // What a machine is built on. It decides which slots there are and how they are wired, and
    // nothing else can change that: a frame is bought as it is, and upgrading it only makes it tougher.
    [CreateAssetMenu(menuName = "Pulse/Frame")]
    public sealed class FrameDef : ScriptableObject
    {
        // One step of upgrading a frame at the Smelter.
        [Serializable]
        public struct Mark
        {
            public int cost;
            [Tooltip("Hull this Mark adds to the one before.")]
            public float hull;
            public float armor;
        }

        public string displayName;
        [TextArea] public string description;
        [Tooltip("What it costs at the Smelter, in Beats. Zero: every player has it from the start.")]
        public int price;
        [Tooltip("Hull at Mark I.")]
        public float hull = 100f;
        [Tooltip("Mark II, Mark III and so on: what each costs and adds. A Mark never changes the slots.")]
        public Mark[] marks = Array.Empty<Mark>();
        public Vector3 bodySize = new Vector3(1.7f, 0.6f, 2.8f);
        [Tooltip("Optional models. Without them the body and wheels are plain blocks.")]
        public Mesh bodyMesh;
        public Mesh wheelMesh;
        public Vector2 schematicSize = new Vector2(640f, 236f);
        public SlotDef[] slots = Array.Empty<SlotDef>();
        public WireDef[] wires = Array.Empty<WireDef>();

        // Hull and armour at a Mark. 0 is Mark I.
        public float HullAt(int mark)
        {
            float sum = hull;
            for (int i = 0; i < mark && i < marks.Length; i++) sum += marks[i].hull;
            return sum;
        }

        public float ArmorAt(int mark)
        {
            float sum = 0f;
            for (int i = 0; i < mark && i < marks.Length; i++) sum += marks[i].armor;
            return sum;
        }

        public int Count(SlotType type)
        {
            int count = 0;
            foreach (var slot in slots)
                if (slot.type == type) count++;
            return count;
        }
    }
}
