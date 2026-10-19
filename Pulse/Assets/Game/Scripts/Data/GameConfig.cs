using UnityEngine;

namespace Pulse
{
    // Everything a run needs: content, materials and tuning values.
    [CreateAssetMenu(menuName = "Pulse/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Content")]
        public WorldDef world;
        public SceneryDef scenery;
        public LobbyDef lobby;
        public Mesh boltMesh;
        public Mesh globMesh;
        [Tooltip("Raised by 'Create Default Content' when it brings older assets up to date.")]
        public int contentVersion;
        [Tooltip("The machines a new player starts with.")]
        public Loadout[] loadouts;
        [Tooltip("Parts every player has from the start, besides what the starting machines are made of.")]
        public ScriptableObject[] startingParts;
        [Header("Every part a machine can be built from. A saved machine is read back from these by name.")]
        public FrameDef[] frames;
        public ReactorDef[] reactors;
        public DriveDef[] drives;
        public ModuleDef[] modules;
        [Tooltip("Every ability a dropped part can carry.")]
        public PerkDef[] perks;
        public EnemyDef grunt;
        public EnemyDef brute;
        [Tooltip("What enemies leave behind, unless a species has a pool of its own.")]
        public DropPool drops;
        [Tooltip("Every Echo there is. Which of them a run can offer depends on the dungeons cleared so far.")]
        public EchoDef[] echoes;

        [Header("Things in the world")]
        [Tooltip("Beats grown out of the ground. Driving into one breaks it.")]
        public Mesh veinMesh;
        [Tooltip("A crate to be opened, and what is left of it afterwards.")]
        public Mesh cacheMesh;
        public Mesh cacheOpenMesh;
        [Tooltip("What a Mine Layer lays.")]
        public Mesh mineMesh;

        [Header("Materials")]
        public Material ground;
        public Material floor;
        public Material body;
        public Material part;
        public Material enemy;
        public Material pulse;
        [Tooltip("Dark metal and rubber: the third slot of every model.")]
        public Material dark;
        [Tooltip("White glow: kinetic shots, and an enemy for an instant when it is hit.")]
        public Material flash;
        public Material enemyPlate;
        public Material enemyGlow;
        public Material particle;
        public Material heat;
        public Material shock;
        public Material cryo;
        public Material oil;
        public Material magnet;

        [Header("States")]
        public float stateDuration = 5f;
        public float burnDps = 4f;
        [Range(0f, 1f)] public float oilSlow = 0.6f;
        public float shockStun = 0.25f;
        [Tooltip("The share of its pace a chilled enemy keeps, and what it keeps with the Echo Deep Cold.")]
        [Range(0f, 1f)] public float chillSlow = 0.65f;
        [Range(0f, 1f)] public float deepChillSlow = 0.4f;

        [Header("Reactions")]
        public float firestormRadius = 2.6f;
        public float firestormDuration = 4f;
        public float firestormDps = 14f;
        public float flashRadius = 3.2f;
        public float flashDamage = 30f;
        public int plasmaArcs = 3;
        public float plasmaRange = 6f;
        public float plasmaDamage = 18f;
        [Tooltip("Thermal Shock: damage at once, and how much more damage its targets take for how long.")]
        public float thermalShockRadius = 2.4f;
        public float thermalShockDamage = 24f;
        public float brittleBonus = 0.4f;
        public float brittleTime = 6f;
        [Tooltip("Superconductor: lightning that jumps from enemy to enemy and loses nothing.")]
        public int superconductorJumps = 6;
        public float superconductorRange = 7f;
        public float superconductorDamage = 16f;
        [Tooltip("Black Ice: everything within the radius slides apart, stunned, and is hurt.")]
        public float blackIceRadius = 3.6f;
        public float blackIceDamage = 12f;
        public float blackIceStun = 1.3f;
        public float blackIceSlide = 9f;

        // Null for Kinetic.
        public Material StateMaterial(StateType state) => state switch
        {
            StateType.Heat => heat,
            StateType.Shock => shock,
            StateType.Cryo => cryo,
            StateType.Oil => oil,
            StateType.Magnet => magnet,
            _ => null,
        };
    }
}
