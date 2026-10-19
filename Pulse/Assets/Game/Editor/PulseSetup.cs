using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Pulse.EditorTools
{
    // Creates the prototype content from machine-concept.md, section 7, and the test arena scene.
    // Assets that already exist are left alone, so values tuned in the Inspector survive a second run.
    public static class PulseSetup
    {
        const string Root = "Assets/Game", Data = Root + "/Data", Materials = Root + "/Art/Materials";
        const string ScenePath = Root + "/Scenes/Arena.unity";
        const string ConfigPath = Data + "/GameConfig.asset", PanelPath = Root + "/UI/PulsePanel.asset";
        const string LobbyScenePath = Root + "/Scenes/Lobby.unity", Models = Root + "/Art/Models";

        // The maps of the dungeons, as they are drawn. DungeonMap says what each character means.
        const string CinderPitMap = @"#########################
#########.......#########
#########...s...#########
#########.......#########
####.................####
####......o...o......####
####.................####
#.......................#
#..s......v...v......s..#
#...........@...........#
#..s......v...v......s..#
#.......................#
####.................####
####......o...o......####
####.................####
#########.......#########
#########...s...#########
#########.......#########
#########################";

        const string RimeVaultMap = @"###################################
#.....s.....#.........#.....s.....#
#...........#.........#...........#
#...........#....v....#...........#
#....o......................o.....#
#.................................#
#.........o.....o.....o...........#
#.s.............@...............s.#
#.........o.....o.....o...........#
#.................................#
#....o......................o.....#
#...........#....v....#...........#
#...........#.........#...........#
#.....s.....#.........#.....s.....#
###################################";

        const string TarSumpMap = @"###############################
#..............s..............#
#.............................#
#.............................#
#....#########...#########....#
#....#...................#....#
#....#..v.............v..#....#
#....#...................#....#
#..........o.........o........#
#s.............@.............s#
#..........o.........o........#
#....#...................#....#
#....#..v.............v..#....#
#....#...................#....#
#....#########...#########....#
#.............................#
#.............................#
#..............s..............#
###############################";

        const string KilnsKeepMap = @"     ccccccccccccc     ccccccccccc
   ccc############GGGGG############ccc
  cc###...........................###cc
 cc##...............................##cc
 c##.................................##c
cc#...................................#cc
c##.....o.......v...B...v.......o.....##c
c#.....................................#c
c#.....................................#c
c#..........v...............v..........#c
c#.....................................#c
c#.....................................#c
c##.....o.......v.......v.......o.....##c
cc#...................................#cc
 c##.................................##c
 cc##...............@...............##cc
  cc###...........................###cc
   ccc#############################ccc
     ccccccccccccccccccccccccccccc";

        [MenuItem("Pulse/Create Default Content")]
        public static void CreateAll()
        {
            foreach (string folder in new[]
            {
                Data + "/Weapons", Data + "/Systems", Data + "/Support", Data + "/Enemies", Data + "/Abilities",
                Data + "/Machine", Data + "/Loadouts", Data + "/World", Data + "/Echoes", Materials, Root + "/Scenes",
            })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();

            // ---- Weapons ----
            var autocannon = Asset<WeaponDef>(Data + "/Weapons/Autocannon.asset", w =>
            {
                Describe(w, "Autocannon", "Fast kinetic shots.", 2);
                w.delivery = Delivery.Projectile; w.damage = 8f; w.cooldown = 0.25f; w.range = 16f; w.projectileSpeed = 34f;
            });
            var flamethrower = Asset<WeaponDef>(Data + "/Weapons/Flamethrower.asset", w =>
            {
                Describe(w, "Flamethrower", "Short cone of fire. Applies Heat to everything it touches.", 2);
                w.delivery = Delivery.Beam; w.state = StateType.Heat; w.damage = 3f; w.cooldown = 0.15f; w.range = 5.5f; w.coneHalfAngle = 16f;
            });
            var oilSprayer = Asset<WeaponDef>(Data + "/Weapons/OilSprayer.asset", w =>
            {
                Describe(w, "Oil Sprayer", "Drops a pool of oil that slows enemies and waits for a spark.", 2);
                w.delivery = Delivery.Area; w.state = StateType.Oil; w.damage = 0f; w.cooldown = 2.2f; w.range = 14f; w.radius = 2f; w.zoneDuration = 7f;
            });
            var teslaCoil = Asset<WeaponDef>(Data + "/Weapons/TeslaCoil.asset", w =>
            {
                Describe(w, "Tesla Coil", "Pulses around the machine. Applies Shock.", 2);
                w.delivery = Delivery.Area; w.state = StateType.Shock; w.damage = 6f; w.cooldown = 1.1f; w.range = 4f; w.radius = 4f; w.anchoredToRig = true;
            });

            // Weapons that are not there from the start: dungeons keep them.
            var longshot = Asset<WeaponDef>(Data + "/Weapons/Longshot.asset", w =>
            {
                Describe(w, "Longshot", "One heavy kinetic slug at a time, from far away.", 3);
                w.delivery = Delivery.Projectile; w.damage = 34f; w.cooldown = 1.3f; w.range = 26f; w.projectileSpeed = 60f;
            });
            var frostJet = Asset<WeaponDef>(Data + "/Weapons/FrostJet.asset", w =>
            {
                Describe(w, "Frost Jet", "Short cone of freezing air. Applies Cryo to everything it touches.", 2);
                w.delivery = Delivery.Beam; w.state = StateType.Cryo; w.damage = 2.5f; w.cooldown = 0.15f; w.range = 6f; w.coneHalfAngle = 18f;
            });

            // Round 11: four more weapons, each a way of fighting of its own.
            var mortar = Asset<WeaponDef>(Data + "/Weapons/Mortar.asset", w =>
            {
                Describe(w, "Mortar", "Lobs a shell that bursts where it lands. Slow, and made for crowds.", 3);
                w.delivery = Delivery.Area; w.shell = true; w.damage = 30f; w.cooldown = 2.4f; w.range = 19f; w.radius = 2.7f; w.zoneDuration = 3.5f;
            });
            var mineLayer = Asset<WeaponDef>(Data + "/Weapons/MineLayer.asset", w =>
            {
                Describe(w, "Mine Layer", "Lays mines that wait for something to walk onto them.", 2);
                w.delivery = Delivery.Deployable; w.damage = 38f; w.cooldown = 1.5f; w.range = 15f; w.radius = 2.5f; w.zoneDuration = 22f;
            });
            var sawRam = Asset<WeaponDef>(Data + "/Weapons/SawRam.asset", w =>
            {
                Describe(w, "Saw Ram", "Cuts whatever is right in front of it. The faster the machine, the deeper the cut.", 2);
                w.delivery = Delivery.Contact; w.damage = 7f; w.cooldown = 0.2f; w.range = 2.5f; w.coneHalfAngle = 55f;
            });
            var railgun = Asset<WeaponDef>(Data + "/Weapons/Railgun.asset", w =>
            {
                Describe(w, "Railgun", "One bolt that goes through everything in its way. Applies Shock.", 3);
                w.delivery = Delivery.Projectile; w.state = StateType.Shock; w.damage = 24f; w.cooldown = 1.7f; w.range = 28f; w.projectileSpeed = 70f; w.pierce = 5;
            });

            // ---- Systems ----
            var splitter = Asset<SystemDef>(Data + "/Systems/Splitter.asset", s =>
            {
                Describe(s, "Splitter", "Wired weapons split: shots fork, beams refract, zones gain a ring.", 1);
                s.kind = SystemKind.Shaper; s.shaper = Shapers.Splitter;
            });
            var incendiaryFeed = Asset<SystemDef>(Data + "/Systems/IncendiaryFeed.asset", s =>
            {
                Describe(s, "Incendiary Feed", "Wired Kinetic weapons and triggers apply Heat.", 1);
                s.kind = SystemKind.Feed; s.feedState = StateType.Heat;
            });

            var arcFeed = Asset<SystemDef>(Data + "/Systems/ArcFeed.asset", s =>
            {
                Describe(s, "Arc Feed", "Wired Kinetic weapons and triggers apply Shock.", 1);
                s.kind = SystemKind.Feed; s.feedState = StateType.Shock;
            });

            var cryoFeed = Asset<SystemDef>(Data + "/Systems/CryoFeed.asset", s =>
            {
                Describe(s, "Cryo Feed", "Wired Kinetic weapons and triggers apply Cryo.", 1);
                s.kind = SystemKind.Feed; s.feedState = StateType.Cryo;
            });
            var oilFeed = Asset<SystemDef>(Data + "/Systems/OilFeed.asset", s =>
            {
                Describe(s, "Oil Feed", "Wired Kinetic weapons and triggers apply Oil.", 1);
                s.kind = SystemKind.Feed; s.feedState = StateType.Oil;
            });

            // A system that changes nothing about a hit and only lets it reach further.
            var extender = Asset<SystemDef>(Data + "/Systems/Extender.asset", s =>
            {
                Describe(s, "Extender", "What it is wired to reaches 40 % further.", 1);
                s.kind = SystemKind.Booster; s.rangeBonus = 0.4f;
            });

            // ---- Armor ----
            var heavyPlating = Asset<SupportDef>(Data + "/Support/HeavyPlating.asset", s =>
            {
                Describe(s, "Heavy Plating", "Thick plate. Takes more off every hit and adds hull, for twice the Power.", 2);
                s.armor = 3f; s.hull = 30f;
            });
            var plating = Asset<SupportDef>(Data + "/Support/Plating.asset", s =>
            {
                Describe(s, "Plating", "Takes a flat amount off every hit.", 1);
                s.armor = 2f;
            });
            var reactivePlating = Asset<SupportDef>(Data + "/Support/ReactivePlating.asset", s =>
            {
                Describe(s, "Reactive Plating", "Taking a hit sets off a burst around the machine.", 1);
                s.armor = 1f; s.trigger = TriggerType.BurstWhenHit; s.triggerDamage = 15f; s.triggerRadius = 3f; s.triggerCooldown = 1.5f;
            });

            // ---- Enemies ----
            var grunt = Asset<EnemyDef>(Data + "/Enemies/Grunt.asset", e =>
            {
                e.displayName = "Grunt"; e.hp = 20f; e.speed = 3.6f; e.radius = 0.5f; e.contactDamage = 5f;
            });
            var brute = Asset<EnemyDef>(Data + "/Enemies/Brute.asset", e =>
            {
                e.displayName = "Brute"; e.hp = 90f; e.speed = 2.4f; e.radius = 0.9f; e.armor = 3f; e.contactDamage = 12f;
            });

            // ---- Machine ----
            var crawler = Asset<FrameDef>(Data + "/Machine/Crawler.asset", f =>
            {
                f.displayName = "Crawler";
                f.slots = new[]
                {
                    Slot("front", SlotType.Hardpoint, Mount.Front, new Vector3(0f, 0.22f, 1f), 70f, 122f),
                    Slot("turret", SlotType.Hardpoint, Mount.Turret, new Vector3(0f, 0.22f, 0f), 320f, 122f),
                    Slot("rear", SlotType.Hardpoint, Mount.Rear, new Vector3(0f, 0.22f, -1f), 570f, 122f),
                    Slot("system1", SlotType.System, Mount.Turret, Vector3.zero, 190f, 122f),
                    Slot("system2", SlotType.System, Mount.Turret, Vector3.zero, 450f, 122f),
                    Slot("armor1", SlotType.Armor, Mount.Turret, new Vector3(-0.55f, 0.15f, 0.5f), 190f, 30f),
                    Slot("armor2", SlotType.Armor, Mount.Turret, new Vector3(0.55f, 0.15f, -0.5f), 450f, 30f),
                    Slot("armor3", SlotType.Armor, Mount.Turret, new Vector3(-0.55f, 0.15f, -0.5f), 320f, 206f),
                    Slot("drive", SlotType.Drive, Mount.Turret, Vector3.zero, 190f, 206f),
                };
                f.wires = new[]
                {
                    Wire("system1", "front"), Wire("system1", "turret"), Wire("system1", "armor1"),
                    Wire("system2", "turret"), Wire("system2", "rear"), Wire("system2", "armor2"),
                };
            });
            // The frame everyone starts with: one weapon, and one socket that takes a system or a support module.
            var buggy = Asset<FrameDef>(Data + "/Machine/Buggy.asset", f =>
            {
                f.displayName = "Buggy"; f.hull = 100f; f.bodySize = new Vector3(1.4f, 0.6f, 2.1f);
                f.description = "Small and light. One weapon, and one socket that takes a system or armor.";
                f.slots = new[]
                {
                    Slot("turret", SlotType.Hardpoint, Mount.Turret, new Vector3(0f, 0.22f, 0.25f), 230f, 104f),
                    Slot("socket", SlotType.Socket, Mount.Turret, new Vector3(0f, 0.15f, -0.6f), 410f, 104f),
                    Slot("drive", SlotType.Drive, Mount.Turret, Vector3.zero, 320f, 196f),
                };
                f.wires = new[] { Wire("socket", "turret") };
                f.marks = new[] { new FrameDef.Mark { cost = 60, hull = 20f }, new FrameDef.Mark { cost = 140, hull = 30f, armor = 1f } };
            });
            // The first frame worth buying: two weapons, a bay wired to both, and a support slot.
            var rover = Asset<FrameDef>(Data + "/Machine/Rover.asset", f =>
            {
                f.displayName = "Rover"; f.hull = 120f; f.bodySize = new Vector3(1.55f, 0.6f, 2.45f); f.price = 150;
                f.description = "Room for a second weapon. One system acts on both, and on the armor too.";
                f.slots = new[]
                {
                    Slot("front", SlotType.Hardpoint, Mount.Front, new Vector3(0f, 0.22f, 0.85f), 120f, 122f),
                    Slot("turret", SlotType.Hardpoint, Mount.Turret, new Vector3(0f, 0.22f, -0.05f), 520f, 122f),
                    Slot("system1", SlotType.System, Mount.Turret, Vector3.zero, 320f, 122f),
                    Slot("armor1", SlotType.Armor, Mount.Turret, new Vector3(0f, 0.15f, -0.85f), 320f, 30f),
                    Slot("drive", SlotType.Drive, Mount.Turret, Vector3.zero, 320f, 206f),
                };
                f.wires = new[] { Wire("system1", "front"), Wire("system1", "turret"), Wire("system1", "armor1") };
                f.marks = new[] { new FrameDef.Mark { cost = 120, hull = 25f }, new FrameDef.Mark { cost = 260, hull = 35f, armor = 1f } };
            });
            var reactor = Asset<ReactorDef>(Data + "/Machine/ShardReactor.asset", r =>
            {
                r.displayName = "Shard Reactor"; r.powerCap = 10;
            });
            var wheels = Asset<DriveDef>(Data + "/Machine/Wheels.asset", d =>
            {
                d.displayName = "Wheels"; d.speed = 7.5f; d.acceleration = 18f; d.turnRate = 85f;
            });

            // ---- Preset machines for testing combinations ----
            Loadout Preset(string file, string name, params (string slot, ModuleDef module)[] modules) =>
                Asset<Loadout>($"{Data}/Loadouts/{file}.asset", l =>
                {
                    l.displayName = name; l.frame = crawler; l.reactor = reactor; l.drive = wheels;
                    l.modules = Array.ConvertAll(modules, m => new Loadout.Entry { slot = m.slot, module = m.module });
                });

            var loadouts = new[]
            {
                Preset("1_Starter", "Starter", ("turret", autocannon), ("armor3", plating)),
                Preset("2_Burner", "Burner", ("front", flamethrower), ("turret", oilSprayer), ("system1", splitter), ("armor3", plating)),
                Preset("3_Storm", "Storm", ("front", teslaCoil), ("turret", autocannon), ("rear", oilSprayer), ("system1", incendiaryFeed), ("armor3", plating)),
                Preset("4_Reactive", "Reactive", ("turret", autocannon), ("rear", oilSprayer), ("system1", incendiaryFeed), ("armor1", reactivePlating)),
            };

            // ---- World: dungeons are wave fights, reached through entrances in the open world ----
            DungeonDef Dungeon(string file, string name, float radius, params (int grunts, int brutes)[] waves) =>
                Asset<DungeonDef>($"{Data}/World/{file}.asset", d =>
                {
                    d.displayName = name; d.arenaRadius = radius;
                    d.waves = Array.ConvertAll(waves, w => new DungeonDef.Wave { grunts = w.grunts, brutes = w.brutes });
                });

            var cinderPit = Dungeon("1_CinderPit", "Cinder Pit", 22f, (10, 0), (16, 0), (14, 2));
            var slagWorks = Dungeon("2_SlagWorks", "Slag Works", 24f, (16, 1), (22, 2), (20, 4), (26, 5));
            var kilnsGate = Dungeon("3_KilnsGate", "Kiln's Gate", 26f, (24, 3), (30, 5), (30, 8), (36, 10), (40, 12));
            // Each dungeon has a map drawn for it, and pays more Beats the harder it is.
            void Furnish(DungeonDef dungeon, string map, int reward)
            {
                if (!string.IsNullOrWhiteSpace(dungeon.map)) return;
                dungeon.map = map;
                dungeon.reward = reward;
                EditorUtility.SetDirty(dungeon);
            }
            Furnish(cinderPit, CinderPitMap, 40);
            Furnish(slagWorks, RimeVaultMap, 100);
            Furnish(kilnsGate, TarSumpMap, 180);
            // Each dungeon also keeps parts that are found nowhere else.
            void Keeps(DungeonDef dungeon, params ScriptableObject[] finds)
            {
                if (dungeon.finds != null && dungeon.finds.Length > 0) return;
                dungeon.finds = finds;
                EditorUtility.SetDirty(dungeon);
            }
            Keeps(cinderPit, longshot);
            Keeps(slagWorks, frostJet);
            Keeps(kilnsGate, arcFeed, heavyPlating);

            // What the Archivist has to say about the parts that are not modules.
            void Tell(ScriptableObject part, ref string field, string text)
            {
                if (!string.IsNullOrEmpty(field)) return;
                field = text;
                EditorUtility.SetDirty(part);
            }
            Tell(crawler, ref crawler.description, "A low, wide body on four wheels. Three hardpoints, two bays and three support slots.");
            Tell(reactor, ref reactor.description, "A splinter of the Pulse in a cage. Enough Power for a light machine.");
            Tell(wheels, ref wheels.description, "Four wheels. Quick in a straight line, but the machine has to roll to turn.");

            var world = Asset<WorldDef>(Data + "/World/CinderFlats.asset", w =>
            {
                w.entrances = new[]
                {
                    new WorldDef.Entrance { dungeon = cinderPit, position = new Vector3(0f, 0f, 40f), requires = -1 },
                    new WorldDef.Entrance { dungeon = slagWorks, position = new Vector3(-70f, 0f, -35f), requires = 0 },
                    new WorldDef.Entrance { dungeon = kilnsGate, position = new Vector3(80f, 0f, -75f), requires = 1 },
                };
            });

            // ---- Config ----
            var config = Asset<GameConfig>(ConfigPath, c =>
            {
                c.loadouts = loadouts; c.grunt = grunt; c.brute = brute;
                c.ground = Mat("Ground", Tokens.Plate);
                c.body = Mat("Body", Tokens.Chalk);
                c.part = Mat("Part", Tokens.Dust);
                c.enemy = Mat("Enemy", Tokens.Seam);
                c.heat = Mat("Heat", Tokens.Heat);
                c.shock = Mat("Shock", Tokens.Shock);
                c.cryo = Mat("Cryo", Tokens.Cryo);
                c.oil = Mat("Oil", Tokens.Oil);
                c.magnet = Mat("Magnet", Tokens.Magnet);
            });
            // ---- Models from Tools/blender/build_models.py, given to every asset that has none yet ----
            GiveMesh(autocannon, ref autocannon.mesh, "Autocannon");
            GiveMesh(flamethrower, ref flamethrower.mesh, "Flamethrower");
            GiveMesh(longshot, ref longshot.mesh, "Longshot");
            GiveMesh(frostJet, ref frostJet.mesh, "FrostJet");
            GiveMesh(mortar, ref mortar.mesh, "Mortar");
            GiveMesh(mineLayer, ref mineLayer.mesh, "MineLayer");
            GiveMesh(sawRam, ref sawRam.mesh, "SawRam");
            GiveMesh(railgun, ref railgun.mesh, "Railgun");
            GiveMesh(heavyPlating, ref heavyPlating.mesh, "SupportModule");
            GiveMesh(oilSprayer, ref oilSprayer.mesh, "OilSprayer");
            GiveMesh(teslaCoil, ref teslaCoil.mesh, "TeslaCoil");
            GiveMesh(plating, ref plating.mesh, "SupportModule");
            GiveMesh(reactivePlating, ref reactivePlating.mesh, "SupportModule");
            GiveMesh(grunt, ref grunt.mesh, "Grunt");
            GiveMesh(brute, ref brute.mesh, "Brute");
            GiveMesh(crawler, ref crawler.bodyMesh, "Crawler_Body");
            GiveMesh(crawler, ref crawler.wheelMesh, "Wheel");
            GiveMesh(grunt, ref grunt.legMesh, "Grunt_Leg");
            if (brute.legMesh == null) { brute.legSpacing = 0.4f; brute.hipHeight = 0.5f; }
            GiveMesh(brute, ref brute.legMesh, "Brute_Leg");

            // Fields added after the config was first created are filled in here.
            if (config.world == null) config.world = world;
            if (config.floor == null) config.floor = Mat("Floor", Tokens.Plate2);
            if (config.pulse == null) config.pulse = Mat("Pulse", Tokens.PulseGreen);
            if (config.particle == null) config.particle = Mat("Particle", Color.white, "Universal Render Pipeline/Particles/Unlit");
            // Ground zones are drawn with particles now; the materials they used to be filled with are gone.
            foreach (string unused in new[] { "Scorch", "OilPool", "Frost" }) AssetDatabase.DeleteAsset($"{Materials}/{unused}.mat");

            // State colours and the Pulse glow, so they bloom. Everything else stays matte.
            foreach (var material in new[] { config.heat, config.shock, config.cryo, config.oil, config.magnet, config.pulse })
                Glow(material, 1.6f);

            // ---- More materials: the extra slots of the models, enemies, and each biome ----
            if (config.dark == null) config.dark = Mat("Dark", new Color(0.085f, 0.095f, 0.115f), smoothness: 0.35f);
            if (config.flash == null) { config.flash = Mat("Flash", Color.white); Glow(config.flash, 2.5f); }
            if (config.enemyPlate == null) config.enemyPlate = Mat("EnemyPlate", new Color(0.34f, 0.19f, 0.16f));
            if (config.enemyGlow == null) { config.enemyGlow = Mat("EnemyGlow", new Color(1f, 0.82f, 0.55f)); Glow(config.enemyGlow, 2.2f); }
            GiveMesh(config, ref config.boltMesh, "Bolt");
            GiveMesh(config, ref config.globMesh, "Glob");

            var basalt = Mat("Basalt", new Color(0.15f, 0.15f, 0.17f));
            var cliff = Mat("Cliff", new Color(0.13f, 0.145f, 0.17f));
            var charred = Mat("Charred", new Color(0.09f, 0.07f, 0.065f));
            var husk = Mat("Husk", new Color(0.33f, 0.23f, 0.19f));
            var snow = Mat("Snow", new Color(0.62f, 0.67f, 0.74f));
            var ice = Mat("Ice", new Color(0.36f, 0.56f, 0.68f), smoothness: 0.75f);
            var frostRock = Mat("FrostRock", new Color(0.3f, 0.34f, 0.4f));
            var pine = Mat("Pine", new Color(0.13f, 0.24f, 0.2f));
            var tarRock = Mat("TarRock", new Color(0.13f, 0.12f, 0.17f), smoothness: 0.4f);
            var rust = Mat("Rust", new Color(0.36f, 0.22f, 0.14f));
            var reed = Mat("Reed", new Color(0.26f, 0.3f, 0.16f));
            var wall = Mat("Wall", new Color(0.24f, 0.2f, 0.19f));

            var dryBush = Mat("DryBush", new Color(0.3f, 0.2f, 0.12f));
            var frostBush = Mat("FrostBush", new Color(0.5f, 0.62f, 0.66f));
            var marshBush = Mat("MarshBush", new Color(0.16f, 0.27f, 0.15f));

            SceneryDef.Prop Prop(string name, string model, Material material, Material accent, int count,
                float minScale, float maxScale, float block = 0f, bool flat = false, float tilt = 0f, bool noShadow = false, bool crush = false) => new SceneryDef.Prop
            {
                name = name, mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{Models}/{model}.fbx"),
                material = material, accent = accent, count = count, minScale = minScale, maxScale = maxScale,
                blockRadius = block, flat = flat, tilt = tilt, noShadow = noShadow, crushable = crush,
            };
            var scenery = Asset<SceneryDef>(Data + "/World/Scenery.asset", s =>
            {
                s.biomes = new[]
                {
                    new SceneryDef.Biome
                    {
                        name = "Cinder Flats", glow = config.heat, ambient = new Color(0.37f, 0.33f, 0.35f),
                        ground = new Color(0.115f, 0.11f, 0.115f), groundAlt = new Color(0.17f, 0.135f, 0.12f), groundSpot = new Color(0.4f, 0.15f, 0.07f),
                        props = new[]
                        {
                            Prop("Lava crack", "LavaCrack", basalt, null, 46, 0.8f, 1.9f, flat: true),
                            Prop("Boulder", "Rock_A", basalt, null, 46, 0.8f, 2f, block: 0.95f),
                            Prop("Stone", "Rock_B", basalt, null, 80, 0.4f, 0.8f, tilt: 20f),
                            Prop("Pebbles", "Pebbles", basalt, null, 300, 0.6f, 1.5f, noShadow: true),
                            Prop("Burnt grass", "Tuft", charred, charred, 220, 0.7f, 1.4f, noShadow: true, crush: true),
                            Prop("Dry bush", "Bush", charred, dryBush, 180, 0.7f, 1.5f, crush: true),
                            Prop("Charred tree", "CharredTree", charred, charred, 54, 0.8f, 1.5f, block: 0.35f, tilt: 6f),
                            Prop("Ember vent", "EmberVent", basalt, null, 18, 0.8f, 1.3f, block: 0.95f),
                            Prop("Husk", "Husk", husk, config.part, 10, 0.9f, 1.25f, block: 1.7f, tilt: 5f),
                            Prop("Pylon", "Pylon", config.part, husk, 7, 1f, 1.3f, block: 0.5f, tilt: 4f),
                        },
                    },
                    new SceneryDef.Biome
                    {
                        name = "Frostline", glow = config.cryo, ambient = new Color(0.4f, 0.46f, 0.56f),
                        ground = new Color(0.4f, 0.45f, 0.53f), groundAlt = new Color(0.5f, 0.55f, 0.62f), groundSpot = new Color(0.27f, 0.42f, 0.55f),
                        props = new[]
                        {
                            Prop("Snow drift", "SnowDrift", snow, null, 70, 0.8f, 1.9f, noShadow: true),
                            Prop("Ice shards", "IceShards", frostRock, ice, 44, 0.7f, 1.6f, block: 0.75f),
                            Prop("Frozen pine", "FrozenPine", snow, pine, 62, 0.8f, 1.6f, block: 0.35f, tilt: 4f),
                            Prop("Ice boulder", "IceBoulder", snow, ice, 36, 0.7f, 1.6f, block: 0.85f),
                            Prop("Stone", "Rock_B", frostRock, null, 70, 0.4f, 0.8f, tilt: 20f),
                            Prop("Pebbles", "Pebbles", snow, null, 260, 0.6f, 1.5f, noShadow: true),
                            Prop("Ice splinters", "Shards", frostRock, ice, 200, 0.7f, 1.6f, noShadow: true, crush: true),
                            Prop("Frost bush", "Bush", frostRock, frostBush, 180, 0.7f, 1.5f, crush: true),
                            Prop("Husk", "Husk", frostRock, snow, 8, 0.9f, 1.25f, block: 1.7f, tilt: 5f),
                            Prop("Pylon", "Pylon", config.part, frostRock, 6, 1f, 1.3f, block: 0.5f, tilt: 4f),
                        },
                    },
                    new SceneryDef.Biome
                    {
                        name = "Tar Marsh", glow = config.oil, ambient = new Color(0.3f, 0.3f, 0.38f),
                        ground = new Color(0.09f, 0.085f, 0.12f), groundAlt = new Color(0.13f, 0.14f, 0.11f), groundSpot = new Color(0.2f, 0.12f, 0.3f),
                        props = new[]
                        {
                            Prop("Tar pool", "TarPool", tarRock, null, 50, 1f, 2.4f, flat: true),
                            Prop("Derrick", "Derrick", rust, config.part, 13, 0.9f, 1.2f, block: 1.3f),
                            Prop("Pipes", "PipeCluster", tarRock, rust, 28, 0.8f, 1.3f, block: 0.75f),
                            Prop("Reeds", "Reeds", tarRock, reed, 190, 0.7f, 1.4f, noShadow: true, crush: true),
                            Prop("Marsh grass", "Tuft", reed, reed, 200, 0.8f, 1.6f, noShadow: true, crush: true),
                            Prop("Marsh bush", "Bush", tarRock, marshBush, 180, 0.7f, 1.5f, crush: true),
                            Prop("Boulder", "Rock_A", tarRock, null, 32, 0.7f, 1.8f, block: 0.95f),
                            Prop("Stone", "Rock_B", tarRock, null, 70, 0.4f, 0.8f, tilt: 20f),
                            Prop("Pebbles", "Pebbles", tarRock, null, 260, 0.6f, 1.5f, noShadow: true),
                            Prop("Husk", "Husk", rust, config.part, 10, 0.9f, 1.25f, block: 1.7f, tilt: 5f),
                            Prop("Pylon", "Pylon", config.part, rust, 6, 1f, 1.3f, block: 0.5f, tilt: 4f),
                        },
                    },
                };
                s.border = Prop("Cliff", "Cliff", cliff, null, 0, 1f, 1.8f);
                s.pillar = Prop("Pillar", "Pillar", wall, config.part, 0, 1f, 1f);
                s.gate = AssetDatabase.LoadAssetAtPath<Mesh>($"{Models}/Gate.fbx");
            });
            // An older scenery asset without biomes is replaced.
            if (config.scenery == null || config.scenery.biomes.Length == 0) config.scenery = scenery;
            AssetDatabase.DeleteAsset(Data + "/World/CinderFlatsScenery.asset");
            scenery = config.scenery;

            // Version 2: rock and ice are hard, so the machine bounces off them instead of shaking them.
            // Snow drifts block like the mounds they look like, and small stones are ground to gravel.
            if (scenery.version < 2)
            {
                foreach (var biome in scenery.biomes)
                    for (int p = 0; p < biome.props.Length; p++)
                    {
                        ref var prop = ref biome.props[p];
                        string model = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(prop.mesh));
                        if (model == "Rock_A" || model == "EmberVent" || model == "IceShards" || model == "IceBoulder") prop.hard = true;
                        if (model == "Rock_B") prop.crushable = true;
                        if (model != "SnowDrift") continue;
                        prop.blockRadius = 1.45f; prop.hard = true; prop.noShadow = false; prop.count = 44;
                    }
                scenery.version = 2;
                EditorUtility.SetDirty(scenery);
            }

            // Version 3: dungeons are built from maps, so there is a wall block and a stand block,
            // and each biome says what the vents under it throw up.
            if (scenery.version < 3)
            {
                var hazards = new[] { StateType.Heat, StateType.Cryo, StateType.Oil };
                for (int b = 0; b < scenery.biomes.Length && b < hazards.Length; b++) scenery.biomes[b].hazard = hazards[b];
                scenery.wall = Prop("Dungeon wall", "DungeonWall", wall, config.part, 0, 1f, 1f);
                scenery.stand = Prop("Arena stand", "ArenaStand", wall, config.part, 0, 1f, 1f);
                scenery.version = 3;
                EditorUtility.SetDirty(scenery);
            }

            // ---- Species: three per biome, each with a mini boss and a boss form ----
            var cinderHide = husk;
            var frostHide = ice;
            var tarHide = Mat("TarHide", new Color(0.24f, 0.18f, 0.33f), smoothness: 0.5f);
            // Bodies are chosen to stand out from the floor of their biome: light on the dark flats and
            // the dark marsh, dark on the pale snow.
            var cinderBody = Mat("CinderBody", new Color(0.47f, 0.4f, 0.36f));
            var frostBody = Mat("FrostBody", new Color(0.2f, 0.25f, 0.33f));
            var tarBody = Mat("TarBody", new Color(0.44f, 0.38f, 0.52f), smoothness: 0.4f);
            EnemyDef Species(string file, string model, Behaviour behaviour, Material plate, Material glow, Action<EnemyDef> init)
            {
                var def = Asset<EnemyDef>($"{Data}/Enemies/{file}.asset", e =>
                {
                    e.behaviour = behaviour; e.plate = plate; e.glow = glow;
                    init(e);
                });
                if (def.body == null)
                {
                    def.body = plate == cinderHide ? cinderBody : plate == frostHide ? frostBody : tarBody;
                    EditorUtility.SetDirty(def);
                }
                GiveMesh(def, ref def.mesh, model);
                if (!def.wobble) GiveMesh(def, ref def.legMesh, model + "_Leg");
                return def;
            }
            void Named(EnemyDef e, string name, string miniBoss, string boss, string trait, string bossTrait)
            {
                e.displayName = name; e.miniBossName = miniBoss; e.bossName = boss; e.trait = trait; e.bossTrait = bossTrait;
            }

            var cinderSpecies = new[]
            {
                Species("CinderMite", "Mite", Behaviour.Swarm, cinderHide, config.heat, e =>
                {
                    Named(e, "Cinder Mite", "Mite Matron", "Brood Mother", "Weak alone, but it comes in packs.", "Keeps hatching mites until it is put down.");
                    e.hp = 9f; e.speed = 4.6f; e.radius = 0.32f; e.contactDamage = 2f; e.packSize = 4;
                    e.height = 0.45f; e.legSpacing = 0.2f; e.hipHeight = 0.2f;
                }),
                Species("SlagRam", "Ram", Behaviour.Charger, cinderHide, config.heat, e =>
                {
                    Named(e, "Slag Ram", "Slag Breaker", "Furnace Ram", "Shows a line, then rams along it.", "Its ram ends in a shockwave and leaves fire behind.");
                    e.hp = 55f; e.speed = 3.2f; e.radius = 0.75f; e.armor = 1f; e.contactDamage = 7f;
                    e.height = 1.3f; e.legSpacing = 0.36f; e.hipHeight = 0.4f;
                }),
                Species("EmberSpitter", "Spitter", Behaviour.Spitter, cinderHide, config.heat, e =>
                {
                    Named(e, "Ember Spitter", "Ember Mortar", "Kiln Maw", "Keeps its distance and lobs fire.", "Lobs five globs of fire at once.");
                    e.hp = 28f; e.speed = 3f; e.radius = 0.55f; e.contactDamage = 6f;
                    e.height = 1.3f; e.legSpacing = 0.26f; e.hipHeight = 0.4f;
                }),
            };
            var frostSpecies = new[]
            {
                Species("RimeStalker", "Stalker", Behaviour.Skirmisher, frostHide, config.cryo, e =>
                {
                    Named(e, "Rime Stalker", "Rime Hunter", "Whiteout", "Circles, darts in and backs off.", "Darts twice as often. Its hits chill and it leaves frost.");
                    e.hp = 26f; e.speed = 5.2f; e.radius = 0.5f; e.contactDamage = 5f;
                    e.height = 1f; e.legSpacing = 0.2f; e.hipHeight = 0.45f;
                }),
                Species("FrostHulk", "Hulk", Behaviour.Slammer, frostHide, config.cryo, e =>
                {
                    Named(e, "Frost Hulk", "Frost Crusher", "Glacier", "Slow and tough. Pounds the ground around it.", "A huge slam that slows, and frost breaks out around it.");
                    e.hp = 110f; e.speed = 2.1f; e.radius = 1f; e.armor = 2f; e.contactDamage = 14f;
                    e.height = 2f; e.legSpacing = 0.42f; e.hipHeight = 0.55f;
                }),
                Species("ShardLancer", "Lancer", Behaviour.Lancer, frostHide, config.cryo, e =>
                {
                    Named(e, "Shard Lancer", "Shard Volley", "Icicle Battery", "Stands off and shoots along the line it shows.", "Fires a fan of five, twice in a row.");
                    e.hp = 24f; e.speed = 2.8f; e.radius = 0.5f; e.contactDamage = 8f;
                    e.height = 1.6f; e.legSpacing = 0.14f; e.hipHeight = 0.5f;
                }),
            };
            var tarSpecies = new[]
            {
                Species("TarLeech", "Leech", Behaviour.Leech, tarHide, config.oil, e =>
                {
                    Named(e, "Tar Leech", "Tar Lamprey", "Mire Tyrant", "Fast. Its bite slows the machine.", "Leaves oil where it goes and drags the machine to it.");
                    e.hp = 22f; e.speed = 5f; e.radius = 0.45f; e.contactDamage = 3f; e.height = 0.6f; e.wobble = true;
                }),
                Species("Bloater", "Bloater", Behaviour.Bloater, tarHide, config.oil, e =>
                {
                    Named(e, "Bloater", "Tar Bloat", "Sump Heart", "Bursts next to the machine and leaves oil.", "Throws oil around the machine and bursts into more of its kind.");
                    e.hp = 45f; e.speed = 2.6f; e.radius = 0.7f; e.contactDamage = 12f; e.height = 1.4f; e.wobble = true;
                }),
                Species("Ooze", "Ooze", Behaviour.Splitter, tarHide, config.oil, e =>
                {
                    Named(e, "Ooze", "Great Ooze", "Tar Colossus", "Falls apart into two smaller ones.", "Falls apart into mini bosses, which fall apart again.");
                    e.hp = 40f; e.speed = 3f; e.radius = 0.6f; e.contactDamage = 5f; e.height = 0.9f; e.wobble = true;
                }),
            };
            // ---- Kiln, the Warden of the Cinder Flats, and its keep ----
            var kiln = Asset<EnemyDef>(Data + "/Enemies/Kiln.asset", e =>
            {
                e.displayName = "Kiln"; e.behaviour = Behaviour.Warden; e.windUp = 1f;
                e.hp = 2600f; e.speed = 3.2f; e.radius = 2.3f; e.armor = 1f; e.contactDamage = 11f;
                e.height = 5.6f; e.legSpacing = 1f; e.hipHeight = 1.45f;
                e.trait = "Pounds the ground, rams across the arena and lobs fire. Every attack shows first.";
                e.bossTrait = "Hotter and faster. Fire that follows the machine, and the floor erupts in lines.";
                e.plate = cinderHide; e.glow = config.heat;
                e.body = Mat("KilnIron", new Color(0.21f, 0.2f, 0.21f), smoothness: 0.35f);
            });
            GiveMesh(kiln, ref kiln.mesh, "Kiln");
            GiveMesh(kiln, ref kiln.legMesh, "Kiln_Leg");
            var kilnReactor = Asset<ReactorDef>(Data + "/Machine/KilnReactor.asset", r =>
            {
                r.displayName = "Kiln Reactor"; r.powerCap = 13;
            });
            Tell(kilnReactor, ref kilnReactor.description, "What burned inside Kiln. It gives more Power than anything the Roundhouse has built.");
            var keep = Asset<DungeonDef>(Data + "/World/4_KilnsKeep.asset", d =>
            {
                d.displayName = "Kiln's Keep"; d.map = KilnsKeepMap; d.reward = 300;
                d.boss = kiln; d.bossTitle = "Warden of the Cinder Flats"; d.bossSecondName = "Meltdown";
                d.finds = new ScriptableObject[] { kilnReactor };
                d.landmark = AssetDatabase.LoadAssetAtPath<Mesh>($"{Models}/KilnsKeep.fbx");
                d.landmarkDepth = 4.5f; d.landmarkRadius = 17f;
            });
            if (keep.map != KilnsKeepMap || keep.landmarkDepth != 4.5f || keep.landmarkStone == null)
            {
                keep.map = KilnsKeepMap;
                keep.landmarkDepth = 4.5f;
                // Soot-black stone with iron on top, so the only light on it is its own fire.
                keep.landmarkStone = Mat("KeepStone", new Color(0.17f, 0.16f, 0.17f));
                keep.landmarkTrim = Mat("KeepIron", new Color(0.33f, 0.24f, 0.2f), smoothness: 0.3f);
                EditorUtility.SetDirty(keep);
            }
            if (!Array.Exists(world.entrances, e => e.dungeon == keep))
            {
                var entrances = new System.Collections.Generic.List<WorldDef.Entrance>(world.entrances)
                {
                    new WorldDef.Entrance { dungeon = keep, position = new Vector3(30f, 0f, 76f), requires = -1 },
                };
                world.entrances = entrances.ToArray();
                EditorUtility.SetDirty(world);
            }

            // Version 4: darker caps on dungeon walls and stands, and the gate of a Warden's arena.
            if (scenery.version < 4)
            {
                var cap = Mat("DungeonCap", new Color(0.29f, 0.3f, 0.33f));
                scenery.wall.accent = cap;
                scenery.stand.accent = cap;
                scenery.bossGate = AssetDatabase.LoadAssetAtPath<Mesh>($"{Models}/BossGate.fbx");
                scenery.version = 4;
                EditorUtility.SetDirty(scenery);
            }

            var speciesByBiome = new[] { cinderSpecies, frostSpecies, tarSpecies };
            for (int b = 0; b < scenery.biomes.Length && b < speciesByBiome.Length; b++)
            {
                if (scenery.biomes[b].species != null && scenery.biomes[b].species.Length > 0) continue;
                scenery.biomes[b].species = speciesByBiome[b];
                EditorUtility.SetDirty(scenery);
            }

            // ---- The lobby ----
            Mesh Model(string model) => AssetDatabase.LoadAssetAtPath<Mesh>($"{Models}/{model}.fbx");
            var lobby = Asset<LobbyDef>(Data + "/World/Roundhouse.asset", l =>
            {
                l.wall = Model("RH_Wall"); l.bayDoor = Model("RH_BayDoor"); l.gate = Model("Gate"); l.turntable = Model("Turntable");
                l.lamp = Model("Lamp"); l.crate = Model("Crate"); l.barrel = Model("Barrel");
                l.workbench = Model("Workbench"); l.shelf = Model("Shelf"); l.furnace = Model("Furnace");
                l.mechanic = Model("NPC_Fitter"); l.archivist = Model("NPC_Archivist"); l.smelter = Model("NPC_Smelter");
                l.character = Model("Character"); l.characterLeg = Model("Character_Leg"); l.characterArm = Model("Character_Arm");
                l.wallMaterial = Mat("RoundhouseWall", new Color(0.3f, 0.25f, 0.22f));
                l.trim = Mat("RoundhouseTrim", new Color(0.36f, 0.39f, 0.43f), smoothness: 0.3f);
                l.lampGlow = Mat("LampGlow", new Color(1f, 0.8f, 0.52f));
                Glow(l.lampGlow, 2f);
                l.fireGlow = config.heat;
                l.floor = new Color(0.2f, 0.19f, 0.19f); l.floorAlt = new Color(0.26f, 0.23f, 0.21f); l.floorBand = new Color(0.34f, 0.3f, 0.24f);
            });
            if (config.lobby == null) config.lobby = lobby;

            // ---- What enemies leave behind. For now: hearts that repair the machine. ----
            var drops = Asset<DropPool>(Data + "/World/Drops.asset", d =>
            {
                d.entries = new[]
                {
                    new DropPool.Entry { kind = DropKind.Heart, chance = 0.07f, amount = 12f, mesh = Model("Heart"), fromMiniBoss = 2, fromBoss = 4 },
                };
            });
            if (config.drops == null) config.drops = drops;
            // Beats: one from most enemies, clusters of five from the strong ones.
            if (!Array.Exists(drops.entries, e => e.kind == DropKind.Beat))
            {
                var all = new System.Collections.Generic.List<DropPool.Entry>(drops.entries)
                {
                    new DropPool.Entry { kind = DropKind.Beat, chance = 0.75f, amount = 1f, mesh = Model("Beat"), material = config.pulse, fromMiniBoss = 3, fromBoss = 5, fromWarden = 10 },
                    new DropPool.Entry { kind = DropKind.Beat, chance = 0f, amount = 5f, mesh = Model("Beat"), material = config.pulse, fromMiniBoss = 2, fromBoss = 6, fromWarden = 30 },
                };
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].kind != DropKind.Heart) continue;
                    var heart = all[i];
                    heart.fromWarden = 6;
                    all[i] = heart;
                }
                drops.entries = all.ToArray();
                EditorUtility.SetDirty(drops);
            }

            // ---- Frames are bought at the Smelter. Everyone starts with the Buggy and one machine on it. ----
            GiveMesh(buggy, ref buggy.bodyMesh, "Buggy_Body");
            GiveMesh(buggy, ref buggy.wheelMesh, "Wheel");
            GiveMesh(rover, ref rover.bodyMesh, "Rover_Body");
            GiveMesh(rover, ref rover.wheelMesh, "Wheel");
            if (config.contentVersion < 7)
            {
                crawler.price = 500;
                crawler.hull = 150f;
                crawler.marks = new[] { new FrameDef.Mark { cost = 250, hull = 30f }, new FrameDef.Mark { cost = 500, hull = 45f, armor = 1f } };
                EditorUtility.SetDirty(crawler);
                var starter = loadouts[0];
                starter.frame = buggy;
                starter.modules = new[]
                {
                    new Loadout.Entry { slot = "turret", module = autocannon }, new Loadout.Entry { slot = "socket", module = plating },
                };
                EditorUtility.SetDirty(starter);
                config.loadouts = new[] { starter };
                // The other parts of the first prototype are still everyone's from the start.
                config.startingParts = new ScriptableObject[]
                {
                    autocannon, flamethrower, oilSprayer, teslaCoil, splitter, incendiaryFeed, plating, reactivePlating,
                };
                config.contentVersion = 7;
            }
            // ---- Round 8: slots have new names, parts are items, and one bay door can be bought open ----
            if (config.contentVersion < 8)
            {
                foreach (var frame in new[] { rover, crawler })
                {
                    for (int i = 0; i < frame.slots.Length; i++) frame.slots[i].id = Profile.Renamed(frame.slots[i].id);
                    for (int i = 0; i < frame.wires.Length; i++)
                    {
                        frame.wires[i].system = Profile.Renamed(frame.wires[i].system);
                        frame.wires[i].slot = Profile.Renamed(frame.wires[i].slot);
                    }
                    EditorUtility.SetDirty(frame);
                }
                rover.description = "Room for a second weapon. One system acts on both, and on the armor too.";
                crawler.description = "A low, wide body on four wheels. Three weapons, two systems and three armor slots.";
                buggy.description = "Small and light. One weapon, and one socket that takes a system or armor.";
                EditorUtility.SetDirty(buggy);
                foreach (var loadout in loadouts)
                {
                    for (int i = 0; i < loadout.modules.Length; i++) loadout.modules[i].slot = Profile.Renamed(loadout.modules[i].slot);
                    EditorUtility.SetDirty(loadout);
                }
                var starting = new System.Collections.Generic.List<ScriptableObject>(config.startingParts ?? new ScriptableObject[0]);
                if (!starting.Contains(extender)) starting.Add(extender);
                config.startingParts = starting.ToArray();
                config.contentVersion = 8;
            }

            // The abilities a dropped part can carry: one number for each rarity, Common to Legendary.
            PerkDef Ability(string name, string text, PartKind fits, PerkStat stat, StateType state, params float[] values) =>
                Asset<PerkDef>($"{Data}/Abilities/{name}.asset", a =>
                {
                    a.displayName = name; a.text = text; a.fits = fits; a.stat = stat; a.state = state; a.values = values;
                });
            config.perks = new[]
            {
                Ability("Keen", "{0} % more damage", PartKind.Weapon, PerkStat.Damage, StateType.None, 10f, 18f, 28f, 40f, 60f),
                Ability("Rapid", "Fires {0} % faster", PartKind.Weapon, PerkStat.FireRate, StateType.None, 8f, 14f, 22f, 32f, 45f),
                Ability("Reaching", "{0} % more range", PartKind.Weapon, PerkStat.Range, StateType.None, 10f, 18f, 28f, 40f, 55f),
                Ability("Searing", "{0} % of its hits apply Heat", PartKind.Weapon, PerkStat.StateChance, StateType.Heat, 6f, 10f, 16f, 24f, 35f),
                Ability("Arcing", "{0} % of its hits apply Shock", PartKind.Weapon, PerkStat.StateChance, StateType.Shock, 6f, 10f, 16f, 24f, 35f),
                Ability("Slick", "{0} % of its hits apply Oil", PartKind.Weapon, PerkStat.StateChance, StateType.Oil, 6f, 10f, 16f, 24f, 35f),
                Ability("Chilling", "{0} % of its hits apply Cryo", PartKind.Weapon, PerkStat.StateChance, StateType.Cryo, 6f, 10f, 16f, 24f, 35f),
                Ability("Thick", "{0} more hull", PartKind.Armor, PerkStat.Hull, StateType.None, 8f, 14f, 22f, 32f, 45f),
                Ability("Hardened", "Takes {0} more off every hit", PartKind.Armor, PerkStat.Armor, StateType.None, 0.5f, 1f, 1.5f, 2f, 3f),
                Ability("Mending", "Repairs {0} hull a second", PartKind.Armor, PerkStat.Mending, StateType.None, 0.3f, 0.5f, 0.8f, 1.2f, 2f),
                Ability("Spiked", "Whatever hits the machine takes {0} damage", PartKind.Armor, PerkStat.Thorns, StateType.None, 6f, 10f, 16f, 24f, 36f),
                Ability("Tuned", "What it is wired to deals {0} % more damage", PartKind.System, PerkStat.WiredDamage, StateType.None, 4f, 7f, 11f, 16f, 24f),
                Ability("Wide", "What it is wired to reaches {0} % further", PartKind.System, PerkStat.WiredRange, StateType.None, 5f, 8f, 12f, 17f, 25f),
            };

            // Enemies drop parts too: rarely an ordinary one, always a mini boss or a boss.
            if (!Array.Exists(drops.entries, e => e.kind == DropKind.Part))
            {
                var all = new System.Collections.Generic.List<DropPool.Entry>(drops.entries)
                {
                    new DropPool.Entry { kind = DropKind.Part, chance = 0.015f, mesh = Model("PartCrate"), material = config.flash, fromMiniBoss = 1, fromBoss = 1, fromWarden = 2 },
                };
                drops.entries = all.ToArray();
                EditorUtility.SetDirty(drops);
            }

            // The doors to the rooms, the rooms behind them and everything in them; the pillars and the
            // low wall of the hall; the display at the turntable.
            string[] furniture =
            {
                "RH_DoorFrame", "RH_Shutter", "RH_LockBar", "TR_Floor", "TR_Wall", "TR_Corner", "TR_Kerb", "TR_Press", "TR_PressHead",
                "NPC_Tuner", "NPC_Tuner_Arm", "TR_Console", "TR_Shelf", "TR_ToolWall", "TR_Bench", "TR_Tanks", "TR_Bins", "TR_Reel",
                "TR_Vent", "Crate", "Barrel",
                "RH_Pillar", "RH_WallLow", "RH_PillarLow", "RH_Display", "RM_Floor",
                "MR_Lift", "MR_Hoist", "MR_Load", "MR_ToolChest", "MR_Compressor", "MR_Gear", "MR_WeaponStand", "MR_Welder", "MR_Clutter",
                "AR_Stacks", "AR_CardCabinet", "AR_Desk", "AR_Case", "AR_Projector", "AR_Ladder", "AR_TapeBank", "AR_Reel", "AR_Scrolls",
                "SR_Rail", "SR_Cart", "SR_Moulds", "SR_Hammer", "SR_HammerHead", "SR_Bellows", "SR_CoalBin", "SR_Ingots", "SR_Quench",
            };
            if (lobby.models == null || lobby.models.Length != furniture.Length || Array.Exists(lobby.models, m => m.mesh == null))
            {
                lobby.models = Array.ConvertAll(furniture, name => new LobbyDef.Named { name = name, mesh = Model(name) });
                EditorUtility.SetDirty(lobby);
            }

            GiveMesh(lobby, ref lobby.deck, "TurntableDeck");
            GiveMesh(lobby, ref lobby.armBase, "Arm_Base");
            GiveMesh(lobby, ref lobby.armTurret, "Arm_Turret");
            GiveMesh(lobby, ref lobby.armUpper, "Arm_Upper");
            GiveMesh(lobby, ref lobby.armFore, "Arm_Fore");
            GiveMesh(lobby, ref lobby.armWelder, "Arm_Welder");
            GiveMesh(lobby, ref lobby.armClaw, "Arm_Claw");
            GiveMesh(lobby, ref lobby.frameRack, "FrameRack");
            // Fire and smoke over the chimneys of the keep.
            if (keep.landmarkFires == null || keep.landmarkFires.Length == 0)
            {
                keep.landmarkFires = new[]
                {
                    new Vector3(3.2f, 15.3f, 6.6f), new Vector3(-3.2f, 15.3f, 6.6f), new Vector3(0f, 16.7f, 7.4f),
                    new Vector3(8.9f, 9.3f, 7.6f), new Vector3(-8.9f, 9.3f, 7.6f),
                };
                EditorUtility.SetDirty(keep);
            }

            // ---- Round 11: more for the dungeons to keep, Echoes, and things to find in the world ----
            if (config.contentVersion < 9)
            {
                void Also(DungeonDef dungeon, params ScriptableObject[] more)
                {
                    var all = new System.Collections.Generic.List<ScriptableObject>(dungeon.finds ?? new ScriptableObject[0]);
                    foreach (var one in more)
                        if (!all.Contains(one)) all.Add(one);
                    dungeon.finds = all.ToArray();
                    EditorUtility.SetDirty(dungeon);
                }
                Also(cinderPit, sawRam);
                Also(slagWorks, cryoFeed, mortar);
                Also(kilnsGate, mineLayer, oilFeed);
                Also(keep, railgun);
                config.contentVersion = 9;
            }

            // The Echoes. Eight belong to no strand and are there from the first run. Each of the
            // other four is a fragment of a strand and joins them once its dungeon has been cleared.
            EchoDef Echo(string file, string name, string text, EchoKind kind, StateType strand = StateType.None, DungeonDef after = null) =>
                Asset<EchoDef>($"{Data}/Echoes/{file}.asset", e =>
                {
                    e.displayName = name; e.text = text; e.kind = kind; e.strand = strand; e.joinsAfter = after;
                });
            config.echoes = new[]
            {
                Echo("SecondBeat", "Second Beat", "Every reaction goes off twice. The second is half as strong.", EchoKind.SecondBeat),
                Echo("Undertow", "Undertow", "Drops fly to the machine from nine metres away.", EchoKind.Undertow),
                Echo("Crescendo", "Crescendo", "Every kill adds 1 % damage, up to 40. Losing hull ends it.", EchoKind.Crescendo),
                Echo("Afterburn", "Afterburn", "Nitro refills twice as fast and leaves fire behind.", EchoKind.Afterburn),
                Echo("DoubleTime", "Double Time", "Every weapon fires a quarter faster.", EchoKind.DoubleTime),
                Echo("Encore", "Encore", "Once, the blow that would destroy the machine throws everything back.", EchoKind.Encore),
                Echo("HeavyPurse", "Heavy Purse", "Beats count double. Nothing leaves a heart.", EchoKind.HeavyPurse),
                Echo("LongWire", "Long Wire", "Every system reaches every weapon.", EchoKind.LongWire),
                Echo("Ember", "Ember", "What dies burning sets its neighbours alight.", EchoKind.Ember, StateType.Heat, keep),
                Echo("Afterimage", "Afterimage", "Every tenth hit chains lightning.", EchoKind.Afterimage, StateType.Shock, cinderPit),
                Echo("DeepCold", "Deep Cold", "Chill lasts twice as long and slows harder.", EchoKind.DeepCold, StateType.Cryo, slagWorks),
                Echo("Seep", "Seep", "Your oil lies three times as long.", EchoKind.Seep, StateType.Oil, kilnsGate),
            };
            // A boss of the open world leaves a choice of Echoes behind.
            if (!Array.Exists(drops.entries, e => e.kind == DropKind.Echo))
            {
                var all = new System.Collections.Generic.List<DropPool.Entry>(drops.entries)
                {
                    new DropPool.Entry { kind = DropKind.Echo, chance = 0f, mesh = Model("Echo"), material = config.pulse, fromBoss = 1 },
                };
                drops.entries = all.ToArray();
                EditorUtility.SetDirty(drops);
            }
            GiveMesh(config, ref config.veinMesh, "BeatVein");
            GiveMesh(config, ref config.cacheMesh, "Cache");
            GiveMesh(config, ref config.cacheOpenMesh, "CacheOpen");
            GiveMesh(config, ref config.mineMesh, "Mine");

            // Everything a machine can be built from, so that saved machines can be read back by name.
            config.frames = new[] { buggy, rover, crawler };
            config.reactors = new[] { reactor, kilnReactor };
            config.drives = new[] { wheels };
            config.modules = new ModuleDef[]
            {
                autocannon, flamethrower, oilSprayer, teslaCoil, longshot, frostJet, mortar, mineLayer, sawRam, railgun,
                splitter, incendiaryFeed, arcFeed, cryoFeed, oilFeed, extender, plating, reactivePlating, heavyPlating,
            };
            // A heart is red: the one red thing in the game, so it cannot be mistaken for anything else.
            var heartRed = Mat("HeartRed", new Color(0.93f, 0.1f, 0.2f));
            Glow(heartRed, 1.7f);
            for (int i = 0; i < drops.entries.Length; i++)
            {
                if (drops.entries[i].kind != DropKind.Heart || drops.entries[i].material != null) continue;
                drops.entries[i].material = heartRed;
                EditorUtility.SetDirty(drops);
            }
            EditorUtility.SetDirty(config);

            Asset<PanelSettings>(PanelPath, p =>
            {
                p.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Root + "/UI/Pulse.tss");
                p.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                p.referenceResolution = new Vector2Int(1920, 1080);
                p.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                p.match = 0.5f;
            });

            AssetDatabase.SaveAssets();
            if (!File.Exists(ScenePath)) RebuildArena();
            if (!File.Exists(LobbyScenePath)) RebuildLobby();
            SetBuildScenes();
            Debug.Log("Pulse: default content is in place.");
        }

        // ---- For trying things out. They work while the game is playing. ----

        // The player's own profile, as it is on disk. Outside Play the one in memory may be old, or
        // the one a test left behind.
        static GameConfig OpenProfile()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (Application.isPlaying) Profile.Load(config);
            else
            {
                Profile.Key = Profile.DefaultKey;
                Profile.Reload(config);
            }
            return config;
        }

        // While a run is going on: the cards with a choice of Echoes come up.
        [MenuItem("Pulse/Run/Offer an Echo")]
        static void OfferEcho()
        {
            if (!Application.isPlaying || Combat.Rig == null) { Debug.Log("Pulse: start a run first."); return; }
            if (Echoes.Held.Count >= Echoes.Max) { Debug.Log("Pulse: the machine holds three Echoes already."); return; }
            Echoes.Owed++;
        }

        [MenuItem("Pulse/Profile/Give 500 Beats")]
        static void GiveBeats()
        {
            OpenProfile();
            Profile.Earn(500);
            Debug.Log($"Pulse: {Profile.Beats} Beats.");
        }

        [MenuItem("Pulse/Profile/Give a Dropped Part")]
        static void GivePart()
        {
            var config = OpenProfile();
            var item = Profile.Keep(Tuning.Roll(config, Rank.Boss, () => UnityEngine.Random.value));
            Debug.Log($"Pulse: {Tuning.Name(item.rarity)} {item.Name}, {(item.ability != null ? item.ability.displayName : "no ability")}.");
        }

        [MenuItem("Pulse/Profile/Find Every Part")]
        static void FindEverything()
        {
            var config = OpenProfile();
            foreach (var module in config.modules) Profile.Find(module);
            foreach (var reactor in config.reactors) Profile.Find(reactor);
            Debug.Log("Pulse: every module and reactor is found. Frames still have to be bought.");
        }

        [MenuItem("Pulse/Profile/Lock the Tuning Room")]
        static void LockRoom()
        {
            OpenProfile();
            Debug.Log(Profile.Lock(TuningRoom.Id) ? "Pulse: the Tuning Room is locked again. Reload the scene to see it."
                : "Pulse: the Tuning Room was not open.");
        }

        [MenuItem("Pulse/Profile/Start Over")]
        static void StartOver()
        {
            if (!Application.isPlaying) Profile.Key = Profile.DefaultKey;
            Profile.Reset(AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath));
            Debug.Log("Pulse: the profile is new. Reload the scene to see it.");
        }

        [MenuItem("Pulse/Rebuild Arena Scene")]
        public static void RebuildArena()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Loaded after the scene switch: opening a new scene unloads assets nothing refers to yet.
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (config == null || panel == null)
            {
                Debug.LogError("Pulse: run 'Create Default Content' first.");
                return;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.48f);
            RenderSettings.fog = false;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Tokens.Soot;
            camera.fieldOfView = 35f;
            camera.farClipPlane = 250f;
            camera.gameObject.AddComponent<AudioListener>();
            var cameraRig = camera.gameObject.AddComponent<CameraRig>();
            camera.transform.SetPositionAndRotation(cameraRig.offset, Quaternion.LookRotation(-cameraRig.offset));

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = config.ground;

            var game = new GameObject("Game");
            game.AddComponent<UIDocument>().panelSettings = panel;
            game.AddComponent<GameRoot>().config = config;

            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildScenes();
        }

        [MenuItem("Pulse/Rebuild Lobby Scene")]
        public static void RebuildLobby()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (config == null || panel == null)
            {
                Debug.LogError("Pulse: run 'Create Default Content' first.");
                return;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.27f, 0.26f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Tokens.Soot;
            camera.fieldOfView = 35f;
            camera.gameObject.AddComponent<AudioListener>();
            camera.gameObject.AddComponent<CameraRig>();

            var lobby = new GameObject("Lobby");
            lobby.AddComponent<UIDocument>().panelSettings = panel;
            lobby.AddComponent<LobbyRoot>().config = config;

            EditorSceneManager.SaveScene(scene, LobbyScenePath);
            SetBuildScenes();
        }

        // The lobby comes first, so a build starts there.
        static void SetBuildScenes()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (string path in new[] { LobbyScenePath, ScenePath })
                if (File.Exists(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static T Asset<T>(string path, Action<T> init) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static Material Mat(string name, Color color, string shader = "Universal Render Pipeline/Lit", float smoothness = 0f)
        {
            string path = $"{Materials}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(Shader.Find(shader)) { color = color, enableInstancing = true };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // Makes a material emit its own colour. Left alone if it already glows.
        static void Glow(Material material, float strength)
        {
            if (material == null || material.IsKeywordEnabled("_EMISSION")) return;
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.SetColor("_EmissionColor", material.color.linear * strength);
            EditorUtility.SetDirty(material);
        }

        static void GiveMesh(ScriptableObject asset, ref Mesh field, string model)
        {
            if (field != null) return;
            field = AssetDatabase.LoadAssetAtPath<Mesh>($"{Root}/Art/Models/{model}.fbx");
            if (field != null) EditorUtility.SetDirty(asset);
        }

        static void Describe(ModuleDef module, string name, string description, int power)
        {
            module.displayName = name;
            module.description = description;
            module.powerCost = power;
        }

        static SlotDef Slot(string id, SlotType type, Mount mount, Vector3 localPos, float x, float y) =>
            new SlotDef { id = id, type = type, mount = mount, localPos = localPos, schematicPos = new Vector2(x, y) };

        static WireDef Wire(string system, string slot) => new WireDef { system = system, slot = slot };
    }
}
