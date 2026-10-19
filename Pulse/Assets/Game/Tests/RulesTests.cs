using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Pulse.Tests
{
    public class ReactionTests
    {
        static readonly StateType[] States =
            { StateType.Heat, StateType.Shock, StateType.Cryo, StateType.Oil, StateType.Magnet };

        [Test]
        public void EveryPairOfDifferentStatesHasItsOwnReaction()
        {
            var seen = new HashSet<ReactionType>();
            for (int a = 0; a < States.Length; a++)
            for (int b = a + 1; b < States.Length; b++)
            {
                var reaction = Reactions.Of(States[a], States[b]);
                Assert.AreNotEqual(ReactionType.None, reaction, $"{States[a]} + {States[b]}");
                Assert.IsTrue(seen.Add(reaction), $"{reaction} appears twice");
            }
            Assert.AreEqual(10, seen.Count);
        }

        [Test]
        public void OrderDoesNotMatter()
        {
            Assert.AreEqual(ReactionType.Firestorm, Reactions.Of(StateType.Heat, StateType.Oil));
            Assert.AreEqual(ReactionType.Firestorm, Reactions.Of(StateType.Oil, StateType.Heat));
            Assert.AreEqual(ReactionType.Flash, Reactions.Of(StateType.Oil, StateType.Shock));
            Assert.AreEqual(ReactionType.Plasma, Reactions.Of(StateType.Shock, StateType.Heat));
        }

        [Test]
        public void SameStateOrKineticDoesNotReact()
        {
            Assert.AreEqual(ReactionType.None, Reactions.Of(StateType.Heat, StateType.Heat));
            Assert.AreEqual(ReactionType.None, Reactions.Of(StateType.None, StateType.Oil));
            Assert.AreEqual(ReactionType.None, Reactions.Of(StateType.Shock, StateType.None));
        }
    }

    public class RunStructureTests
    {
        static WorldDef ChainOfThree()
        {
            var world = ScriptableObject.CreateInstance<WorldDef>();
            world.entrances = new[]
            {
                new WorldDef.Entrance { requires = -1 },
                new WorldDef.Entrance { requires = 0 },
                new WorldDef.Entrance { requires = 1 },
            };
            return world;
        }

        [Test]
        public void OnlyTheFirstEntranceIsOpenAtTheStart()
        {
            var world = ChainOfThree();
            var cleared = new HashSet<int>();
            Assert.IsTrue(WorldRules.IsOpen(world, 0, cleared));
            Assert.IsFalse(WorldRules.IsOpen(world, 1, cleared));
            Assert.IsFalse(WorldRules.IsOpen(world, 2, cleared));
        }

        [Test]
        public void ClearingAnEntranceClosesItAndOpensTheNext()
        {
            var world = ChainOfThree();
            var cleared = new HashSet<int> { 0 };
            Assert.IsFalse(WorldRules.IsOpen(world, 0, cleared));
            Assert.IsTrue(WorldRules.IsOpen(world, 1, cleared));
            Assert.IsFalse(WorldRules.IsOpen(world, 2, cleared));
        }

        [Test]
        public void WavesRunInOrderAndThenTheDungeonIsCleared()
        {
            var dungeon = ScriptableObject.CreateInstance<DungeonDef>();
            dungeon.waves = new[]
            {
                new DungeonDef.Wave { grunts = 3 },
                new DungeonDef.Wave { grunts = 5, brutes = 1 },
            };
            var run = new WaveRun(dungeon);
            Assert.AreEqual(-1, run.Wave);

            Assert.IsTrue(run.Next(out var first));
            Assert.AreEqual(3, first.grunts);
            Assert.IsTrue(run.Next(out var second));
            Assert.AreEqual(1, second.brutes);
            Assert.IsFalse(run.Cleared);

            Assert.IsFalse(run.Next(out _));
            Assert.IsTrue(run.Cleared);
            Assert.IsFalse(run.Next(out _), "asking again after the end changes nothing");
        }

        [Test]
        public void BossesAreRarerThanMiniBossesAndMostSpawnsAreOrdinary()
        {
            var world = ScriptableObject.CreateInstance<WorldDef>();
            Assert.Less(world.bossChance, world.miniBossChance);
            Assert.AreEqual(Rank.Boss, WorldRules.RollRank(world, 0f, 0, 0));
            Assert.AreEqual(Rank.MiniBoss, WorldRules.RollRank(world, world.bossChance + world.miniBossChance * 0.5f, 0, 0));
            Assert.AreEqual(Rank.Normal, WorldRules.RollRank(world, 0.5f, 0, 0));
        }

        [Test]
        public void NoMoreBossesAppearWhileEnoughAreAlive()
        {
            var world = ScriptableObject.CreateInstance<WorldDef>();
            Assert.AreEqual(Rank.Normal, WorldRules.RollRank(world, 0f, 0, world.maxBosses));
            Assert.AreEqual(Rank.Normal, WorldRules.RollRank(world, world.bossChance + 0.001f, world.maxMiniBosses, 0));
        }

        [Test]
        public void EveryRankIsTougherThanTheOneBelowAndCarriesItsOwnName()
        {
            var mite = ScriptableObject.CreateInstance<EnemyDef>();
            mite.displayName = "Cinder Mite";
            mite.miniBossName = "Mite Matron";
            mite.bossName = "Brood Mother";
            mite.hp = 9f;

            Assert.Less(Ranks.Hull(mite, Rank.Lesser), Ranks.Hull(mite, Rank.Normal));
            Assert.Less(Ranks.Hull(mite, Rank.Normal), Ranks.Hull(mite, Rank.MiniBoss));
            Assert.Less(Ranks.Hull(mite, Rank.MiniBoss), Ranks.Hull(mite, Rank.Boss));
            Assert.Greater(Ranks.Hull(mite, Rank.Boss), 500f, "even the weakest species makes a real boss");
            Assert.AreEqual("Cinder Mite", Ranks.Name(mite, Rank.Normal));
            Assert.AreEqual("Mite Matron", Ranks.Name(mite, Rank.MiniBoss));
            Assert.AreEqual("Brood Mother", Ranks.Name(mite, Rank.Boss));
        }

        [Test]
        public void TheStormStartsAtTheEdgeOfTheMapAndHurtsMoreTheDeeperIn()
        {
            var world = ScriptableObject.CreateInstance<WorldDef>();
            Assert.AreEqual(0f, WorldRules.StormDepth(world, Vector3.zero));
            Assert.AreEqual(0f, WorldRules.StormDepth(world, new Vector3(world.halfSize, 0f, -world.halfSize)), "the edge itself is safe");
            Assert.AreEqual(0.5f, WorldRules.StormDepth(world, new Vector3(0f, 0f, -world.halfSize - world.stormDepth * 0.5f)), 0.001f);
            Assert.AreEqual(1f, WorldRules.StormDepth(world, new Vector3(world.halfSize + 500f, 0f, 0f)));

            Assert.AreEqual(0f, WorldRules.StormDamage(world, 0f));
            Assert.Greater(WorldRules.StormDamage(world, 0.01f), 0f);
            Assert.Greater(WorldRules.StormDamage(world, 1f), WorldRules.StormDamage(world, 0.5f));
            Assert.Greater(WorldRules.StormDamage(world, 0.5f), WorldRules.StormDamage(world, 0.1f));
        }

        [Test]
        public void OrdinaryEnemiesDropByChanceAndBossesAlwaysDrop()
        {
            var pool = ScriptableObject.CreateInstance<DropPool>();
            pool.entries = new[] { new DropPool.Entry { kind = DropKind.Heart, chance = 0.1f, amount = 12f, fromMiniBoss = 2, fromBoss = 4 } };
            var drops = new List<DropPool.Entry>();

            pool.Roll(Rank.Normal, () => 0.5f, drops);
            Assert.AreEqual(0, drops.Count, "an unlucky roll drops nothing");
            pool.Roll(Rank.Normal, () => 0.05f, drops);
            Assert.AreEqual(1, drops.Count, "a lucky roll drops one");

            drops.Clear();
            pool.Roll(Rank.MiniBoss, () => 0.99f, drops);
            Assert.AreEqual(2, drops.Count);
            drops.Clear();
            pool.Roll(Rank.Boss, () => 0.99f, drops);
            Assert.AreEqual(4, drops.Count);
            Assert.AreEqual(12f, drops[0].amount);
        }
    }

    public class WiringTests
    {
        FrameDef frame;
        WeaponDef kinetic, heated;
        SystemDef splitter, shockFeed;
        SupportDef plating, reactive;

        [SetUp]
        public void SetUp()
        {
            // One bay wired to a hardpoint and a support slot, plus one unwired hardpoint.
            frame = ScriptableObject.CreateInstance<FrameDef>();
            frame.slots = new[]
            {
                new SlotDef { id = "front", type = SlotType.Hardpoint },
                new SlotDef { id = "loose", type = SlotType.Hardpoint },
                new SlotDef { id = "bay", type = SlotType.System },
                new SlotDef { id = "support", type = SlotType.Armor },
            };
            frame.wires = new[]
            {
                new WireDef { system = "bay", slot = "front" },
                new WireDef { system = "bay", slot = "support" },
            };

            kinetic = ScriptableObject.CreateInstance<WeaponDef>();
            heated = ScriptableObject.CreateInstance<WeaponDef>();
            heated.state = StateType.Heat;
            splitter = ScriptableObject.CreateInstance<SystemDef>();
            splitter.kind = SystemKind.Shaper;
            splitter.shaper = Shapers.Splitter;
            shockFeed = ScriptableObject.CreateInstance<SystemDef>();
            shockFeed.kind = SystemKind.Feed;
            shockFeed.feedState = StateType.Shock;
            plating = ScriptableObject.CreateInstance<SupportDef>();
            reactive = ScriptableObject.CreateInstance<SupportDef>();
            reactive.trigger = TriggerType.BurstWhenHit;
        }

        Loadout Build(params (string slot, ModuleDef module)[] modules)
        {
            var loadout = ScriptableObject.CreateInstance<Loadout>();
            loadout.frame = frame;
            loadout.modules = System.Array.ConvertAll(modules, m => new Loadout.Entry { slot = m.slot, module = m.module });
            return loadout;
        }

        [Test]
        public void FeedGivesItsStateToAKineticWeapon()
        {
            var loadout = Build(("front", kinetic), ("bay", shockFeed));
            var mods = Wiring.Resolve(loadout, "front");
            Assert.AreEqual(StateType.Shock, Wiring.EffectiveState(kinetic, mods));
            Assert.IsTrue(Wiring.IsLive(loadout, frame.wires[0]));
        }

        [Test]
        public void FeedDoesNotChangeAWeaponThatHasAState()
        {
            var loadout = Build(("front", heated), ("bay", shockFeed));
            var mods = Wiring.Resolve(loadout, "front");
            Assert.AreEqual(StateType.Heat, Wiring.EffectiveState(heated, mods));
            Assert.IsFalse(Wiring.IsLive(loadout, frame.wires[0]));
        }

        [Test]
        public void ShaperReachesOnlyWiredSlots()
        {
            var loadout = Build(("front", kinetic), ("loose", kinetic), ("bay", splitter));
            Assert.IsTrue(Wiring.Resolve(loadout, "front").Has(Shapers.Splitter));
            Assert.IsFalse(Wiring.Resolve(loadout, "loose").Has(Shapers.Splitter));
        }

        [Test]
        public void SystemsActOnATriggerButNotOnAPlainStatModule()
        {
            var withTrigger = Build(("support", reactive), ("bay", shockFeed));
            Assert.AreEqual(StateType.Shock, Wiring.Resolve(withTrigger, "support").feed);
            Assert.IsTrue(Wiring.IsLive(withTrigger, frame.wires[1]));

            var statOnly = Build(("support", plating), ("bay", shockFeed));
            Assert.AreEqual(StateType.None, Wiring.Resolve(statOnly, "support").feed);
            Assert.IsFalse(Wiring.IsLive(statOnly, frame.wires[1]));
        }

        [Test]
        public void AnEmptyBayChangesNothing()
        {
            var loadout = Build(("front", kinetic));
            Assert.AreEqual(Shapers.None, Wiring.Resolve(loadout, "front").shapers);
            Assert.IsFalse(Wiring.IsLive(loadout, frame.wires[0]));
        }

        [Test]
        public void ABoosterAddsRangeToWhatItIsWiredToAndNothingElse()
        {
            var extender = ScriptableObject.CreateInstance<SystemDef>();
            extender.kind = SystemKind.Booster;
            extender.rangeBonus = 0.4f;
            var loadout = Build(("front", kinetic), ("loose", kinetic), ("bay", extender));
            Assert.AreEqual(1.4f, Wiring.Resolve(loadout, "front").range, 0.001f);
            Assert.AreEqual(1f, Wiring.Resolve(loadout, "front").damage, 0.001f);
            Assert.AreEqual(1f, Wiring.Resolve(loadout, "loose").range, 0.001f, "a slot it is not wired to gains nothing");
            Assert.IsTrue(Wiring.IsLive(loadout, frame.wires[0]));
        }

        [Test]
        public void ASocketTakesASystemOrArmorButNoWeapon()
        {
            Assert.IsTrue(Parts.Fits(splitter, SlotType.Socket));
            Assert.IsTrue(Parts.Fits(plating, SlotType.Socket));
            Assert.IsFalse(Parts.Fits(kinetic, SlotType.Socket));
            Assert.IsFalse(Parts.Fits(splitter, SlotType.Armor));
            Assert.IsFalse(Parts.Fits(plating, SlotType.System));
        }

        [Test]
        public void EveryLevelMakesAPartBetterAndCostsMoreThanTheLast()
        {
            kinetic.damage = 10f;
            plating.armor = 2f;
            var gun = new Item { def = kinetic, level = 1 };
            var plate = new Item { def = plating, level = 1 };
            float damage = Tuning.Of(kinetic, gun).damage, armor = Tuning.Of(plating, plate).armor;
            int cost = 0;
            for (int level = 2; level <= Tuning.MaxLevel; level++)
            {
                Assert.Greater(Tuning.UpgradeCost(gun), cost);
                cost = Tuning.UpgradeCost(gun);
                gun.level = plate.level = level;
                Assert.Greater(Tuning.Of(kinetic, gun).damage, damage);
                Assert.Greater(Tuning.Of(plating, plate).armor, armor);
                damage = Tuning.Of(kinetic, gun).damage;
                armor = Tuning.Of(plating, plate).armor;
            }
            Assert.AreEqual(0, Tuning.UpgradeCost(gun), "the top level has no next step");
            Assert.AreEqual(1f, Tuning.Of(kinetic, null).damage, 0.001f, "a part that is no item is plain");
        }

        [Test]
        public void AnAbilityIsStrongerTheRarerItIs()
        {
            var keen = ScriptableObject.CreateInstance<PerkDef>();
            keen.fits = PartKind.Weapon;
            keen.stat = PerkStat.Damage;
            keen.values = new[] { 10f, 18f, 28f, 40f, 60f };
            var item = new Item { def = kinetic, ability = keen, level = 1 };
            float last = 1f;
            for (var rarity = Rarity.Common; rarity <= Rarity.Legendary; rarity++)
            {
                item.rarity = rarity;
                Assert.Greater(Tuning.Of(kinetic, item).damage, last);
                last = Tuning.Of(kinetic, item).damage;
                Assert.Greater(Tuning.ScrapValue(item), Tuning.ScrapValue(new Item { def = kinetic, level = 1, rarity = rarity - 1 }));
            }
            Assert.AreEqual(1.6f, last, 0.001f);

            // A weapon with a state of its own has no use for an ability that applies one.
            var searing = ScriptableObject.CreateInstance<PerkDef>();
            searing.fits = PartKind.Weapon;
            searing.stat = PerkStat.StateChance;
            searing.state = StateType.Heat;
            Assert.IsTrue(Tuning.Suits(searing, kinetic));
            Assert.IsFalse(Tuning.Suits(searing, heated));
            Assert.IsFalse(Tuning.Suits(keen, plating));
        }

        [Test]
        public void StrongerEnemiesDropBetterPartsAndNothingRollsMythic()
        {
            float Share(Rank rank, Rarity atLeast)
            {
                int hits = 0;
                for (int i = 0; i < 1000; i++)
                {
                    var rarity = Tuning.RollRarity(rank, (i + 0.5f) / 1000f);
                    Assert.Less(rarity, Rarity.Mythic);
                    Assert.Greater(rarity, Rarity.None);
                    if (rarity >= atLeast) hits++;
                }
                return hits / 1000f;
            }
            Assert.Less(Share(Rank.Normal, Rarity.Rare), Share(Rank.MiniBoss, Rarity.Rare));
            Assert.Less(Share(Rank.MiniBoss, Rarity.Rare), Share(Rank.Boss, Rarity.Rare));
            Assert.Less(Share(Rank.Boss, Rarity.Epic), Share(Rank.Warden, Rarity.Epic));
            Assert.Less(Share(Rank.Normal, Rarity.Legendary), 0.02f, "an ordinary enemy almost never drops the best");
        }

        [Test]
        public void PowerUsedIsTheSumOfModuleCosts()
        {
            kinetic.powerCost = 2;
            splitter.powerCost = 1;
            Assert.AreEqual(3, Build(("front", kinetic), ("bay", splitter)).PowerUsed);
        }
    }

    public class EchoTests
    {
        static EchoDef Echo(EchoKind kind)
        {
            var echo = ScriptableObject.CreateInstance<EchoDef>();
            echo.name = echo.displayName = kind.ToString();
            echo.kind = kind;
            return echo;
        }

        [SetUp]
        public void Begin() => Echoes.Reset();

        [TearDown]
        public void End() => Echoes.Reset();

        [Test]
        public void ARunHoldsAtMostThreeAndNoneTwice()
        {
            var first = Echo(EchoKind.SecondBeat);
            Assert.IsTrue(Echoes.Take(first));
            Assert.IsFalse(Echoes.Take(first));
            Assert.IsTrue(Echoes.Take(Echo(EchoKind.Undertow)));
            Assert.IsFalse(Echoes.Full);
            Assert.IsTrue(Echoes.Take(Echo(EchoKind.DoubleTime)));
            Assert.IsFalse(Echoes.Take(Echo(EchoKind.Encore)), "a fourth has no room");
            Assert.IsTrue(Echoes.Full);
            Assert.IsTrue(Echoes.Has(EchoKind.SecondBeat));
            Assert.IsFalse(Echoes.Has(EchoKind.Encore));
            Assert.AreEqual(Echoes.Haste, Echoes.Rate);
        }

        [Test]
        public void AChoiceStillOwedCountsAgainstTheRoom()
        {
            Echoes.Take(Echo(EchoKind.SecondBeat));
            Echoes.Take(Echo(EchoKind.Undertow));
            Assert.IsFalse(Echoes.Full);
            Echoes.Owed = 1;
            Assert.IsTrue(Echoes.Full, "two held and one to be chosen leaves no room for a fourth");
        }

        [Test]
        public void CrescendoGrowsWithKillsUpToItsCapAndEndsWithAHit()
        {
            for (int i = 0; i < 100; i++) Echoes.Kill();
            Assert.AreEqual(Echoes.CrescendoCap, Echoes.Streak);
            Assert.AreEqual(1f, Echoes.Damage, "kills add nothing for a machine without the Echo");
            Echoes.Take(Echo(EchoKind.Crescendo));
            Assert.AreEqual(1f + Echoes.CrescendoStep * Echoes.CrescendoCap, Echoes.Damage, 0.0001f);
            Echoes.Hurt();
            Assert.AreEqual(1f, Echoes.Damage);
        }

        [Test]
        public void EncoreWorksOnceAndAfterimageOnEveryTenthHit()
        {
            Assert.IsFalse(Echoes.Encore());
            Echoes.Take(Echo(EchoKind.Encore));
            Assert.IsTrue(Echoes.EncoreReady);
            Assert.IsTrue(Echoes.Encore());
            Assert.IsFalse(Echoes.Encore());
            Assert.IsFalse(Echoes.Tenth());
            Echoes.Take(Echo(EchoKind.Afterimage));
            int strikes = 0;
            for (int i = 0; i < 30; i++)
                if (Echoes.Tenth()) strikes++;
            Assert.AreEqual(3, strikes);
        }

        [Test]
        public void AnOfferIsThreeDifferentEchoesTheMachineDoesNotHold()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.echoes = System.Array.ConvertAll(
                new[] { EchoKind.SecondBeat, EchoKind.Undertow, EchoKind.Crescendo, EchoKind.Afterburn, EchoKind.DoubleTime }, Echo);
            Echoes.Take(config.echoes[0]);
            for (int seed = 0; seed < 20; seed++)
            {
                var random = new System.Random(seed);
                var offer = Echoes.Offer(config, null, () => (float)random.NextDouble());
                Assert.AreEqual(Echoes.Choices, offer.Count);
                CollectionAssert.AllItemsAreUnique(offer);
                CollectionAssert.DoesNotContain(offer, config.echoes[0]);
            }
            // With fewer than three left, the offer is what is left.
            Echoes.Take(config.echoes[1]);
            Echoes.Take(config.echoes[2]);
            Assert.AreEqual(2, Echoes.Offer(config, null, () => 0.5f).Count);
        }

        [Test]
        public void ALongWireLetsASystemReachTheWeaponsItIsNotWiredTo()
        {
            SlotDef Slot(string id, SlotType type) => new SlotDef { id = id, type = type };
            var frame = ScriptableObject.CreateInstance<FrameDef>();
            frame.slots = new[]
            {
                Slot("front", SlotType.Hardpoint), Slot("rear", SlotType.Hardpoint), Slot("system1", SlotType.System), Slot("armor1", SlotType.Armor),
            };
            frame.wires = new[] { new WireDef { system = "system1", slot = "front" } };
            var gun = ScriptableObject.CreateInstance<WeaponDef>();
            var feed = ScriptableObject.CreateInstance<SystemDef>();
            feed.kind = SystemKind.Feed;
            feed.feedState = StateType.Heat;
            var plate = ScriptableObject.CreateInstance<SupportDef>();
            plate.trigger = TriggerType.BurstWhenHit;
            var loadout = ScriptableObject.CreateInstance<Loadout>();
            loadout.frame = frame;
            loadout.modules = new[]
            {
                new Loadout.Entry { slot = "front", module = gun }, new Loadout.Entry { slot = "rear", module = gun },
                new Loadout.Entry { slot = "system1", module = feed }, new Loadout.Entry { slot = "armor1", module = plate },
            };

            Assert.AreEqual(StateType.Heat, Wiring.Resolve(loadout, "front").feed);
            Assert.AreEqual(StateType.None, Wiring.Resolve(loadout, "rear").feed);
            Assert.AreEqual(StateType.Heat, Wiring.Resolve(loadout, "rear", longWire: true).feed);
            Assert.AreEqual(StateType.None, Wiring.Resolve(loadout, "armor1", longWire: true).feed, "it reaches weapons, not plates");
            Assert.IsTrue(Wiring.GainsFromLongWire(loadout));
            // A machine whose system is wired to every weapon gains nothing from it.
            frame.wires = new[] { new WireDef { system = "system1", slot = "front" }, new WireDef { system = "system1", slot = "rear" } };
            Assert.IsFalse(Wiring.GainsFromLongWire(loadout));
        }
    }

    public class CodexTests
    {
        [Test]
        public void EveryBuiltReactionIsMadeOfTwoStatesAndNoneNeedsMagnet()
        {
            Assert.AreEqual(6, Codex.Built.Length);
            var seen = new HashSet<ReactionType>();
            foreach (var reaction in Codex.Built)
            {
                Assert.IsTrue(seen.Add(reaction));
                Codex.States(reaction, out var first, out var second);
                Assert.AreEqual(reaction, Reactions.Of(first, second));
                Assert.AreNotEqual(StateType.Magnet, first);
                Assert.AreNotEqual(StateType.Magnet, second, "nothing applies Magnet yet");
            }
            Codex.States(ReactionType.ThermalShock, out var a, out var b);
            Assert.AreEqual(StateType.Heat, a);
            Assert.AreEqual(StateType.Cryo, b);
            Assert.AreEqual("Thermal Shock", Codex.Name(ReactionType.ThermalShock));
        }

        [Test]
        public void ASpeciesItsMiniBossAndItsBossAreThreeStepsOfOneEntry()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var mite = ScriptableObject.CreateInstance<EnemyDef>();
            mite.name = "TestMite";
            mite.displayName = "Mite";
            mite.miniBossName = "Matron";
            mite.bossName = "Mother";
            var grunt = ScriptableObject.CreateInstance<EnemyDef>();
            grunt.name = "TestGrunt";
            grunt.displayName = "Grunt";
            config.grunt = grunt;
            config.brute = mite;
            var entries = new List<Codex.Entry>();
            Codex.All(config, Codex.Section.Strays, entries);
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual(1, entries[0].steps.Length, "a Stray without ranks of its own is one step");
            Assert.AreEqual(3, entries[1].steps.Length);
            Assert.AreEqual(Codex.ForSpecies, entries[1].steps[0].reward);
            Assert.AreEqual(Codex.ForMiniBoss, entries[1].steps[1].reward);
            Assert.AreEqual(Codex.ForBoss, entries[1].steps[2].reward);
            Assert.AreEqual("stray/TestMite/boss", Codex.Key(mite, Rank.Boss));
            Assert.AreEqual("stray/TestMite", Codex.Key(mite, Rank.Normal));
        }
    }
}
