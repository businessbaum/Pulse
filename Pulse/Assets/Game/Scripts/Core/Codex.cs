using System.Collections.Generic;
using UnityEngine;

namespace Pulse
{
    // The Archivist's record of what the player has met: what the strands do together, the Strays
    // and the Echoes. Something is written down the first time it happens in a run, wherever the
    // run ends. The Archivist pays for it in Beats when the player comes to look at the entry.
    //
    // An entry can have several steps, each paid by itself: a species is one step, its mini boss a
    // second, its boss a third. What is known and what has been paid is kept in the profile.
    public static class Codex
    {
        public enum Section { Reactions, Strays, Echoes }

        public const int Sections = 3;
        public static readonly string[] Titles = { "Reactions", "Strays", "Echoes" };
        public static readonly string[] Singular = { "Reaction", "Stray", "Echo" };

        // What the Archivist pays for each kind of step.
        public const int ForReaction = 30, ForSpecies = 10, ForMiniBoss = 25, ForBoss = 50, ForWarden = 150, ForEcho = 15;

        public struct Step
        {
            public string key, label;
            public int reward;
        }

        public sealed class Entry
        {
            public Section section;
            public string name, text;
            // What to show before it is known: where to look for it.
            public string hint;
            public Step[] steps;
            // For the picture on its tile: the two states of a reaction, the strand of an Echo, the
            // colour of a Stray's biome.
            public StateType first, second;
            public EchoDef echo;
            public EnemyDef stray;

            public bool Known => Profile.Knows(steps[0].key);

            // What the Archivist still owes for it.
            public int Owed
            {
                get
                {
                    int owed = 0;
                    foreach (var step in steps)
                        if (Profile.Knows(step.key) && !Profile.IsPaid(step.key)) owed += step.reward;
                    return owed;
                }
            }
        }

        // The reactions that exist. The other pairs of states only cancel each other for now.
        public static readonly ReactionType[] Built =
        {
            ReactionType.Firestorm, ReactionType.Flash, ReactionType.Plasma,
            ReactionType.ThermalShock, ReactionType.Superconductor, ReactionType.BlackIce,
        };

        static readonly string[] ReactionTexts =
        {
            "Burning ground that spreads to every pool of oil it touches.",
            "One hard blast, at once.",
            "Arcs jump to whatever stands near and ignore armor.",
            "Cracks whatever it hits: for a while it takes more damage from everything.",
            "Lightning runs from one enemy to the next and loses nothing on the way.",
            "They lose their footing, slide apart and are hurt more the closer they stood.",
        };

        public static string Key(ReactionType reaction) => "reaction/" + reaction;
        public static string Key(EchoDef echo) => "echo/" + echo.name;
        public static string Key(EnemyDef stray, Rank rank = Rank.Normal) =>
            "stray/" + stray.name + (rank == Rank.MiniBoss ? "/mini" : rank == Rank.Boss ? "/boss" : "");

        // The two states a reaction is made of.
        public static void States(ReactionType reaction, out StateType first, out StateType second)
        {
            first = second = StateType.None;
            for (var a = StateType.Heat; a <= StateType.Magnet; a++)
                for (var b = a + 1; b <= StateType.Magnet; b++)
                    if (Reactions.Of(a, b) == reaction) { first = a; second = b; }
        }

        // A name with spaces: ThermalShock is shown as "Thermal Shock".
        public static string Name(ReactionType reaction) => reaction switch
        {
            ReactionType.ThermalShock => "Thermal Shock",
            ReactionType.BlackIce => "Black Ice",
            ReactionType.ShardBurst => "Shard Burst",
            ReactionType.TarBall => "Tar Ball",
            _ => reaction.ToString(),
        };

        // ---- Writing things down ----

        // A reaction went off.
        public static void Saw(ReactionType reaction) => Profile.Note(Key(reaction));

        // A Stray was destroyed. A mini boss or a boss also counts for its species.
        public static void Destroyed(EnemyDef stray, Rank rank)
        {
            if (stray == null) return;
            Profile.Note(Key(stray));
            if (rank == Rank.MiniBoss || rank == Rank.Boss) Profile.Note(Key(stray, rank));
        }

        public static void Held(EchoDef echo)
        {
            if (echo != null) Profile.Note(Key(echo));
        }

        // ---- Reading ----

