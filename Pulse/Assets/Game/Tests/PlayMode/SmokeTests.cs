using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pulse.PlayTests
{
    // Plays through the main paths of the game. Unity fails a test by itself when the game logs an
    // error or throws, so these also catch anything that breaks while the scenes run.
    public class SmokeTests
    {
        // Without this the game stands still whenever the editor is not the window in front.
        [SetUp]
        public void KeepRunning()
        {
            Application.runInBackground = true;
            // A profile of the tests' own, started fresh for every test.
            Profile.Key = "pulse.profile.test";
            Profile.Erase();
        }

        [TearDown]
        public void Reset()
        {
            Rig.MoveOverride = null;
            Rig.BoostOverride = null;
            GameRoot.FindSeed = null;
            Time.timeScale = 1f;
            Profile.Erase();
        }

        // Waits until something is true, and fails the test if that takes too long.
        static IEnumerator Until(Func<bool> done, float seconds, string what)
        {
            float waited = 0f;
            while (!done())
            {
                waited += Time.unscaledDeltaTime;
                if (waited > seconds) Assert.Fail($"Waited {seconds} s for: {what}");
                yield return null;
            }
        }

        // Lets the fights run for a while and keeps the machine repaired, so the test is about the
        // fights and not about whether this machine survives them.
        static IEnumerator Survive(float seconds)
        {
            var root = UnityEngine.Object.FindAnyObjectByType<GameRoot>();
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (Combat.Rig.Alive) Combat.Rig.Repair();
                // The run stands still while a choice of Echoes is up. A test that is not about
                // Echoes takes the first card and goes on.
                if (root != null && root.Echo.IsOpen) root.Echo.Pick(0);
                yield return null;
            }
        }

        // Waits with the world kept empty, so that nothing wanders in and takes part in what is being tested.
        static IEnumerator Alone(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                Combat.Enemies.Clear();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator LobbyOpensItsScreensTheArmsWorkAndThePadStartsARun()
        {
            yield return SceneManager.LoadSceneAsync("Lobby");
            yield return null;
            var lobby = UnityEngine.Object.FindAnyObjectByType<LobbyRoot>();
            var character = UnityEngine.Object.FindAnyObjectByType<LobbyCharacter>();
            Assert.IsNotNull(lobby);
            Assert.IsNotNull(character);

            Rig.MoveOverride = new Vector2(0.5f, 0.5f);
            yield return new WaitForSeconds(0.6f);
            Assert.Greater(character.transform.position.z, -6.4f, "the character walks");
            Rig.MoveOverride = null;

            lobby.OpenMechanic();
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(character.Frozen, "the character stands still while a screen is open");
            lobby.CloseMechanic();
            yield return null;
            yield return null;
            Assert.IsFalse(character.Frozen);

            // Every machine has a room behind a bay door. Three are open from the start, the Tuner's is
            // not. The character walks into the Mechanic's, which lies behind the far wall, and the
            // camera turns to look into it from the side; back in the hall it turns back.
            Assert.AreEqual(4, lobby.Rooms.Count);
            int open = 0;
            foreach (var one in lobby.Rooms) if (one.IsOpen) open++;
            Assert.AreEqual(3, open);
            LobbyRoom shop = null;
            foreach (var one in lobby.Rooms) if (one is MechanicRoom) shop = one;
            Assert.IsNotNull(shop);
            Assert.Greater(Mathf.Abs(shop.Yaw), 20f, "a room behind the far wall is looked into from the side");
            var eye = Camera.main.GetComponent<CameraRig>();
            Vector3 into = shop.Outward;
            character.transform.position = shop.DoorPos;
            eye.Snap();
            yield return null;
            Assert.AreEqual(0f, eye.yaw, 0.5f);
            Rig.MoveOverride = new Vector2(into.x, into.z);
            yield return Until(() => Vector3.Dot(character.transform.position - shop.DoorPos, into) > 3.2f, 6f, "the character to walk into the Mechanic's shop");
            Rig.MoveOverride = null;
            yield return Until(() => Mathf.Abs(Mathf.DeltaAngle(eye.yaw, shop.Yaw)) < 2f, 4f, "the camera to turn to the room");
            Assert.Less((character.transform.position - shop.StationPos).magnitude, 6f, "the Mechanic is in its room");
            // The stick means what the camera shows: "up" now leads deeper into the picture, which is
            // across this room, not along it.
            Vector3 before = character.transform.position, ahead = Quaternion.Euler(0f, shop.Yaw, 0f) * Vector3.forward;
            Rig.MoveOverride = new Vector2(0f, 1f);
            yield return new WaitForSeconds(0.35f);
            Rig.MoveOverride = null;
            Vector3 moved = character.transform.position - before;
            Assert.Greater(Vector3.Dot(moved.normalized, ahead), 0.9f, "up on the stick is away from the camera");
            // Back in the hall the camera turns back.
            character.transform.position = shop.DoorPos - into * 1.5f;
            yield return Until(() => Mathf.Abs(eye.yaw) < 2f, 4f, "the camera to turn back in the hall");

            // The display at the turntable shows the machines.
            lobby.OpenPresets();
            yield return null;
            yield return null;
            Assert.IsTrue(character.Frozen);
            Assert.IsTrue(lobby.Presets.IsOpen);
            lobby.ClosePresets();
            yield return null;

            // The machine stands on the deck of the turntable, the arms work on it, and now and then
            // the deck turns it to another side.
            var deck = lobby.Workshop.Deck;
            Assert.IsNotNull(deck);
            yield return Until(() => !lobby.Workshop.Clear, 4f, "an arm to start working");
            yield return Until(() => Mathf.Abs(Mathf.DeltaAngle(0f, deck.localEulerAngles.y)) > 5f, 14f, "the deck to turn the machine");

            // The pause menu stops time and gives it back.
            lobby.Pause.Open("The Roundhouse");
            yield return null;
            Assert.AreEqual(0f, Time.timeScale);
            lobby.Pause.Close();
            Assert.AreEqual(1f, Time.timeScale);

            // Standing on the pad starts the run: the machine fetches the character and drives out.
            character.transform.position = new Vector3(0f, 0f, 10.8f);
            yield return Until(() => lobby.Busy, 4f, "the scene to begin");
            yield return Until(() => lobby.Workshop.Clear || SceneManager.GetActiveScene().name == "Arena", 5f, "the arms to fold away");
            yield return Until(() => SceneManager.GetActiveScene().name == "Arena", 14f, "the machine to leave for the open world");
            yield return null;
            Assert.IsNotNull(Combat.Rig);
            // It arrives at speed and then comes down to its own.
            yield return Until(() => !Combat.Rig.Launching && !Combat.Rig.Scripted, 6f, "the machine to slow down after arriving");
            Assert.Less(Mathf.Abs(Combat.Rig.transform.position.x), 1f, "it arrives in a straight line");
            Assert.Less(Combat.Rig.transform.position.magnitude, 12f, "it ends up at the starting point");
        }

        [UnityTest]
        public IEnumerator DrivingTakingHitsAndEnteringADungeonRaiseNoErrors()
        {
            yield return SceneManager.LoadSceneAsync("Arena");
            yield return null;
            Vector3 start = Combat.Rig.transform.position;

            // Straight ahead, then a hard turn: this runs over plants and into whatever stands in the way.
            Rig.MoveOverride = new Vector2(0f, 1f);
            yield return new WaitForSeconds(5f);
            Assert.Greater((Combat.Rig.transform.position - start).magnitude, 5f, "the machine drives");
            Rig.MoveOverride = new Vector2(1f, 1f);
            yield return new WaitForSeconds(3f);

            float hull = Combat.Rig.Hull;
            Combat.Rig.TakeHit(9f, Combat.Rig.transform.position + Vector3.right * 2f);
            Assert.Less(Combat.Rig.Hull, hull, "a hit costs hull");
            yield return new WaitForSeconds(0.6f);

            // Standing in the first entrance breaks its floor; the machine falls into the dungeon,
            // where the first wave arrives.
            Combat.Rig.Teleport(Combat.Config.world.entrances[0].position);
            Rig.MoveOverride = Vector2.zero;
            yield return Until(() => Combat.Director.Mode == RunMode.Dungeon, 6f, "the fall into the dungeon to begin");
            yield return Until(() => !Combat.Director.Busy, 6f, "the machine to land");
            Assert.AreEqual(0f, Combat.Rig.transform.position.y, 0.01f, "it stands on the dungeon floor");
            yield return Until(() => Combat.Enemies.Count > 0, 6f, "the first wave");
        }

        [UnityTest]
        public IEnumerator EverySpeciesFightsInEveryRankWithoutErrors()
        {
            yield return SceneManager.LoadSceneAsync("Arena");
            yield return null;
            var rig = Combat.Rig;
            Rig.MoveOverride = new Vector2(0.4f, 1f);
            int kinds = 0;

            // One of each rank of each species, placed around the machine and already after it.
            foreach (var biome in Combat.Config.scenery.biomes)
            foreach (var species in biome.species)
            {
                kinds++;
                foreach (Rank rank in new[] { Rank.Normal, Rank.MiniBoss, Rank.Boss })
                {
                    float angle = UnityEngine.Random.value * Mathf.PI * 2f;
                    Combat.Enemies.Spawn(species, rig.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 9f, true, rank);
                }
                yield return Survive(2.5f);
            }
            Assert.AreEqual(9, kinds, "three species in each of three biomes");
            Assert.Greater(Combat.Enemies.Elites.Count, 0, "mini bosses and bosses are reported to the HUD");

            // Killing everything runs every way of dying: bursts, splits and broods.
            for (int round = 0; round < 6; round++)
            {
                for (int i = Combat.Enemies.Count - 1; i >= 0; i--) Combat.Enemies.Hit(i, 100000f, StateType.None, ignoreArmor: true);
                yield return Survive(0.3f);
            }
            Assert.IsTrue(rig.Alive);
            Assert.Greater(RunStats.Bosses, 0);
            Assert.Greater(RunStats.MiniBosses, 0);
        }

        [UnityTest]
        public IEnumerator NitroHeartsAndTheStormDoWhatTheySay()
        {
            yield return SceneManager.LoadSceneAsync("Arena");
            yield return null;
            var rig = Combat.Rig;
            float top = rig.Loadout.drive.speed;

            // Nitro: faster than top speed while it burns, and the tank empties. Driven south, along
            // the strip the machine arrives on, because nothing solid stands there.
            rig.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Rig.MoveOverride = new Vector2(0f, 1f);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(1f, rig.Nitro, 0.001f, "the tank starts full and stays full while unused");
            Rig.BoostOverride = true;
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(rig.Boosting);
            Assert.Greater(rig.Velocity.magnitude, top * 1.3f);
            Assert.Less(rig.Nitro, 0.8f);
            // Held until it is empty, it stops by itself; let go, it refills.
            yield return new WaitForSeconds(1.9f);
            Assert.IsFalse(rig.Boosting, "an empty tank burns nothing");
            Rig.BoostOverride = false;
            Rig.MoveOverride = Vector2.zero;
            float low = rig.Nitro;
            yield return new WaitForSeconds(2f);
            Assert.Greater(rig.Nitro, low);

            // A heart is left alone while the hull is whole, and repairs it once it is not.
            var heart = Combat.Config.drops.entries[0];
            Combat.Enemies.Clear();
            yield return null;
            Combat.Pickups.Clear();
            Combat.Pickups.Spawn(heart, rig.transform.position);
            yield return Alone(1.5f);
            Assert.AreEqual(1, Combat.Pickups.Count, "a heart waits while nothing needs repairing");
            rig.TakeHit(40f, rig.transform.position + Vector3.right * 2f);
            float hurt = rig.Hull;
            yield return Until(() => { Combat.Enemies.Clear(); return Combat.Pickups.Count == 0; }, 4f, "the heart to be picked up");
            Assert.AreEqual(hurt + heart.amount, rig.Hull, 0.01f);
            Assert.AreEqual(1, RunStats.Hearts);

            // Past the edge of the map the storm hurts, and more so further out.
            var world = Combat.Config.world;
            rig.Repair();
            rig.Teleport(new Vector3(world.halfSize + world.stormDepth * 0.1f, 0f, 0f));
            yield return new WaitForSeconds(2.5f);
            Assert.Greater(Combat.Storm.Depth, 0f);
            float shallow = rig.MaxHull - rig.Hull;
            Assert.Greater(shallow, 0f, "the storm hurts");
            rig.Repair();
            rig.Teleport(new Vector3(world.halfSize + world.stormDepth * 0.6f, 0f, 0f));
            yield return new WaitForSeconds(2.5f);
            Assert.Greater(rig.MaxHull - rig.Hull, shallow * 2f, "it hurts much more deep inside");
            Assert.IsTrue(rig.Alive);
            // Back inside the map it stops.
            rig.Teleport(Vector3.zero);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0f, Combat.Storm.Depth);
        }

        [UnityTest]
        public IEnumerator TheWardenMakesItsEntranceChangesPhaseAndFalls()
        {
            yield return SceneManager.LoadSceneAsync("Arena");
            yield return null;
            var world = Combat.Config.world;
            int keep = Array.FindIndex(world.entrances, e => e.dungeon.boss != null);
            Assert.GreaterOrEqual(keep, 0, "the world has a keep with a Warden in it");
            var def = world.entrances[keep].dungeon;
            int beats = Profile.Beats;
            Assert.IsFalse(Profile.IsFound(def.finds[0]), "what the Warden keeps is not the player's yet");

            // Standing in the entrance in front of the keep drops the machine into the arena.
            Combat.Rig.Teleport(world.entrances[keep].position);
            Rig.MoveOverride = Vector2.zero;
            yield return Until(() => Combat.Director.Mode == RunMode.Dungeon, 6f, "the fall into the arena to begin");
            Assert.IsTrue(Combat.Director.Boss.Active);
            Assert.AreEqual(-1, Combat.Enemies.Warden(), "the arena is empty until the gate opens");

            // The Warden comes through the gate. Until its entrance is over, nothing can hurt it.
            yield return Until(() => Combat.Enemies.Warden() >= 0, 12f, "the Warden to come through the gate");
            int index = Combat.Enemies.Warden();
            Combat.Enemies.Hit(index, 500f, StateType.None, ignoreArmor: true);
            Assert.AreEqual(Combat.Enemies[index].maxHp, Combat.Enemies[index].hp, "it cannot be hurt during its entrance");
            Assert.IsTrue(Combat.Director.Busy, "the machine waits while the Warden enters");
            yield return Until(() => !Combat.Director.Busy, 12f, "the entrance to end");
            yield return null;
            Assert.AreEqual(1, Combat.Enemies.Elites.Count, "the Warden is reported to the HUD");
            Assert.AreEqual(Rank.Warden, Combat.Enemies.Elites[0].rank);

            // First phase: it fights, and at half its hull it changes.
            Rig.MoveOverride = new Vector2(0.45f, 1f);
            yield return Survive(6f);
            index = Combat.Enemies.Warden();
            Assert.GreaterOrEqual(index, 0);
            Assert.AreEqual(1, Combat.Enemies[index].stage);
            Assert.IsFalse(Combat.Director.Boss.VentsLit, "the vents are cold in the first phase");
            Combat.Enemies.Hit(index, Combat.Enemies[index].hp - Combat.Enemies[index].maxHp * 0.45f, StateType.None, ignoreArmor: true);
            yield return Until(() => Combat.Enemies.Warden() >= 0 && Combat.Enemies[Combat.Enemies.Warden()].stage == 2, 6f, "the second phase");
            Assert.IsTrue(Combat.Director.Boss.VentsLit, "the second phase lights the vents");
            yield return Survive(7f);
            Assert.Greater(Combat.Marks.Count, 0, "its blows leave the floor cracked and scorched");

            // Its fall pays out and opens the way back.
            index = Combat.Enemies.Warden();
            Assert.GreaterOrEqual(index, 0);
            Combat.Enemies.Hit(index, 100000f, StateType.None, ignoreArmor: true);
            yield return Until(() => Combat.Director.Mode == RunMode.OpenWorld && !Combat.Director.Busy, 14f, "the way back to the open world");
            Assert.AreEqual(1f, Time.timeScale, "time runs normally again after its fall");
            Assert.GreaterOrEqual(RunStats.Beats, def.reward, "a fallen Warden pays, into what the machine carries");
            Assert.AreEqual(beats, Profile.Beats, "and none of it is the player's before it is home");
            Assert.IsTrue(Profile.IsFound(def.finds[0]), "what it kept is the player's now");
            Assert.IsTrue(Profile.IsFresh(def.finds[0]), "and the Archivist has not shown it yet");
            Assert.IsTrue(Combat.Rig.Alive);

            // The keep counts as cleared from now on, and the Echo of its strand joins the others.
            Assert.IsTrue(Profile.HasCleared(def));
            Assert.IsTrue(Echoes.InPool(Array.Find(Combat.Config.echoes, e => e.joinsAfter == def)));
            // What the keep held besides lies where the machine came out: an Echo. Driving onto it
            // brings up the choice.
            var root = UnityEngine.Object.FindAnyObjectByType<GameRoot>();
            Rig.MoveOverride = Vector2.zero;
            Assert.AreEqual(0, Echoes.Held.Count);
            Combat.Rig.Teleport(world.entrances[keep].position + Vector3.back * (RunDirector.GateRadius + 7f));
            yield return Until(() => root.Echo.IsOpen, 5f, "the choice of an Echo after the keep");
            root.Echo.Pick(0);
            yield return null;
            Assert.AreEqual(1, Echoes.Held.Count);
        }

        [UnityTest]
        public IEnumerator TheArchivistTheSmelterAndTheMechanicDoTheirWork()
        {
            yield return SceneManager.LoadSceneAsync("Lobby");
            yield return null;
            yield return null;
            var lobby = UnityEngine.Object.FindAnyObjectByType<LobbyRoot>();
            var config = Combat.Config;
            ModuleDef Named(string name) => Array.Find(config.modules, m => m.name == name);
            FrameDef Frame(string name) => Array.Find(config.frames, f => f.name == name);
            var longshot = Named("Longshot");
            FrameDef buggy = Frame("Buggy"), rover = Frame("Rover"), crawler = Frame("Crawler");
            Assert.IsNotNull(longshot);
            Assert.IsFalse(Profile.IsFound(longshot), "a dungeon keeps it, so it is not there from the start");

            // A new player has one machine, on the smallest frame: one weapon and one socket.
            Assert.AreEqual(1, Profile.Presets.Count);
            Assert.AreEqual(buggy, Profile.Machine.frame);
            Assert.AreEqual(1, buggy.Count(SlotType.Hardpoint));
            Assert.AreEqual(1, buggy.Count(SlotType.Socket));
            Assert.AreEqual(3, buggy.slots.Length, "a weapon, a socket and the drive");
            Assert.IsTrue(Profile.IsFound(buggy));
            Assert.IsFalse(Profile.IsFound(rover), "every other frame has to be bought");
            // Every part is an item of its own, and what is on the machine is one of them.
            Assert.IsNotNull(Profile.Machine.ItemIn("turret"));
            Assert.Contains(Profile.Machine.ItemIn("turret"), Profile.Items);
            Assert.IsTrue(Profile.IsFitted(Profile.Machine.ItemIn("turret")));
            Assert.IsNotNull(Profile.Items.Find(i => i.def.name == "Extender"), "the range booster is everyone's from the start");

            // The Mechanic does not offer what the player does not have.
            lobby.OpenMechanic();
            yield return null;
            var mechanic = lobby.Mechanic;
            Assert.IsFalse(mechanic.Fit("turret", longshot));
            Assert.IsFalse(mechanic.Fit("frame", rover));
            lobby.CloseMechanic();

            // A part comes home. The Archivist opens on what is new, and looking at it takes the mark away.
            Assert.IsTrue(Profile.Find(longshot));
            Assert.IsTrue(Profile.IsFresh(longshot));
            lobby.OpenArchive();
            yield return null;
            Assert.IsFalse(Profile.IsFresh(longshot));
            for (int kind = 0; kind < Parts.Kinds; kind++)
                for (int item = 0; item < 3; item++)
                {
                    lobby.Archive.Show((PartKind)kind, item);
                    yield return null;
                }
            lobby.CloseArchive();
            yield return null;

            // Now it fits. A change is only on the screen until it is saved.
            lobby.OpenMechanic();
            yield return null;
            Assert.IsTrue(mechanic.Fit("turret", longshot));
            Assert.IsTrue(mechanic.HasChanges);
            Assert.AreNotEqual(longshot, Profile.Presets[0].In("turret"), "the saved machine is untouched");
            mechanic.SaveChanges();
            Assert.AreEqual(longshot, Profile.Presets[0].In("turret"));
            Assert.IsFalse(mechanic.HasChanges);

            // The socket takes a system or armor, and never a weapon. A booster in it adds range.
            Assert.IsTrue(mechanic.Fit("socket", Named("Splitter")));
            Assert.IsTrue(Wiring.Resolve(mechanic.Shown, "turret").Has(Shapers.Splitter), "a system in the socket acts on the weapon");
            Assert.IsTrue(mechanic.Fit("socket", Named("Extender")));
            Assert.AreEqual(1.4f, Wiring.Resolve(mechanic.Shown, "turret").range, 0.001f);
            Assert.IsTrue(mechanic.Fit("socket", Named("ReactivePlating")));
            Assert.IsFalse(mechanic.Fit("socket", longshot), "the only Longshot is on the turret, and it is no system");
            Assert.IsFalse(mechanic.Fit("turret", Named("Plating")));
            mechanic.SaveChanges();
            lobby.CloseMechanic();

            // The Smelter sells frames and Marks for Beats, and nothing without them.
            lobby.OpenSmelter();
            yield return null;
            var smelter = lobby.Smelter;
            smelter.Show(rover);
            Assert.IsFalse(smelter.Act(), "no Beats, no frame");
            Assert.IsFalse(Profile.IsFound(rover));
            Profile.Earn(rover.price + rover.marks[0].cost + crawler.price + 10);
            int beats = Profile.Beats;
            Assert.IsTrue(smelter.Act());
            Assert.IsTrue(Profile.IsFound(rover));
            Assert.AreEqual(beats - rover.price, Profile.Beats);
            Assert.AreEqual(0, Profile.MarkOf(rover));
            Assert.IsTrue(smelter.Act(), "the next thing to buy for a frame the player has is its next Mark");
            Assert.AreEqual(1, Profile.MarkOf(rover));
            Assert.Greater(rover.HullAt(1), rover.HullAt(0), "a Mark makes the frame tougher");
            Assert.AreEqual(5, rover.slots.Length, "and leaves its slots as they are");
            Assert.AreEqual("SYSTEM", Parts.SlotName(rover, Array.Find(rover.slots, x => x.type == SlotType.System)));
            Assert.AreEqual("ARMOR", Parts.SlotName(rover, Array.Find(rover.slots, x => x.type == SlotType.Armor)));
            smelter.Show(crawler);
            Assert.IsTrue(smelter.Act());
            Assert.IsTrue(Profile.IsFound(crawler));
            Assert.IsFalse(smelter.Act(), "not enough left for its second Mark");
            Assert.AreEqual(0, Profile.MarkOf(crawler));
            Assert.AreEqual("ARMOR 2", Parts.SlotName(crawler, Array.Find(crawler.slots, x => x.id == "armor2")));
            yield return null;
            lobby.CloseSmelter();

            // The machine goes onto the large frame. What has a slot of the same name there stays on.
            Profile.Grant(longshot);
            Profile.Grant(longshot);
            lobby.OpenMechanic();
            yield return null;
            Assert.IsTrue(mechanic.Fit("frame", crawler));
            Assert.AreEqual(longshot, mechanic.Shown.In("turret"));
            Assert.IsNull(mechanic.Shown.In("socket"), "the Crawler has no socket");

            // One part sits in one slot: three Longshots are three items.
            Assert.IsTrue(mechanic.Fit("front", longshot));
            Assert.IsTrue(mechanic.Fit("rear", longshot));
            Assert.AreNotSame(mechanic.Shown.ItemIn("front"), mechanic.Shown.ItemIn("rear"));
            Assert.AreNotSame(mechanic.Shown.ItemIn("front"), mechanic.Shown.ItemIn("turret"));
            // Picking a part that is on the machine already moves it.
            var moved = mechanic.Shown.ItemIn("rear");
            Assert.IsTrue(mechanic.Fit("front", moved));
            Assert.AreSame(moved, mechanic.Shown.ItemIn("front"));
            Assert.IsNull(mechanic.Shown.ItemIn("rear"));
            Assert.IsTrue(mechanic.Fit("rear", longshot));
            // Parts are grouped: the three Longshots are one line among the kinds that fit, and
            // listed one by one under it.
            Assert.IsTrue(mechanic.Look("rear", longshot));
            Assert.AreEqual(3, mechanic.ItemCount, "three Longshots under the one line for Longshots");
            int weaponKinds = 0;
            foreach (var module in config.modules)
                if (module is WeaponDef && Profile.Items.Exists(i => i.def == module)) weaponKinds++;
            Assert.AreEqual(weaponKinds + 1, mechanic.KindCount, "one line for each kind of weapon the player has, and one for leaving the slot empty");
            Assert.Less(mechanic.KindCount, Profile.Items.FindAll(i => i.def is WeaponDef).Count + 1);

            // More Power than the reactor gives: it cannot be saved.
            mechanic.Fit("system1", Named("Splitter"));
            mechanic.Fit("system2", Named("IncendiaryFeed"));
            Assert.Greater(mechanic.Shown.PowerUsed, mechanic.Shown.reactor.powerCap);
            mechanic.SaveChanges();
            Assert.AreEqual(buggy, Profile.Presets[0].frame, "a machine that draws too much is not saved");
            yield return null;

            // Emptying a slot brings it back under.
            Assert.IsTrue(mechanic.Fit("system2", (ScriptableObject)null));
            Assert.LessOrEqual(mechanic.Shown.PowerUsed, mechanic.Shown.reactor.powerCap);
            mechanic.SaveChanges();
            Assert.AreEqual(crawler, Profile.Presets[0].frame);
            lobby.CloseMechanic();
            yield return null;

            // The display at the turntable: a second machine, a copy of the first, which is all
            // there is room for. Taking it puts it on the turntable.
            lobby.OpenPresets();
            yield return null;
            var presets = lobby.Presets;
            Assert.IsTrue(presets.New());
            Assert.AreEqual(2, Profile.Presets.Count);
            Assert.IsFalse(presets.New(), "two machines, no more");
            Assert.AreEqual(0, Profile.Current);
            presets.Show(1);
            Assert.IsTrue(presets.Take());
            Assert.IsFalse(presets.IsOpen);
            Assert.AreEqual(1, Profile.Current, "the new machine is on the turntable");
            Assert.AreEqual(crawler, Profile.Machine.frame);
            Assert.AreEqual(longshot, Profile.Machine.In("rear"));
            string taken = Profile.Machine.displayName;
            int rearItem = Profile.Machine.ItemIn("rear").id;
            yield return null;

            // All of it is still there after the game has been closed and opened again. It is saved
            // without being asked: the file is simply there.
            int kept = Profile.Beats, owned = Profile.Items.Count;
            Profile.Flush();
            string file = System.IO.Path.Combine(Application.persistentDataPath, Profile.Key + ".json");
            Assert.IsTrue(System.IO.File.Exists(file), "the profile is a file");
            string key = Profile.Key;
            Profile.Key = key + ".other";
            Profile.Load(config);
            Assert.IsFalse(Profile.IsFound(longshot), "another profile knows nothing of it");
            Assert.IsFalse(Profile.IsFound(rover));
            Profile.Erase();
            Profile.Key = key;
            Profile.Load(config);
            Assert.IsTrue(Profile.IsFound(longshot));
            Assert.IsTrue(Profile.IsFound(rover));
            Assert.AreEqual(1, Profile.MarkOf(rover));
            Assert.AreEqual(kept, Profile.Beats);
            Assert.AreEqual(owned, Profile.Items.Count);
            Assert.AreEqual(2, Profile.Presets.Count);
            Assert.AreEqual(taken, Profile.Machine.displayName);
            Assert.AreEqual(longshot, Profile.Machine.In("rear"));
            Assert.AreEqual(rearItem, Profile.Machine.ItemIn("rear").id, "the very same part is back in its slot");

            // A machine can be removed again, but not the last one.
            lobby.OpenPresets();
            yield return null;
            presets.Show(1);
            Assert.IsTrue(presets.Delete());
            Assert.AreEqual(1, Profile.Presets.Count);
            Assert.IsFalse(presets.Delete(), "the last machine stays");
            lobby.ClosePresets();
        }

        [UnityTest]
        public IEnumerator TheTuningRoomIsBoughtOpenAndTheTunerRaisesAndScrapsParts()
        {
            yield return SceneManager.LoadSceneAsync("Lobby");
            yield return null;
            yield return null;
            var lobby = UnityEngine.Object.FindAnyObjectByType<LobbyRoot>();
            var config = Combat.Config;
            var room = lobby.TuningRoom;
            int price = config.lobby.tunerRoomPrice;
            Vector3 inside = room.Middle - (room.Middle - room.DoorPos).normalized * 2.5f;

            // The door is shut: nobody walks into the room, and it cannot be had for nothing.
            Assert.IsFalse(room.IsOpen);
            Assert.IsFalse(Profile.IsUnlocked(TuningRoom.Id));
            Assert.Greater((lobby.Walk.Resolve(inside, 0.35f) - inside).magnitude, 1f, "the wall keeps the character out");
            var character = UnityEngine.Object.FindAnyObjectByType<LobbyCharacter>();
            Vector3 into = room.Outward;
            float Through() => Vector3.Dot(character.transform.position - room.DoorPos, into);
            character.transform.position = room.DoorPos;
            Rig.MoveOverride = new Vector2(into.x, into.z);
            yield return new WaitForSeconds(1.2f);
            Assert.Less(Through(), 1.2f, "a shut door is not walked through");
            Assert.IsFalse(Pointer.Shown, "no pointer while the character walks");
            Rig.MoveOverride = null;
            lobby.BuyTuningRoom();
            yield return null;
            Assert.IsFalse(lobby.Busy);
            Assert.IsFalse(Profile.IsUnlocked(TuningRoom.Id));

            // Bought, it opens in a scene, and then the character can walk in.
            Profile.Earn(price + 500);
            int beats = Profile.Beats;
            var eye = Camera.main.GetComponent<CameraRig>();
            Vector3 home = eye.offset;
            lobby.BuyTuningRoom();
            yield return null;
            Assert.IsTrue(lobby.Busy, "the door opens in a scene");
            Assert.AreEqual(beats - price, Profile.Beats);
            Assert.IsTrue(Profile.IsUnlocked(TuningRoom.Id));
            // The door stands side-on to the hall's camera, so the camera comes round in front of
            // it while it opens, and is back in its place afterwards.
            yield return Until(() => Vector3.Dot(eye.transform.forward, into) > 0.6f, 4f, "the camera to face the door");
            Assert.IsFalse(room.IsOpen, "it is in front of the door before the door opens");
            yield return Until(() => room.IsOpen && !lobby.Busy, 14f, "the door to open");
            Assert.AreEqual(home, eye.offset);
            Assert.AreEqual(0f, eye.pin);
            Assert.Less((lobby.Walk.Resolve(inside, 0.35f) - inside).magnitude, 0.01f, "the room can be walked into");
            // Walked, not placed: from in front of the door, through it, up to the press, and back out.
            character.transform.position = room.DoorPos;
            Rig.MoveOverride = new Vector2(into.x, into.z);
            yield return Until(() => Through() > 5f, 6f, "the character to walk through the door into the room");
            Rig.MoveOverride = new Vector2(-into.x, -into.z);
            yield return Until(() => Through() < 0.3f, 6f, "the character to walk back out into the hall");
            Rig.MoveOverride = null;
            // What stands in the room is solid: nobody stands inside the press.
            Vector3 press = room.Middle + (room.Middle - room.DoorPos).normalized * 1.4f;
            Assert.Greater((lobby.Walk.Resolve(press, 0.35f) - press).magnitude, 0.3f);

            // The Tuner raises a part by a level for Beats, as long as there are Beats and levels left.
            var gun = Profile.Machine.ItemIn("turret");
            float before = Tuning.Of(gun.def, gun).damage;
            lobby.OpenTuner();
            yield return null;
            yield return null;
            Assert.IsTrue(Pointer.Shown, "a screen brings the pointer back");
            var tuner = lobby.Tuner;
            tuner.Show(gun);
            Assert.AreSame(gun, tuner.Shown);
            int cost = Tuning.UpgradeCost(gun);
            beats = Profile.Beats;
            Assert.IsTrue(tuner.Upgrade());
            Assert.AreEqual(2, gun.level);
            Assert.AreEqual(beats - cost, Profile.Beats);
            Assert.Greater(Tuning.Of(gun.def, gun).damage, before);
            while (gun.level < Tuning.MaxLevel)
            {
                Profile.Earn(Tuning.UpgradeCost(gun));
                Assert.IsTrue(tuner.Upgrade());
            }
            Profile.Earn(1000);
            Assert.IsFalse(tuner.Upgrade(), "level five is the top");

            // A part on a machine cannot be scrapped. A spare one can, and pays Beats.
            Assert.IsFalse(tuner.Scrap());
            var spare = Profile.Grant(gun.def);
            Assert.IsTrue(spare.fresh);
            tuner.Show(spare);
            Assert.IsFalse(spare.fresh, "looking at a part takes its mark away");
            // Parts are grouped: the two guns are listed under the one line for their kind.
            Assert.AreEqual(2, tuner.ShownCount);
            int kindsOwned = 0;
            foreach (var module in config.modules)
                if (Profile.Items.Exists(i => i.def == module)) kindsOwned++;
            Assert.AreEqual(kindsOwned, tuner.GroupCount, "one line for each kind of part the player has");
            Assert.Less(tuner.GroupCount, Profile.Items.Count);
            beats = Profile.Beats;
            int owned = Profile.Items.Count;
            Assert.IsTrue(tuner.Scrap());
            Assert.AreEqual(owned - 1, Profile.Items.Count);
            Assert.AreEqual(beats + Tuning.ScrapValue(spare), Profile.Beats);
            lobby.CloseTuner();

            // The room stays open, and the part keeps its level, when the game is opened again.
            Profile.Flush();
            yield return SceneManager.LoadSceneAsync("Lobby");
            yield return null;
            yield return null;
            lobby = UnityEngine.Object.FindAnyObjectByType<LobbyRoot>();
            Assert.IsTrue(lobby.TuningRoom.IsOpen);
            Assert.AreEqual(Tuning.MaxLevel, Profile.Machine.ItemIn("turret").level);
        }

        [UnityTest]
        public IEnumerator EnemiesDropPartsWithAbilitiesAndTheMachineUsesThem()
        {
            yield return SceneManager.LoadSceneAsync("Arena");
            yield return null;
            var config = Combat.Config;
            var rig = Combat.Rig;
            Rig.MoveOverride = Vector2.zero;
            Assert.IsNotEmpty(config.perks);
            var pool = config.drops;
            Assert.IsTrue(Array.Exists(pool.entries, e => e.kind == DropKind.Part), "the drop pool holds parts");
            var entry = Array.Find(pool.entries, e => e.kind == DropKind.Part);

            // What a boss leaves is a part with an ability that suits it.
            for (int i = 0; i < 40; i++)
            {
                var rolled = Tuning.Roll(config, Rank.Boss, () => UnityEngine.Random.value);
                Assert.IsNotNull(rolled.def);
                Assert.IsNotNull(rolled.ability, "every dropped part has an ability");
                Assert.IsTrue(Tuning.Suits(rolled.ability, rolled.def));
                Assert.GreaterOrEqual(rolled.rarity, Rarity.Common);
                Assert.Less(rolled.rarity, Rarity.Mythic);
                Assert.IsNotEmpty(rolled.ability.Describe(rolled.rarity));
            }

            // It lies on the ground until the machine drives onto it. Then it is the player's.
            var dropped = Tuning.Roll(config, Rank.Boss, () => UnityEngine.Random.value);
            int owned = Profile.Items.Count;
            Combat.Enemies.Clear();
            yield return null;
            Combat.Pickups.Clear();
            Combat.Pickups.Spawn(entry, rig.transform.position, false, dropped);
            yield return Until(() => { Combat.Enemies.Clear(); return Combat.Pickups.Count == 0; }, 5f, "the part to be picked up");
            Assert.AreEqual(owned + 1, Profile.Items.Count);
            Assert.Contains(dropped, Profile.Items);
            Assert.Greater(dropped.id, 0);
            Assert.IsTrue(dropped.fresh);
            Assert.AreEqual(1, RunStats.Parts);
            // The log on the right says so. Beats are logged too, a trail of them as one line that
            // counts up. A heart is not: it is used up on the spot.
            var log = UnityEngine.Object.FindAnyObjectByType<GameRoot>().Log;
            yield return null;
            Assert.AreEqual(1, log.Count);
            StringAssert.Contains(dropped.Name.ToUpperInvariant(), log.Text(0));
            if (dropped.ability != null) StringAssert.Contains(dropped.ability.displayName.ToUpperInvariant(), log.Text(0));
            var beat = Array.Find(pool.entries, e => e.kind == DropKind.Beat);
            Combat.Pickups.Spawn(beat, rig.transform.position);
            Combat.Pickups.Spawn(beat, rig.transform.position);
            yield return Until(() => { Combat.Enemies.Clear(); return Combat.Pickups.Count == 0; }, 5f, "the Beats to be picked up");
            yield return null;
            Assert.AreEqual($"+{Mathf.RoundToInt(beat.amount) * 2} BEATS", log.Text(0), "two lots of Beats are one line");
            Assert.AreEqual(Mathf.RoundToInt(beat.amount) * 2, RunStats.Beats, "the machine carries them");
            Assert.AreEqual(0, Profile.Beats, "they are not the player's until they are home");
            int lines = log.Count;
            rig.TakeHit(10f, rig.transform.position + Vector3.right * 2f);
            Combat.Pickups.Spawn(Array.Find(pool.entries, e => e.kind == DropKind.Heart), rig.transform.position);
            yield return Until(() => { Combat.Enemies.Clear(); return Combat.Pickups.Count == 0; }, 5f, "the heart to be picked up");
            yield return null;
            Assert.LessOrEqual(log.Count, lines, "a heart is not logged");
            StringAssert.Contains("BEATS", log.Text(0));
            rig.Repair();

            // A machine built with a levelled, keen weapon behind an Extender hits harder and reaches further.
            var keen = Array.Find(config.perks, x => x.stat == PerkStat.Damage);
            var gun = Profile.Grant(Array.Find(config.modules, m => m.name == "Autocannon"), Rarity.Legendary, keen);
            gun.level = 3;
            var booster = Profile.Items.Find(i => i.def.name == "Extender");
            var machine = Profile.Copy(Profile.Machine, "Test");
            machine.modules = new[]
            {
                new Loadout.Entry { slot = "turret", module = gun.def, item = gun },
                new Loadout.Entry { slot = "socket", module = booster.def, item = booster },
            };
            var stats = Tuning.Of(gun.def, gun);
            Assert.AreEqual((1f + 2f * 0.12f) * (1f + keen.Value(Rarity.Legendary) / 100f), stats.damage, 0.001f);
            Assert.AreEqual(1.4f, Wiring.Resolve(machine, "turret").range, 0.001f);
            rig.Build(machine, config, refill: true);
            Combat.Enemies.Clear();
            yield return null;
            // Something stands beyond the gun's own range and within the extended one. It gets shot.
            float own = ((WeaponDef)gun.def).range;
            int target = Combat.Enemies.Spawn(config.brute, rig.transform.position + Vector3.forward * (own * 1.2f), true);
            float hull = Combat.Enemies[target].hp;
            yield return Until(() => Combat.Enemies.Count == 0 || Combat.Enemies[0].hp < hull, 4f, "the extended gun to hit what its own range would not reach");
        }

        [UnityTest]
        public IEnumerator AnOlderProfileIsCarriedOverAndTheOptionsAreKept()
        {
            yield return SceneManager.LoadSceneAsync("Lobby");
            yield return null;
            yield return null;
            var config = Combat.Config;
            var lobby = UnityEngine.Object.FindAnyObjectByType<LobbyRoot>();

            // A profile from before parts were items, kept where profiles used to be kept. Its slots
            // still have their old names.
            Profile.Erase();
            PlayerPrefs.SetString(Profile.Key, "{\"version\":2,\"beats\":77,\"current\":0,"
                + "\"found\":[\"Autocannon\",\"Plating\",\"Splitter\",\"Rover\",\"Buggy\",\"ShardReactor\",\"Wheels\"],\"fresh\":[],"
                + "\"markedFrames\":[\"Rover\"],\"marks\":[1],\"machines\":[{\"name\":\"Old\",\"frame\":\"Rover\",\"reactor\":\"ShardReactor\","
                + "\"drive\":\"Wheels\",\"slots\":[\"turret\",\"bay1\",\"supportA\"],\"modules\":[\"Autocannon\",\"Splitter\",\"Plating\"]}]}");
            Profile.Load(config);
            Assert.AreEqual(77, Profile.Beats);
            Assert.AreEqual("Old", Profile.Machine.displayName);
            Assert.AreEqual("Rover", Profile.Machine.frame.name);
            Assert.AreEqual(1, Profile.MarkOf(Profile.Machine.frame));
            Assert.AreEqual("Splitter", Profile.Machine.In("system1").name, "a bay is a system slot now");
            Assert.AreEqual("Plating", Profile.Machine.In("armor1").name, "a support slot is an armor slot now");
            Assert.IsNotNull(Profile.Machine.ItemIn("turret"), "what it had found has become items");
            Assert.IsNotNull(Profile.Items.Find(i => i.def.name == "Extender"), "and what the game has gained since is handed over");
            string file = System.IO.Path.Combine(Application.persistentDataPath, Profile.Key + ".json");
            Assert.IsTrue(System.IO.File.Exists(file), "it is a file from now on");
            Assert.IsFalse(PlayerPrefs.HasKey(Profile.Key), "and the old place is empty");

            // The options are kept apart from the profile, and take effect as they are changed.
            var shake = Options.All[0];
            int was = shake.get();
            lobby.Pause.Open("The Roundhouse");
            lobby.Pause.ShowOptions(true);
            yield return null;
            Assert.IsTrue(lobby.Pause.InOptions);
            Options.Step(shake, 1);
            Assert.AreEqual((was + 1) % shake.choices.Length, shake.get());
            while (shake.get() != 0) Options.Step(shake, 1);
            Assert.AreEqual(0f, Options.Shake, "shake switched off");
            while (shake.get() != was) Options.Step(shake, 1);
            lobby.Pause.ShowOptions(false);
            lobby.Pause.Close();
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator TheTeleporterTakesTheMachineHomeUnlessItIsHit()
        {
            yield return SceneManager.LoadSceneAsync("Arena");
            yield return null;
            var recall = Combat.Recall;
            var rig = Combat.Rig;
            Rig.MoveOverride = Vector2.zero;

            Assert.IsTrue(recall.Begin());
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                // Nothing may wander up and hit the machine before the test does.
                Combat.Enemies.Clear();
                yield return null;
            }
            Assert.IsTrue(recall.Charging);
            Assert.Greater(recall.Progress, 0.15f);

            // A hit breaks the charge, and the teleporter needs a moment before it can start again.
            rig.TakeHit(5f, rig.transform.position + Vector3.right * 2f);
            yield return null;
            Assert.IsFalse(recall.Charging);
            Assert.IsTrue(recall.Broken);
            Assert.IsFalse(recall.Begin());
            yield return new WaitForSeconds(1.7f);

            // Left alone, it takes the machine home, and what the machine carries becomes the player's.
            RunStats.Beats = 7;
            int beats = Profile.Beats;
            Assert.IsTrue(recall.Begin());
            float waited = 0f;
            while (SceneManager.GetActiveScene().name != "Lobby")
            {
                if (Combat.Enemies != null) Combat.Enemies.Clear();
                waited += Time.unscaledDeltaTime;
                if (waited > 12f) Assert.Fail("Waited 12 s for the teleporter to take the machine home");
                yield return null;
            }
            yield return null;
            var lobby = UnityEngine.Object.FindAnyObjectByType<LobbyRoot>();
            var character = UnityEngine.Object.FindAnyObjectByType<LobbyCharacter>(FindObjectsInactive.Include);
            Assert.IsNotNull(lobby);
            Assert.IsNotNull(character);
            Assert.IsTrue(lobby.Busy, "the machine arrives before the player may walk");
            Assert.IsFalse(character.gameObject.activeSelf, "the character is still inside the machine");
            yield return Until(() => !lobby.Busy, 8f, "the arrival to end");
            Assert.IsTrue(character.gameObject.activeSelf);
            Assert.IsTrue(character.enabled);
            Assert.Less(character.transform.position.magnitude, 5f, "the character jumped down next to the machine");
            Assert.Greater(character.transform.position.magnitude, 3f, "and stands clear of the deck");
            Assert.AreEqual(beats + 7, Profile.Beats, "what the machine carried came home with it");
        }

        [UnityTest]
        public IEnumerator PausingStopsTheRunAndADestroyedMachineBringsUpTheEndScreen()
        {
            yield return SceneManager.LoadSceneAsync("Arena");
            yield return null;
            var game = UnityEngine.Object.FindAnyObjectByType<GameRoot>();
            Rig.MoveOverride = new Vector2(0f, 1f);
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(Pointer.Shown, "no pointer while driving");

            game.Pause.Open("Test");
            Vector3 held = Combat.Rig.transform.position;
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(held, Combat.Rig.transform.position, "nothing moves while paused");
            Assert.IsTrue(Pointer.Shown, "the menu brings the pointer back");
            game.Pause.Close();
            yield return null;
            yield return null;
            Assert.IsFalse(Pointer.Shown);

            // What a destroyed machine carried is lost.
            RunStats.Beats = 12;
            int had = Profile.Beats;
            Combat.Rig.TakeHit(100000f, Combat.Rig.transform.position + Vector3.forward, "The test");
            Assert.IsFalse(Combat.Rig.Alive);
            yield return Until(() => game.Over, 5f, "the end screen");
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.IsTrue(Pointer.Shown, "the end screen has a button to click");
            Assert.AreEqual(had, Profile.Beats, "the Beats went down with the machine");
            Assert.AreEqual("The test", RunStats.LastHitBy);
        }

        // ---- Round 11: Echoes, cold, new weapons, things to find, the Codex ----

        static EchoDef EchoOf(EchoKind kind) => Array.Find(Combat.Config.echoes, e => e.kind == kind);

        // A machine made for a test: a frame, and what goes into which of its slots.
        static Loadout Machine(string frame, params (string slot, string module)[] parts)
        {
            var config = Combat.Config;
            var loadout = ScriptableObject.CreateInstance<Loadout>();
            loadout.name = loadout.displayName = "Test machine";
            loadout.frame = Array.Find(config.frames, f => f.name == frame);
            loadout.reactor = config.reactors[0];
            loadout.drive = config.drives[0];
            loadout.modules = Array.ConvertAll(parts, p => new Loadout.Entry { slot = p.slot, module = Array.Find(config.modules, m => m.name == p.module) });
            foreach (var entry in loadout.modules) Assert.IsNotNull(entry.module, "the game has a part of that name");
            return loadout;
        }

        [UnityTest]
        public IEnumerator EchoesAreOfferedTakenAndBendTheRules()
        {
            yield return SceneManager.LoadSceneAsync("Arena");
            yield return null;
            var root = UnityEngine.Object.FindAnyObjectByType<GameRoot>();
            var config = Combat.Config;
            var rig = Combat.Rig;
            Rig.MoveOverride = Vector2.zero;
            Assert.AreEqual(12, config.echoes.Length);
            Assert.AreEqual(0, Echoes.Held.Count, "a run starts with none");

            // The machine has earned a choice: the run stops and three cards come up.
            Echoes.Owed = 1;
            yield return Until(() => root.Echo.IsOpen, 3f, "the cards to come up");
            Assert.AreEqual(0f, Time.timeScale, "the run stands still while the player chooses");
            Assert.AreEqual(3, root.Echo.Offered.Count);
            foreach (var offered in root.Echo.Offered)
            {
                Assert.IsNull(offered.joinsAfter, "the Echo of a strand is not offered before its dungeon has been cleared");
                Assert.AreNotEqual(EchoKind.LongWire, offered.kind, "a Long Wire is no use to a machine whose system reaches its one weapon");
            }
            var first = root.Echo.Offered[0];
            root.Echo.Pick(0);
            yield return null;
            Assert.IsFalse(root.Echo.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(0, Echoes.Owed);
            Assert.IsTrue(Echoes.Has(first.kind));
            Assert.IsTrue(Profile.Knows(Codex.Key(first)), "the Archivist hears of an Echo the first time it is carried");

            // Heavy Purse: a Beat counts double.
            Echoes.Reset();
            root.TakeEcho(EchoOf(EchoKind.HeavyPurse));
            var beat = Array.Find(config.drops.entries, e => e.kind == DropKind.Beat && e.amount == 1f);
            int carried = RunStats.Beats;
            Combat.Pickups.Spawn(beat, rig.transform.position);
            yield return Alone(1.6f);
            Assert.AreEqual(carried + 2, RunStats.Beats);

            // Undertow: a drop six metres off comes to a machine that stands still.
            root.TakeEcho(EchoOf(EchoKind.Undertow));
            Assert.AreEqual(Echoes.MagnetReach, rig.Magnet);
            carried = RunStats.Beats;
            Combat.Pickups.Spawn(beat, rig.transform.position + Vector3.forward * 6f);
            yield return Alone(3f);
            Assert.Greater(RunStats.Beats, carried);

            // Crescendo: a kill adds damage, and a hit takes it all away.
            root.TakeEcho(EchoOf(EchoKind.Crescendo));
            Assert.AreEqual(1f, Echoes.Damage);
            Combat.Enemies.Clear();
            int grunt = Combat.Enemies.Spawn(config.grunt, rig.transform.position + Vector3.right * 30f, aggro: false);
            Combat.Enemies.Hit(grunt, 999f, StateType.None, ignoreArmor: true);
            yield return null;
            yield return null;
            Assert.GreaterOrEqual(Echoes.Streak, 1);
            Assert.Greater(Echoes.Damage, 1f);
            rig.TakeHit(5f, rig.transform.position + Vector3.forward);
            Assert.AreEqual(0, Echoes.Streak);

            // With three held there is no room for another: an Echo on the ground is Beats.
            Assert.IsTrue(Echoes.Full);
            Combat.Pickups.Clear();
            carried = RunStats.Beats;
            Combat.Pickups.Give(DropKind.Echo, rig.transform.position);
            yield return Alone(1.6f);
            Assert.AreEqual(carried + Echoes.Spare, RunStats.Beats);
            Assert.IsFalse(root.Echo.IsOpen);

            // Encore: once, the blow that would destroy the machine does not.
            Echoes.Reset();
            root.TakeEcho(EchoOf(EchoKind.Encore));
            rig.TakeHit(99999f, rig.transform.position + Vector3.forward, "a test");
            Assert.IsTrue(rig.Alive);
            Assert.AreEqual(rig.MaxHull * Echoes.EncoreHull, rig.Hull, 0.5f);
            Assert.IsFalse(Echoes.EncoreReady);

            // Long Wire: only for a machine with a system that does not reach every weapon.
            var crawler = Machine("Crawler", ("front", "Autocannon"), ("rear", "Autocannon"), ("system1", "IncendiaryFeed"));
            Assert.IsTrue(Wiring.GainsFromLongWire(crawler));
            Assert.AreEqual(StateType.None, Wiring.Resolve(crawler, "rear").feed);
            Assert.AreEqual(StateType.Heat, Wiring.Resolve(crawler, "rear", longWire: true).feed);
            Assert.IsFalse(Wiring.GainsFromLongWire(Profile.Machine));

            // The pause menu says what the held Echoes do.
            root.Pause.Extra.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ColdReactsAndTheNewWeaponsFight()
        {
            yield return SceneManager.LoadSceneAsync("Arena");
            yield return null;
            var config = Combat.Config;
            var rig = Combat.Rig;
            var enemies = Combat.Enemies;
            Rig.MoveOverride = Vector2.zero;
            // An unarmed machine, so that only the test touches the enemies.
            rig.Build(Machine("Buggy"), config, refill: true);
            Vector3 far = rig.transform.position + new Vector3(40f, 0f, 20f);

            // Thermal Shock: Heat on something chilled cracks it, and what is cracked takes more damage.
            enemies.Clear();
            int a = enemies.Spawn(config.brute, far, aggro: false);
            enemies.Hit(a, 0f, StateType.Cryo);
            Assert.AreEqual(StateType.Cryo, enemies[a].state);
            enemies.Hit(a, 0f, StateType.Heat);
            yield return null;
            yield return null;
            Assert.AreEqual(1, RunStats.ThermalShocks);
            Assert.Greater(enemies[a].brittle, 0f);
            Assert.Less(enemies[a].hp, enemies[a].maxHp);
            float before = enemies[a].hp;
            enemies.Hit(a, 10f, StateType.None, ignoreArmor: true);
            Assert.AreEqual(10f * (1f + config.brittleBonus), before - enemies[a].hp, 0.01f);
            Assert.IsTrue(Profile.Knows(Codex.Key(ReactionType.ThermalShock)), "the Archivist hears of a reaction the first time it goes off");

            // Superconductor: lightning runs down a whole row.
            enemies.Clear();
            int b0 = enemies.Spawn(config.brute, far, aggro: false), b1 = enemies.Spawn(config.brute, far + Vector3.right * 4f, aggro: false);
            int b2 = enemies.Spawn(config.brute, far + Vector3.right * 8f, aggro: false);
            enemies.Hit(b0, 0f, StateType.Shock);
            enemies.Hit(b0, 0f, StateType.Cryo);
            yield return null;
            yield return null;
            Assert.AreEqual(1, RunStats.Superconductors);
            foreach (int index in new[] { b0, b1, b2 }) Assert.Less(enemies[index].hp, enemies[index].maxHp);

            // Black Ice: nothing keeps its footing, and they slide apart.
            enemies.Clear();
            int c0 = enemies.Spawn(config.brute, far, aggro: false), c1 = enemies.Spawn(config.brute, far + Vector3.right * 2.2f, aggro: false);
            enemies.Hit(c0, 0f, StateType.Oil);
            enemies.Hit(c0, 0f, StateType.Cryo);
            yield return null;
            yield return null;
            Assert.AreEqual(1, RunStats.BlackIces);
            Assert.Greater(enemies[c1].stun, 0f);
            Assert.Less(enemies[c1].hp, enemies[c1].maxHp);
            yield return new WaitForSeconds(0.6f);
            Assert.Greater((enemies[c1].pos - enemies[c0].pos).magnitude, 3.2f);
            Assert.AreEqual(3, Combat.ReactionCount);

            // A saw cuts what is right in front of the machine.
            Vector3 ahead = rig.transform.forward;
            enemies.Clear();
            rig.Build(Machine("Crawler", ("front", "SawRam")), config, refill: true);
            int target = enemies.Spawn(config.brute, rig.transform.position + ahead * 3.1f, aggro: false);
            yield return new WaitForSeconds(0.5f);
            Assert.Less(enemies[target].hp, enemies[target].maxHp);

            // A railgun goes through a whole row.
            enemies.Clear();
            rig.Build(Machine("Crawler", ("turret", "Railgun")), config, refill: true);
            int r0 = enemies.Spawn(config.brute, rig.transform.position + ahead * 8f, aggro: false);
            int r1 = enemies.Spawn(config.brute, rig.transform.position + ahead * 11f, aggro: false);
            int r2 = enemies.Spawn(config.brute, rig.transform.position + ahead * 14f, aggro: false);
            yield return new WaitForSeconds(0.6f);
            foreach (int index in new[] { r0, r1, r2 }) Assert.Less(enemies[index].hp, enemies[index].maxHp);
            Assert.AreEqual(StateType.Shock, enemies[r2].state);

            // A mortar's shell bursts on its target.
            enemies.Clear();
            Combat.Projectiles.Clear();
            rig.Build(Machine("Crawler", ("turret", "Mortar")), config, refill: true);
            target = enemies.Spawn(config.brute, rig.transform.position + ahead * 12f, aggro: false);
            yield return Until(() => enemies.Count > target && enemies[target].hp < enemies[target].maxHp, 4f, "the shell to land");

            // Mines are laid behind the machine, and go off under what follows it.
            enemies.Clear();
            Combat.Projectiles.Clear();
            rig.Build(Machine("Crawler", ("rear", "MineLayer")), config, refill: true);
            float dealt = RunStats.DamageDealt;
            enemies.Spawn(config.brute, rig.transform.position - ahead * 7f);
            int laid = 0;
            for (float t = 0f; t < 6f && RunStats.DamageDealt <= dealt; t += Time.deltaTime)
            {
                laid = Mathf.Max(laid, Combat.Projectiles.Mines);
                rig.Repair();
                yield return null;
            }
            Assert.Greater(laid, 0, "a mine was laid");
            Assert.Greater(RunStats.DamageDealt, dealt, "and went off under the Brute that walked onto it");
        }

        [UnityTest]
        public IEnumerator VeinsBreakAndCachesOpen()
        {
            // The same layout every time, so that the way to a vein is the same every time.
            GameRoot.FindSeed = 7;
            yield return SceneManager.LoadSceneAsync("Arena");
            yield return null;
            var rig = Combat.Rig;
            var finds = Combat.Finds;
            Rig.MoveOverride = Vector2.zero;
            int veins = finds.VeinsLeft, caches = finds.CachesLeft;
            Assert.GreaterOrEqual(veins, 20, "veins stand all over the map");
            Assert.GreaterOrEqual(caches, 4, "and a few caches");

            // Driving into a vein breaks it, and its Beats fly out.
            Assert.IsTrue(finds.NearestVein(rig.transform.position, out var vein));
            rig.Teleport(vein + Vector3.back * 4f);
            rig.transform.rotation = Quaternion.identity;
            Combat.Camera.Snap();
            Combat.Pickups.Clear();
            Rig.MoveOverride = new Vector2(0f, 1f);
            for (float t = 0f; t < 5f && finds.VeinsLeft == veins; t += Time.deltaTime)
            {
                Combat.Enemies.Clear();
                yield return null;
            }
            Rig.MoveOverride = Vector2.zero;
            Assert.AreEqual(veins - 1, finds.VeinsLeft);
            Assert.AreEqual(1, RunStats.Veins);
            Assert.GreaterOrEqual(Combat.Pickups.Count, 3, "a vein holds three to five Beats");

            // A cache opens for a machine that stays next to it, and the Strays around hear it.
            Assert.IsTrue(finds.NearestCache(rig.transform.position, out var cache));
            Combat.Enemies.Clear();
            Combat.Pickups.Clear();
            rig.Teleport(cache + Vector3.back * 3f);
            Combat.Camera.Snap();
            yield return Until(() => finds.Opening > 0.03f, 3f, "the cache to start opening");
            Assert.GreaterOrEqual(Combat.Enemies.Count, 5, "opening it rouses what lives there");
            Assert.Less((rig.transform.position - cache).magnitude, 4.2f);
            Assert.Greater((rig.transform.position - cache).magnitude, 2f, "a cache is solid");
            for (float t = 0f; t < 10f && finds.CachesLeft == caches; t += Time.deltaTime)
            {
                Combat.Enemies.Clear();
                yield return null;
            }
            Assert.AreEqual(caches - 1, finds.CachesLeft);
            Assert.AreEqual(1, RunStats.Caches);
            Assert.Less(finds.Opening, 0f);
            Assert.GreaterOrEqual(Combat.Pickups.Count, 7, "a part, Beats and a heart");
        }

        [UnityTest]
        public IEnumerator TheArchivistPaysForWhatIsNewToIt()
        {
            yield return SceneManager.LoadSceneAsync("Lobby");
            yield return null;
            yield return null;
            var lobby = UnityEngine.Object.FindAnyObjectByType<LobbyRoot>();
            var config = Combat.Config;
            Assert.AreEqual(0, Codex.Owed(config));

            // A reaction went off twice, a species was met, and so was its boss.
            Profile.Note(Codex.Key(ReactionType.Firestorm));
            Profile.Note(Codex.Key(ReactionType.Firestorm));
            var species = Codex.Strays(config)[0];
            Codex.Destroyed(species, Rank.Boss);
            Assert.AreEqual(Codex.ForReaction + Codex.ForSpecies + Codex.ForBoss, Codex.Owed(config));
            int purse = Profile.Beats;

            // With something owed the screen opens on the Codex, and looking at an entry gets it paid.
            lobby.OpenArchive();
            yield return null;
            Assert.IsTrue(lobby.Archive.InCodex);
            Assert.AreEqual(purse + Codex.ForReaction, Profile.Beats);
            Assert.AreEqual(2, Profile.Count(Codex.Key(ReactionType.Firestorm)));
            // A species and its boss are two steps of one entry.
            lobby.Archive.Show(Codex.Section.Strays, 0);
            yield return null;
            Assert.AreEqual(purse + Codex.ForReaction + Codex.ForSpecies + Codex.ForBoss, Profile.Beats);
            lobby.Archive.Show(Codex.Section.Strays, 0);
            Assert.AreEqual(purse + Codex.ForReaction + Codex.ForSpecies + Codex.ForBoss, Profile.Beats, "nothing is paid twice");
            Assert.AreEqual(0, Codex.Owed(config));

            // Every page of it can be looked at.
            for (int section = 0; section < Codex.Sections; section++)
                for (int item = 0; item < 12; item++)
                {
                    lobby.Archive.Show((Codex.Section)section, item);
                    yield return null;
                }
            Assert.AreEqual(12, lobby.Archive.EntryCount, "twelve Echoes");
            lobby.Archive.ShowCodex(false);
            yield return null;
            Assert.IsFalse(lobby.Archive.InCodex);
            lobby.CloseArchive();
            yield return null;
        }
    }
}
