using UnityEngine;

namespace Pulse
{
    [CreateAssetMenu(menuName = "Pulse/Enemy")]
    public sealed class EnemyDef : ScriptableObject
    {
        public string displayName;
        public float hp = 20f;
        public float speed = 3.5f;
        public float radius = 0.5f;
        public float armor;
        [Tooltip("Damage of its main attack: a hit, a ram, a shot, a slam or a burst.")]
        public float contactDamage = 5f;

        [Header("How it fights")]
        public Behaviour behaviour;
        [Tooltip("How many appear together. Mini bosses and bosses always come alone.")]
        public int packSize = 1;
        [Tooltip("One line for the player: what makes this species different.")]
        public string trait;
        [Tooltip("One line for the player: what its boss does on top of that. Shown under the boss bar.")]
        public string bossTrait;
        public string miniBossName;
        public string bossName;
        [Tooltip("How long it shows an attack before the attack lands, as a multiple of the base time. Higher gives the player more time to dodge.")]
        public float windUp = 1.6f;
        [Tooltip("What it leaves behind. Empty: the common pool of the game.")]
        public DropPool drops;

        [Header("Model")]
        [Tooltip("Optional. Without a mesh the enemy is drawn as a cube.")]
        public Mesh mesh;
        [Tooltip("Height of the model, for the bar that floats over a mini boss.")]
        public float height = 1.2f;
        [Tooltip("The materials of the model: body, plates and glow. Empty fields use the common enemy materials. The body is replaced by the state colour while a state is on the enemy.")]
        public Material body;
        public Material plate;
        public Material glow;
        [Tooltip("For species without legs: the body squashes and stretches as it moves.")]
        public bool wobble;

        [Header("Legs: one model, drawn twice and swung in turn")]
        public Mesh legMesh;
        [Tooltip("Distance of each hip from the middle.")]
        public float legSpacing = 0.17f;
        public float hipHeight = 0.42f;
    }
}
