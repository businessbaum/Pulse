using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pulse
{
    // How good the ability of a part is. None: the part has no ability, like the ones every player
    // starts with. Mythic has a name and a place in the order, and nothing rolls it yet.
    public enum Rarity { None, Common, Uncommon, Rare, Epic, Legendary, Mythic }

    // One part the player owns. Two Autocannons are two items: each has its own level, and one that
    // was dropped by an enemy carries an ability of some rarity. What it is made of is its module.
    [Serializable]
    public sealed class Item
    {
        public int id;
        // The names of its module and of its ability, as their assets are called.
        public string part, perk;
        public int level = 1;
        public Rarity rarity;
        // Not looked at yet where parts are tuned.
        public bool fresh;
        [NonSerialized] public ModuleDef def;
        [NonSerialized] public PerkDef ability;

        public string Name => def != null ? def.displayName : part;
    }

    // What an item makes of its module: the module's own numbers, changed by level and ability.
    public struct ModuleStats
    {
        // Multipliers for the part itself. 1 changes nothing.
        public float damage, rate, range;
        // What an armor module gives, in all.
        public float hull, armor, mending, thorns;
        // A weapon without a state of its own applies this one with this chance, per hit.
        public StateType state;
        public float stateChance;
        // Multipliers a system gives to everything it is wired to.
        public float wiredDamage, wiredRange;
    }

    // The rules of levels, abilities and drops. No scene objects involved.
    public static class Tuning
    {
        public const int MaxLevel = 5;
        // What each level above the first adds: to a weapon's damage, to everything an armor module
        // gives, to the damage of what a system is wired to, and to what a booster adds.
        const float WeaponStep = 0.12f, ArmorStep = 0.15f, SystemStep = 0.04f, BoostStep = 0.15f;
        // What the step to level 2, 3, 4 and 5 costs, in Beats.
        static readonly int[] Costs = { 40, 90, 160, 260 };
        // How likely each rarity is, from Common to Legendary, by who dropped the part.
        static readonly float[] FromNormal = { 60f, 27f, 10f, 2.5f, 0.5f }, FromMiniBoss = { 34f, 38f, 21f, 6f, 1f };
        static readonly float[] FromBoss = { 8f, 34f, 36f, 17f, 5f }, FromWarden = { 0f, 14f, 40f, 34f, 12f };
        static readonly string[] Names = { "", "Common", "Uncommon", "Rare", "Epic", "Legendary", "Mythic" };

        public static string Name(Rarity rarity) => Names[(int)rarity];

        public static ModuleStats Of(ModuleDef module, Item item) => At(module, item, item != null ? item.level : 1);

        // The same, as it would be at another level.
        public static ModuleStats At(ModuleDef module, Item item, int level)
        {
            var stats = new ModuleStats { damage = 1f, rate = 1f, range = 1f, wiredDamage = 1f, wiredRange = 1f };
            int steps = Mathf.Max(0, level - 1);
            switch (module)
            {
                case WeaponDef _:
                    stats.damage += WeaponStep * steps;
                    break;
                case SupportDef plate:
                    float grown = 1f + ArmorStep * steps;
                    stats.armor = plate.armor * grown;
                    stats.hull = plate.hull * grown;
                    stats.damage = grown;
                    break;
                case SystemDef system:
                    stats.wiredDamage = 1f + system.damageBonus + SystemStep * steps;
                    stats.wiredRange = 1f + system.rangeBonus * (1f + BoostStep * steps);
                    break;
            }
            if (item == null || item.ability == null) return stats;

            float value = item.ability.Value(item.rarity);
            switch (item.ability.stat)
            {
                case PerkStat.Damage: stats.damage *= 1f + value / 100f; break;
                case PerkStat.FireRate: stats.rate *= 1f + value / 100f; break;
                case PerkStat.Range: stats.range *= 1f + value / 100f; break;
                case PerkStat.StateChance: stats.state = item.ability.state; stats.stateChance = value / 100f; break;
                case PerkStat.Hull: stats.hull += value; break;
                case PerkStat.Armor: stats.armor += value; break;
                case PerkStat.Mending: stats.mending += value; break;
                case PerkStat.Thorns: stats.thorns += value; break;
                case PerkStat.WiredDamage: stats.wiredDamage *= 1f + value / 100f; break;
                case PerkStat.WiredRange: stats.wiredRange *= 1f + value / 100f; break;
            }
            return stats;
        }

        // What the next level costs. Zero: it is at the top.
        public static int UpgradeCost(Item item) => item.level >= MaxLevel ? 0 : Costs[Mathf.Clamp(item.level - 1, 0, Costs.Length - 1)];

        // What scrapping it pays: more for a higher level and a better ability.
        public static int ScrapValue(Item item) => 4 + 6 * item.level + 9 * (int)item.rarity;

        // roll is a random number from 0 to 1.
        public static Rarity RollRarity(Rank rank, float roll)
        {
            float[] weights = rank == Rank.Warden ? FromWarden : rank == Rank.Boss ? FromBoss : rank == Rank.MiniBoss ? FromMiniBoss : FromNormal;
            float total = 0f;
            foreach (float weight in weights) total += weight;
            float at = roll * total;
            for (int i = 0; i < weights.Length; i++)
            {
                at -= weights[i];
                if (at < 0f) return Rarity.Common + i;
            }
            return Rarity.Legendary;
        }

        // Whether a part can carry an ability: it must be of the right kind, and a weapon that has a
        // state of its own gains nothing from applying another.
        public static bool Suits(PerkDef perk, ModuleDef module) => perk != null && module != null && Parts.KindOf(module) == perk.fits
            && (perk.stat != PerkStat.StateChance || module.OutputState == StateType.None);

        // Makes the part an enemy of that rank leaves behind: any module of the game, with an ability
        // that suits it. It is not the player's yet. roll returns random numbers from 0 to 1.
        public static Item Roll(GameConfig config, Rank rank, Func<float> roll)
        {
            if (config.modules == null || config.modules.Length == 0) return null;
            var module = config.modules[Mathf.Min((int)(roll() * config.modules.Length), config.modules.Length - 1)];
            var item = new Item { part = module.name, def = module, level = 1, fresh = true };
            var suited = new List<PerkDef>();
            if (config.perks != null)
                foreach (var perk in config.perks)
                    if (Suits(perk, module)) suited.Add(perk);
            if (suited.Count == 0) return item;
            item.ability = suited[Mathf.Min((int)(roll() * suited.Count), suited.Count - 1)];
            item.perk = item.ability.name;
            item.rarity = RollRarity(rank, roll());
            return item;
        }

        // The numbers of an item at a level, for its data sheet. The share is how far along the
        // levels the number is, so that the bars grow as the item is upgraded.
        public static void Sheet(Item item, int level, List<PartStat> into)
        {
            into.Clear();
            var stats = At(item.def, item, level);
            float share = level / (float)MaxLevel;
            switch (item.def)
            {
                case WeaponDef weapon:
                    Line(into, "Damage", weapon.damage > 0f ? Number(weapon.damage * stats.damage) : "none", share);
                    Line(into, "Shots a second", Number(stats.rate / Mathf.Max(0.01f, weapon.cooldown)), -1f);
                    Line(into, "Range", Number(weapon.range * stats.range) + " m", -1f);
                    break;
                case SupportDef plate:
                    if (stats.armor > 0f) Line(into, "Armor", Number(stats.armor), share);
                    if (stats.hull > 0f) Line(into, "Hull", "+" + Number(stats.hull), share);
                    if (plate.trigger != TriggerType.None) Line(into, "Burst damage", Number(plate.triggerDamage * stats.damage), share);
                    break;
                case SystemDef _:
                    if (stats.wiredRange > 1.001f) Line(into, "Range of what it is wired to", "+" + Number((stats.wiredRange - 1f) * 100f) + " %", share);
                    Line(into, "Damage of what it is wired to", "+" + Number((stats.wiredDamage - 1f) * 100f) + " %", share);
                    break;
            }
        }

        static void Line(List<PartStat> into, string label, string value, float share) =>
            into.Add(new PartStat { label = label, value = value, share = share });

        static string Number(float value) => value.ToString(Mathf.Abs(value - Mathf.Round(value)) < 0.05f ? "0" : "0.0",
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
