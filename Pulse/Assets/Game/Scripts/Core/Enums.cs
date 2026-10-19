using System;

namespace Pulse
{
    // None means Kinetic: plain damage, no state.
    public enum StateType { None, Heat, Shock, Cryo, Oil, Magnet }

    public enum Delivery { Projectile, Beam, Area, Deployable, Contact }

    public enum ReactionType
    {
        None, Plasma, ThermalShock, Firestorm, Slag, Superconductor,
        Flash, Coil, BlackIce, ShardBurst, TarBall
    }

    // Hardpoint: a weapon. System: a module that changes what it is wired to. Armor: a plate.
    // Socket: takes a system or an armor module; a system in it acts on what the socket is wired to.
    // (System slots were called bays and armor slots support slots until the eighth round.)
    public enum SlotType { Hardpoint, System, Armor, Drive, Socket }

    public enum Mount { Front, Turret, Flank, Rear }

    // Shaper: changes the shape of a hit. Feed: gives a Kinetic hit a state. Booster: only adds
    // to the numbers of what it is wired to, such as range.
    public enum SystemKind { Shaper, Feed, Relay, Booster }

    [Flags]
    public enum Shapers { None = 0, Splitter = 1, Capacitor = 2, Gyro = 4 }

    public enum TriggerType { None, BurstWhenHit }

    // How a species fights. Its mini boss and boss forms build on the same idea.
    public enum Behaviour
    {
        Walker,      // walks up and hits
        Swarm,       // weak, comes in packs
        Charger,     // winds up, then rams in a straight line
        Spitter,     // keeps its distance and lobs burning globs
        Skirmisher,  // circles, darts in, backs off
        Slammer,     // slow and tough, pounds the ground around it
        Lancer,      // stands off and shoots along a line it shows first
        Leech,       // fast, slows the machine when it bites
        Bloater,     // bursts next to the machine and leaves an oil pool
        Splitter,    // falls apart into smaller ones when it dies
        Warden,      // one of the five great machines: a fight of its own, in two phases
    }

    // Lesser: what a Splitter falls apart into. Every species can appear as a mini boss or a boss.
    // Warden: one of the great machines. It is what its asset says; no rank changes its numbers.
    public enum Rank { Lesser, Normal, MiniBoss, Boss, Warden }
}
