using UnityEngine;

namespace Pulse
{
    // What an ability changes. The value is a percentage for Damage, FireRate, Range, StateChance,
    // WiredDamage and WiredRange; hull for Hull; armor for Armor; hull a second for Mending; and
    // damage for Thorns.
    public enum PerkStat { Damage, FireRate, Range, StateChance, Hull, Armor, Mending, Thorns, WiredDamage, WiredRange }

    // An ability a part can carry when an enemy drops it. One ability comes in five strengths, one
    // for each rarity from Common to Legendary.
    [CreateAssetMenu(menuName = "Pulse/Ability")]
    public sealed class PerkDef : ScriptableObject
    {
        public string displayName;
        [Tooltip("What it does, with {0} where the number goes. For example: {0} % more damage.")]
        public string text;
        [Tooltip("The kind of part that can carry it.")]
        public PartKind fits;
        public PerkStat stat;
        [Tooltip("Only for StateChance: the state it applies.")]
        public StateType state;
        [Tooltip("The number at Common, Uncommon, Rare, Epic and Legendary.")]
        public float[] values = new float[5];

        public float Value(Rarity rarity) =>
            rarity == Rarity.None || values.Length == 0 ? 0f : values[Mathf.Clamp((int)rarity - 1, 0, values.Length - 1)];

        public string Describe(Rarity rarity) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, text, Value(rarity).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
    }
}
