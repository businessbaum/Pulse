using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pulse
{
    public enum PartKind { Weapon, System, Armor, Drive, Reactor, Frame }

    // One line of a part's data sheet: what it measures, the value as text, and how that compares
    // with the best part of its kind, from 0 to 1. A share below zero means there is nothing to compare.
    public struct PartStat
    {
        public string label, value;
        public float share;
    }

    // Everything a machine can be made of, looked at as one catalogue: weapons, systems and support
    // modules, and the drive, reactor and frame they sit on. The screens of the lobby's machines
    // both read parts through this, so they describe them in the same words.
    public static class Parts
    {
        public const int Kinds = 6;
        public static readonly string[] Plural = { "Weapons", "Systems", "Armor", "Drives", "Reactors", "Frames" };
        public static readonly string[] Singular = { "Weapon", "System", "Armor", "Drive", "Reactor", "Frame" };

        static readonly List<ScriptableObject> buffer = new List<ScriptableObject>();

        public static PartKind KindOf(UnityEngine.Object part) => part switch
        {
            WeaponDef _ => PartKind.Weapon,
            SystemDef _ => PartKind.System,
            SupportDef _ => PartKind.Armor,
            DriveDef _ => PartKind.Drive,
            ReactorDef _ => PartKind.Reactor,
            _ => PartKind.Frame,
        };

        public static string Name(UnityEngine.Object part) => part switch
        {
            ModuleDef module => module.displayName,
            ReactorDef reactor => reactor.displayName,
            DriveDef drive => drive.displayName,
            FrameDef frame => frame.displayName,
            null => "",
            _ => part.name,
        };

        public static string Description(UnityEngine.Object part) => part switch
        {
            ModuleDef module => module.description,
            ReactorDef reactor => reactor.description,
            DriveDef drive => drive.description,
            FrameDef frame => frame.description,
            _ => "",
        };

        // Every part of one kind the game has, found or not, in the order of the catalogue.
        public static void All(GameConfig config, PartKind kind, List<ScriptableObject> into)
        {
            into.Clear();
            switch (kind)
            {
                case PartKind.Drive: Take(config.drives, into); break;
                case PartKind.Reactor: Take(config.reactors, into); break;
                case PartKind.Frame: Take(config.frames, into); break;
                default:
                    if (config.modules == null) break;
                    foreach (var module in config.modules)
                        if (module != null && KindOf(module) == kind) into.Add(module);
                    break;
            }
        }

        static void Take<T>(T[] from, List<ScriptableObject> into) where T : ScriptableObject
        {
            if (from == null) return;
            foreach (var one in from)
                if (one != null) into.Add(one);
        }

        // How many parts of a kind the player has found, and how many there are.
        public static int Found(GameConfig config, PartKind kind, out int total, out int fresh)
        {
            All(config, kind, buffer);
            total = buffer.Count;
            int found = fresh = 0;
            foreach (var part in buffer)
            {
                if (Profile.IsFound(part)) found++;
                if (Profile.IsFresh(part)) fresh++;
            }
            return found;
        }

        // The dungeon that keeps a part, or null when none does.
        public static DungeonDef KeptIn(GameConfig config, UnityEngine.Object part)
        {
            if (config.world == null) return null;
            foreach (var entrance in config.world.entrances)
            {
                var dungeon = entrance.dungeon;
                if (dungeon == null || dungeon.finds == null) continue;
                foreach (var find in dungeon.finds)
                    if (find == part) return dungeon;
            }
            return null;
        }

        // The data sheet of a part: at most six lines.
        public static void Stats(GameConfig config, ScriptableObject part, List<PartStat> into)
        {
            into.Clear();
            switch (part)
            {
                case WeaponDef weapon:
                    Line(into, "Damage", weapon.damage > 0f ? Number(weapon.damage) : "none", weapon.damage, Most(config, part, p => ((WeaponDef)p).damage));
                    Line(into, "Shots a second", Number(1f / Mathf.Max(0.01f, weapon.cooldown)), 1f / Mathf.Max(0.01f, weapon.cooldown),
                        Most(config, part, p => 1f / Mathf.Max(0.01f, ((WeaponDef)p).cooldown)));
                    Line(into, "Range", Number(weapon.range) + " m", weapon.range, Most(config, part, p => ((WeaponDef)p).range));
                    if (weapon.delivery == Delivery.Area) Line(into, "Area", Number(weapon.radius) + " m across", weapon.radius, Most(config, part, p => Reach((WeaponDef)p)));
                    if (weapon.delivery == Delivery.Beam) Line(into, "Cone", Number(weapon.coneHalfAngle * 2f) + " degrees");
                    if (weapon.delivery == Delivery.Deployable) Line(into, "Blast", Number(weapon.radius) + " m across");
                    if (weapon.pierce > 0) Line(into, "Goes through", weapon.pierce + " enemies");
                    Line(into, "Power", weapon.powerCost.ToString());
                    break;

                case SystemDef system:
                    if (system.rangeBonus > 0f) Line(into, "Range of what it is wired to", "+" + Number(system.rangeBonus * 100f) + " %");
                    if (system.damageBonus > 0f) Line(into, "Damage of what it is wired to", "+" + Number(system.damageBonus * 100f) + " %");
                    Line(into, "Power", system.powerCost.ToString());
                    break;

                case SupportDef support:
                    if (support.armor > 0f) Line(into, "Armor", Number(support.armor) + " off every hit", support.armor, Most(config, part, p => ((SupportDef)p).armor));
                    if (support.hull > 0f) Line(into, "Extra hull", "+" + Number(support.hull), support.hull, Most(config, part, p => ((SupportDef)p).hull));
                    if (support.trigger != TriggerType.None)
                    {
                        Line(into, "Burst damage", Number(support.triggerDamage), support.triggerDamage, Most(config, part, p => ((SupportDef)p).triggerDamage));
                        Line(into, "Burst reach", Number(support.triggerRadius) + " m");
                        Line(into, "Ready again after", Number(support.triggerCooldown) + " s");
                    }
                    Line(into, "Power", support.powerCost.ToString());
                    break;

                case DriveDef drive:
                    Line(into, "Top speed", Number(drive.speed) + " m/s", drive.speed, Most(config, part, p => ((DriveDef)p).speed));
                    Line(into, "Acceleration", Number(drive.acceleration), drive.acceleration, Most(config, part, p => ((DriveDef)p).acceleration));
                    Line(into, "Turning", Number(drive.turnRate) + " degrees a second", drive.turnRate, Most(config, part, p => ((DriveDef)p).turnRate));
                    Line(into, "Nitro", "Speed x " + Number(drive.nitroBoost));
                    Line(into, "A full tank lasts", Number(drive.nitroSeconds) + " s");
                    Line(into, "An empty tank refills in", Number(drive.nitroRecharge) + " s");
                    break;

                case ReactorDef reactor:
                    Line(into, "Power", reactor.powerCap.ToString(), reactor.powerCap, Most(config, part, p => ((ReactorDef)p).powerCap));
                    break;

                case FrameDef frame:
                    int mark = Profile.MarkOf(frame);
                    Line(into, "Hull", Number(frame.HullAt(mark)), frame.HullAt(mark), Most(config, part, p => ((FrameDef)p).HullAt(99)));
                    Line(into, "Weapons", frame.Count(SlotType.Hardpoint).ToString(), frame.Count(SlotType.Hardpoint),
                        Most(config, part, p => ((FrameDef)p).Count(SlotType.Hardpoint)));
                    if (frame.Count(SlotType.Socket) > 0) Line(into, "Sockets", frame.Count(SlotType.Socket).ToString());
                    if (frame.Count(SlotType.System) > 0) Line(into, "Systems", frame.Count(SlotType.System).ToString());
                    if (frame.Count(SlotType.Armor) > 0) Line(into, "Armor slots", frame.Count(SlotType.Armor).ToString());
                    break;
            }
        }

        static float Reach(WeaponDef weapon) => weapon.delivery == Delivery.Area ? weapon.radius : 0f;

        static void Line(List<PartStat> into, string label, string value, float amount = -1f, float most = 1f) =>
            into.Add(new PartStat { label = label, value = value, share = amount < 0f ? -1f : Mathf.Clamp01(amount / Mathf.Max(0.0001f, most)) });

        // The largest value any part of the same kind has.
        static float Most(GameConfig config, ScriptableObject like, Func<ScriptableObject, float> of)
        {
            All(config, KindOf(like), buffer);
            float most = 0f;
            foreach (var part in buffer) most = Mathf.Max(most, of(part));
            return most;
        }

        static string Number(float value) => value.ToString(Mathf.Abs(value - Mathf.Round(value)) < 0.05f ? "0" : "0.0",
            System.Globalization.CultureInfo.InvariantCulture);

        // What a slot is called on screen. A weapon slot goes by where it sits: FRONT, TURRET, REAR.
        // The others go by their kind, with a number when the frame has several of that kind.
        public static string SlotName(FrameDef frame, SlotDef slot)
        {
            if (slot.type == SlotType.Hardpoint) return slot.id.ToUpperInvariant();
            string kind = slot.type switch
            {
                SlotType.System => "SYSTEM",
                SlotType.Armor => "ARMOR",
                SlotType.Socket => "SOCKET",
                _ => "DRIVE",
            };
            if (frame.Count(slot.type) < 2) return kind;
            int number = 0;
            foreach (var other in frame.slots)
            {
                if (other.type == slot.type) number++;
                if (other.id == slot.id) break;
            }
            return kind + " " + number;
        }

        // Whether a module can go into a slot of that type. A socket takes a system or an armor module.
        public static bool Fits(ModuleDef module, SlotType slot) => module != null
            && (module.Slot == slot || (slot == SlotType.Socket && (module.Slot == SlotType.System || module.Slot == SlotType.Armor)));

        // What a frame holds, in a few words: "1 weapon, 1 socket".
        public static string Layout(FrameDef frame)
        {
            var text = new System.Text.StringBuilder();
            void Add(SlotType type, string one, string many)
            {
                int count = frame.Count(type);
                if (count == 0) return;
                if (text.Length > 0) text.Append(", ");
                text.Append(count).Append(' ').Append(count == 1 ? one : many);
            }
            Add(SlotType.Hardpoint, "weapon", "weapons");
            Add(SlotType.Socket, "socket", "sockets");
            Add(SlotType.System, "system", "systems");
            Add(SlotType.Armor, "armor", "armor");
            return text.ToString();
        }

        // Mark I, Mark II, Mark III.
        public static string MarkName(int mark) => "Mk " + new string('I', mark + 1);
    }
}
