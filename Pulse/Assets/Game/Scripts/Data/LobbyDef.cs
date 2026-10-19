using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Pulse
{
    // The models and colours of the lobby, the Roundhouse.
    [CreateAssetMenu(menuName = "Pulse/Lobby")]
    public sealed class LobbyDef : ScriptableObject
    {
        // A model the lobby is furnished with, looked up by its name.
        [Serializable]
        public struct Named
        {
            public string name;
            public Mesh mesh;
        }

        [Header("Building")]
        public Mesh wall;
        public Mesh bayDoor;
        public Mesh gate;
        public Mesh turntable;
        [Tooltip("The middle of the turntable, which turns with the machine on it.")]
        public Mesh deck;
        [Header("The four arms around the turntable, each in pieces that turn on their joints")]
        public Mesh armBase;
        public Mesh armTurret;
        public Mesh armUpper;
        public Mesh armFore;
        public Mesh armWelder;
        public Mesh armClaw;
        public Mesh lamp;
        public Mesh crate;
        public Mesh barrel;

        [Header("Stations and the machines that run them")]
        public Mesh workbench;
        public Mesh shelf;
        public Mesh furnace;
        [Tooltip("Stands next to the furnace: where frames hang.")]
        public Mesh frameRack;
        [FormerlySerializedAs("fitter")] public Mesh mechanic;
        public Mesh archivist;
        public Mesh smelter;

        [Header("Rooms behind bay doors")]
        [Tooltip("What the door of the Tuner's room costs, in Beats.")]
        public int tunerRoomPrice = 150;
        [Tooltip("The door that can be bought open, the room behind it and everything in it, by name.")]
        public Named[] models = Array.Empty<Named>();

        [Header("The character the player walks with")]
        public Mesh character;
        public Mesh characterLeg;
        public Mesh characterArm;
        public float walkSpeed = 4.2f;

        [Header("Materials and colours")]
        public Material wallMaterial;
        public Material trim;
        public Material lampGlow;
        public Material fireGlow;
        public Color floor = Color.gray;
        public Color floorAlt = Color.gray;
        public Color floorBand = Color.gray;

        public Mesh Model(string name)
        {
            foreach (var one in models)
                if (one.name == name) return one.mesh;
            return null;
        }
    }
}
