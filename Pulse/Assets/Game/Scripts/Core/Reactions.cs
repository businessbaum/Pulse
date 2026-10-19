namespace Pulse
{
    // The reaction matrix from machine-concept.md, section 3.1.
    public static class Reactions
    {
        public static ReactionType Of(StateType a, StateType b)
        {
            if (a == b || a == StateType.None || b == StateType.None) return ReactionType.None;
            if (a > b) (a, b) = (b, a);
            return (a, b) switch
            {
                (StateType.Heat, StateType.Shock) => ReactionType.Plasma,
                (StateType.Heat, StateType.Cryo) => ReactionType.ThermalShock,
                (StateType.Heat, StateType.Oil) => ReactionType.Firestorm,
                (StateType.Heat, StateType.Magnet) => ReactionType.Slag,
                (StateType.Shock, StateType.Cryo) => ReactionType.Superconductor,
                (StateType.Shock, StateType.Oil) => ReactionType.Flash,
                (StateType.Shock, StateType.Magnet) => ReactionType.Coil,
                (StateType.Cryo, StateType.Oil) => ReactionType.BlackIce,
                (StateType.Cryo, StateType.Magnet) => ReactionType.ShardBurst,
                (StateType.Oil, StateType.Magnet) => ReactionType.TarBall,
                _ => ReactionType.None,
            };
        }
    }
}