        public static void All(GameConfig config, Section section, List<Entry> into)
        {
            into.Clear();
            switch (section)
            {
                case Section.Reactions:
                    for (int i = 0; i < Built.Length; i++)
                    {
                        States(Built[i], out var first, out var second);
                        into.Add(new Entry
                        {
                            section = section, name = Name(Built[i]), text = ReactionTexts[i], first = first, second = second,
                            hint = "Two strands on one target.",
                            steps = new[] { new Step { key = Key(Built[i]), label = "Set off", reward = ForReaction } },
                        });
                    }
                    break;

                case Section.Strays:
                    foreach (var stray in Strays(config))
                    {
                        bool warden = stray.behaviour == Behaviour.Warden;
                        var steps = new List<Step> { new Step { key = Key(stray), label = "Destroyed", reward = warden ? ForWarden : ForSpecies } };
                        if (!string.IsNullOrEmpty(stray.miniBossName))
                            steps.Add(new Step { key = Key(stray, Rank.MiniBoss), label = stray.miniBossName, reward = ForMiniBoss });
                        if (!string.IsNullOrEmpty(stray.bossName))
                            steps.Add(new Step { key = Key(stray, Rank.Boss), label = stray.bossName, reward = ForBoss });
                        into.Add(new Entry
                        {
                            section = section, name = stray.displayName, text = stray.trait, stray = stray, steps = steps.ToArray(),
                            first = Home(config, stray), hint = warden ? "A Warden." : "It is out there.",
                        });
                    }
                    break;

                default:
                    if (config.echoes == null) break;
                    foreach (var echo in config.echoes)
                    {
                        if (echo == null) continue;
                        into.Add(new Entry
                        {
                            section = section, name = echo.displayName, text = echo.text, echo = echo, first = echo.strand,
                            hint = echo.joinsAfter != null && !Profile.HasCleared(echo.joinsAfter)
                                ? $"Joins the others once the {echo.joinsAfter.displayName} is cleared."
                                : "Dungeons and bosses give Echoes up.",
                            steps = new[] { new Step { key = Key(echo), label = "Carried", reward = ForEcho } },
                        });
                    }
                    break;
            }
        }

        // Every Stray there is: the species of each biome in turn, what dungeons send in waves, and the Wardens.
        public static List<EnemyDef> Strays(GameConfig config)
        {
            var all = new List<EnemyDef>();
            void Add(EnemyDef stray)
            {
                if (stray != null && !all.Contains(stray)) all.Add(stray);
            }
            if (config.scenery != null)
                foreach (var biome in config.scenery.biomes)
                    if (biome.species != null)
                        foreach (var stray in biome.species) Add(stray);
            Add(config.grunt);
            Add(config.brute);
            if (config.world != null)
                foreach (var entrance in config.world.entrances)
                    if (entrance.dungeon != null) Add(entrance.dungeon.boss);
            return all;
        }

        // The strand of the biome a species lives in, for the colour of its picture.
        static StateType Home(GameConfig config, EnemyDef stray)
        {
            if (config.scenery != null)
                foreach (var biome in config.scenery.biomes)
                    if (biome.species != null && System.Array.IndexOf(biome.species, stray) >= 0) return biome.hazard;
            return stray.behaviour == Behaviour.Warden ? StateType.Heat : StateType.None;
        }

        // How many entries of a section are known, how many there are, and what is still owed for them.
        public static int Known(GameConfig config, Section section, out int total, out int owed)
        {
            var entries = new List<Entry>();
            All(config, section, entries);
            total = entries.Count;
            owed = 0;
            int known = 0;
            foreach (var entry in entries)
            {
                if (entry.Known) known++;
                owed += entry.Owed;
            }
            return known;
        }

        // Everything the Archivist still owes, over all sections.
        public static int Owed(GameConfig config)
        {
            int sum = 0;
            for (int s = 0; s < Sections; s++)
            {
                Known(config, (Section)s, out _, out int owed);
                sum += owed;
            }
            return sum;
        }

        // Pays out every step of an entry that is known and not paid yet. Returns what was paid.
        public static int Claim(Entry entry)
        {
            int paid = 0;
            foreach (var step in entry.steps)
                if (Profile.Pay(step.key, step.reward)) paid += step.reward;
            return paid;
        }
    }
}
