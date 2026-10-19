using UnityEngine;

namespace Pulse
{
    // Which rule an Echo bends. The rule itself is checked where it applies; see Echoes.
    public enum EchoKind
    {
        SecondBeat,   // every reaction goes off a second time, half as strong
        Undertow,     // drops fly to the machine from a distance
        Crescendo,    // every kill adds damage, until the machine loses hull
        Afterburn,    // Nitro refills twice as fast and leaves fire behind
        DoubleTime,   // every weapon fires faster
        Encore,       // once, a blow that would destroy the machine does not
        HeavyPurse,   // Beats count double, and no hearts drop
        LongWire,     // every system reaches every weapon
        Ember,        // a burning enemy that dies sets its neighbours alight
        Afterimage,   // every tenth hit chains lightning
        DeepCold,     // chill lasts twice as long and slows harder
        Seep,         // the machine's oil lies three times as long
    }

    // An Echo: a fragment of the Pulse that bends one rule for the rest of a run (lore.md, section 5).
    // It needs no slot and no Power. A run holds at most three.
    [CreateAssetMenu(menuName = "Pulse/Echo")]
    public sealed class EchoDef : ScriptableObject
    {
        public string displayName;
        [Tooltip("One line for the player: the rule it bends.")]
        public string text;
        public EchoKind kind;
        [Tooltip("The strand it is a fragment of. None: it belongs to no strand.")]
        public StateType strand;
        [Tooltip("It joins the pool once this dungeon has been cleared for the first time. Empty: it is there from the first run.")]
        public DungeonDef joinsAfter;
    }
}
