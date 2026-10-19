# Pulse (working title)

Unity 6 project for the roguelike described in the concept files one folder up (machine-concept.md, lore.md, lobby.md, gui-style.md). It holds the first prototype: a lobby with a workshop in its middle and a room that is bought open, where machines are built from parts that have levels and abilities on frames bought for Beats; six weapons, three reactions, an open world with three biomes and nine species that each have a mini boss and a boss form, three dungeons with maps of their own, and the first Warden, Kiln, in an arena under its keep.

## Run it

1. Open the project with Unity 6000.6.5f1.
2. Open `Assets/Game/Scenes/Lobby.unity` and press Play. To skip the lobby, open `Arena.unity` instead.

In the lobby you walk a small robot: the stick or WASD moves it in that direction on screen. E or the A button next to a machine opens its screen. Standing in the green half circle at the gate starts a run: the machine fetches the robot and drives out.

- **The Mechanic** builds machines: click a machine on the left, a row in the middle (frame, slot, drive, reactor) and a part on the right. Save keeps the change; Take puts the machine on the turntable. The player keeps two machines.
- **The Smelter** sells frames and their Marks for Beats. Only another frame gives other slots; a Mark only adds hull and armor.
- **The Tuner** stands in a room behind the bay door on the west side, which costs 150 Beats to open. It raises a part by a level for Beats and scraps spare parts.
- **The Archivist** shows every part there is and where the missing ones are kept. The other side of its screen is the Codex: the reactions, Strays and Echoes met so far, and Beats for every entry that is new. The button bottom right, or Q, turns the screen over.

The screens carry no key hints. With the keyboard: W A S D move, E chooses, Esc goes back, and at the Mechanic Q saves, T takes and Del deletes.

In a run:

| Input | Action |
|---|---|
| W / S, up / down, or stick up / down | Drive forward or reverse, seen from the machine |
| A / D, left / right, or stick left / right | Turn left or right. Wheels need speed to turn and steer mirrored in reverse, like a car. The machine turns in a wide arc: a full circle takes about four seconds. |
| Space, right trigger or X | Nitro: a burst of speed while held and driving forward. The tank refills by itself. |
| R or Y | Go home: the teleporter charges for four seconds, and any hull lost breaks it |
| Stand in a green entrance | After two seconds its floor breaks and the machine falls into that dungeon |
| Stay next to a crate | It opens after six seconds, and the Strays around come for it |
| Drive into a vein | It breaks and throws out its Beats |
| A / D and E, stick and A button, or a click | Choose one of the Echoes when the cards come up |
| Esc or Start | Pause menu: resume, return to the Roundhouse (starts the teleporter), quit, and the list of controls |
| Enter, Space or A button | Return to the lobby after the machine is destroyed |

Weapons fire on their own. A new profile starts with one machine, the Starter: the Buggy frame with an Autocannon and Plating. The machine is chosen in the lobby and cannot be changed during a run.

The game saves by itself. What the player has is one file, `pulse.profile.json` in Unity's persistent data folder (`%USERPROFILE%\AppData\LocalLow\<company>\<product>` on Windows), with the file before it kept next to it as `.bak`: Beats, parts with their levels and abilities, owned frames with their Marks, the two machines and which rooms are open. It is written within a second of anything changing and at once when the game is left; a small green mark lights bottom right when it has been. An older profile is carried over. The options (screen shake, flashes, camera distance, fullscreen, V-Sync) are in the pause menu and are kept apart from the profile; so is "Erase save", which asks twice. The menu **Pulse > Profile** has helpers for trying things out: Give 500 Beats, Give a Dropped Part, Find Every Part, Lock the Tuning Room (to buy it open once more), and Start Over. Outside Play they read the player's own file first. **Pulse > Run > Offer an Echo** brings up the choice of Echoes in a run that is going on.

The mouse pointer is hidden while the machine drives or the character walks, and comes back with every menu and screen. In a run, what the machine picks up is listed on the right edge of the screen: parts and Beats, not hearts. Beats are carried until the machine is home: the teleporter banks them, a destroyed machine loses them.

In the Roundhouse every machine has a room behind a bay door (`Scripts/Lobby/LobbyRoom.cs` and one class per room), and a display at the turntable chooses which machine is taken (`PresetScreen.cs`). The rooms behind the far wall turn the camera while the character is in them (`CameraRig.yaw`).

## How a run works

- The open world has three biomes that meet where the run starts: Cinder Flats to the north-east, Frostline to the west, Tar Marsh to the south. Each has its own floor colours, props and glow.
- The map has no wall. It ends in fog that grows denser the further in the machine goes, and past the edge a storm strikes it with lightning, harder the deeper it goes (`stormDepth` and the two `stormDamage` values in the world asset). Enemies stay out of it.
- A run that starts in the lobby begins at speed: the machine races in from the south and brakes as it reaches the starting point. Lobby and open world are joined without a cut; so is the fall into a dungeon. Both use the iris in `CameraRig`.
- The open world spawns enemies at a constant rate. It never gets harder with time. Enemies stay idle until the machine comes close (`aggroRange` in the world asset); after that they follow it for good.
- Each biome has three species of its own, listed with what they do in machine-concept.md, section 9.1. Any spawn can be a mini boss or, more rarely, a boss (`miniBossChance` and `bossChance` in the world asset). Both carry a hull bar, and a boss that is after the machine gets a wide bar at the top of the screen.
- Attacks that matter are shown before they land: a line for a ram, a dart or a shot; for a slam, a burst or a falling glob the ground it will hit flickers with pale flecks. How long each species shows its attacks is `windUp` on its asset in `Data/Enemies`.
- Nothing in the world is marked with a circle. Burning ground, oil and frost are made of particles (`Vfx.Ground`, `Rise` and `Bubbles`, fed by `ZoneSystem`), and the Tesla coil sends out a wave of sparks. Areas left by enemies also throw up pale amber sparks and act on the machine too.
- Destroyed enemies sometimes drop a red heart that repairs the machine. The machine has to drive onto it. What drops, how often, how much it is worth and what colour it is, is in `Data/World/Drops.asset`.
- Small plants, splinters and small stones are flattened when the machine drives over them. Everything larger blocks it. Hit at half speed or more, a soft prop (a tree, a wreck, a pylon) shakes once, and a hard one (rock, ice, a snow drift) throws the machine back a little. Neither happens again until the machine has moved away from that prop.
- Now and then a destroyed enemy drops a part: a small case that throws up sparks. A mini boss and a boss always drop one. It is a module with an ability of some rarity (machine-concept.md, section 6.1), and it is waiting at the Mechanic and the Tuner once the machine is home.
- Destroyed enemies also drop Beats, the currency: small green splinters that throb in one shared rhythm with the counter at the top of the screen. They are carried: the teleporter home banks them, and a destroyed machine loses them.
- A small green marker next to the machine points to the nearest open dungeon entrance. Entrances are green when open, grey when locked and white when cleared.
- A dungeon is fought in waves on a map of its own: walls, pillars, and vents that throw up the hazard of the biome above. The map is drawn as text in the dungeon's asset (`map`; the characters are explained in `DungeonMap.cs`). Clearing a dungeon repairs the machine, returns it to the open world, unlocks the next entrance, pays Beats (`reward`: 40, 100 and 180) and hands over the parts it kept (`finds`).
- Kiln's Keep stands in the north-east of the Cinder Flats and is open from the start. Its entrance drops the machine into an arena with a crowd on the stands. Kiln makes an entrance, fights in two phases and pays 300 Beats and the Kiln Reactor. What it does is in machine-concept.md, section 9.3; how it fights is `EnemySystem.Reign`, and everything around the fight is `BossFight.cs`.
- A cleared dungeon, a Warden and every boss of the open world leave an **Echo**: a shard that stops the run when the machine drives onto it and offers three cards. An Echo bends one rule for the rest of the run, and a run holds three (machine-concept.md, section 3.5). Which are in force is in `Core/Echoes.cs`; each rule is checked where it applies. The Echoes themselves are assets in `Data/Echoes`.
- Cryo chills, and with it there are six reactions: Thermal Shock, Superconductor and Black Ice besides Firestorm, Flash and Plasma. Their numbers are in `GameConfig` under Reactions.
- The open world holds **veins** of Beats and **caches**, somewhere else in every run (`Game/Finds.cs`; machine-concept.md, section 9.5).
- What a run meets for the first time is written into the Codex (`Core/Codex.cs`) and paid for by the Archivist.
- A destroyed machine brings up the end-of-run screen with what destroyed it, eight numbers, two rows of smaller ones and one button back to the lobby.

## Where things are

| Path | Content |
|---|---|
| `Assets/Game/Data` | One asset per weapon, system, support module, enemy, frame, loadout and dungeon, plus the world and `GameConfig` with all tuning values. Change numbers here; no code needed. |
| `Assets/Game/Scripts/Core` | States, the reaction matrix, colour tokens, what a rank does to a species, the profile that is kept between runs and the object that saves it (`Profile.cs`, `Autosave.cs`), the rules of levels, abilities and drops (`Items.cs`), which Echoes are in force (`Echoes.cs`), the Codex (`Codex.cs`), the options, and the catalogue of parts |
| `Assets/Game/Scripts/Data` | The asset types |
| `Assets/Game/Scripts/Machine` | The machine, the wiring rules, weapon and support behaviour |
| `Assets/Game/Scripts/Combat` | Enemies and how each species fights, ground zones, projectiles, drops, line effects, particles |
| `Assets/Game/Scripts/Game` | Run start, open world and dungeon logic, dungeon maps, the Warden's arena, the teleporter home (`Recall.cs`), the scenes that play when entering either, the storm at the edge of the map, veins and caches (`Finds.cs`), scenery, floor, look, camera |
| `Assets/Game/Scripts/Lobby` | The Roundhouse: the hall with its wall, pillars and display (`LobbyRoot.cs`), the workshop with its deck and arms (`Workshop.cs`), the walking character, what every room shares (`LobbyRoom.cs`: door, shell, opening scene, camera) and the four rooms (`MechanicRoom.cs`, `ArchiveRoom.cs`, `SmelterRoom.cs`, `TuningRoom.cs`), the screens of the machines and of the display (`PresetScreen.cs`), the floor plan the character walks on (`LobbyWalk.cs`), and the scenes of leaving and arriving |
| `Assets/Game/Scripts/UI` | HUD, boss bars and the Warden's title card, pause menu, end-of-run screen, the choice of an Echo and the signs of Echoes and Codex entries (`EchoScreen.cs`), the cut-corner `Plate` and `PlateButton` elements, the wiring diagram, part tiles and chips |
| `Assets/Game/UI` | `Pulse.uss`, the one style sheet for every screen, and the fonts |
| `Assets/Game/Art/Models` | FBX models, generated by the Blender script |
| `Assets/Game/Tests` | Tests for reactions, wiring, waves, dungeon unlocks, ranks, the storm and drops, levels, abilities and rarities, Echoes and the Codex, and in `PlayMode` fifteen tests that play through the lobby with its workshop, a run, every species in every rank, Nitro with hearts and the storm, the whole fight against Kiln, the lobby screens, buying the Tuner's room and tuning parts, dropped parts, carrying an old profile over, the options, the teleporter home, the pause and end screens, Echoes and what each of them changes, the reactions with cold and the four new weapons, veins and caches, and the Archivist paying for the Codex. They fail on any logged error, and they use a profile of their own. |
| `Assets/Game/Editor` | The menu **Pulse > Create Default Content** and **Pulse > Rebuild Arena Scene** |
| `Tools/blender` | `build_models.py`, which builds every model |
| `ArtSource` | `pulse_models.blend` with all models side by side |

## Adding content

- **A weapon, system or support module:** right-click in `Assets/Game/Data`, choose Create > Pulse, fill in the values, and add it to `Modules` in `GameConfig`. To have it from the start, put it into one of the starting loadouts; to make the player find it, list it under `finds` of a dungeon.
- **An ability:** Create > Pulse > Ability, say which kind of part carries it, what it changes and its five numbers, and add it to `Perks` in `GameConfig`. What each kind of number does is in `Tuning.At` in `Items.cs`.
- **A frame:** Create > Pulse > Frame, list its slots (a Socket takes a system or a support module) and wires, set its price and Marks, give it a body model, and add it to `Frames` in `GameConfig`. The Smelter and the Mechanic pick it up from there. A price of zero means everyone has it.
- **A starting machine:** Create > Pulse > Loadout, pick frame, reactor and drive, list which module sits in which slot, and add it to `Loadouts` in `GameConfig`. Everything a starting machine is made of counts as found.
- **A dungeon:** Create > Pulse > Dungeon, draw its map, list its waves, set what it pays and keeps, then add an entrance for it to the world asset in `Assets/Game/Data/World`. Giving it a `boss` turns it into a Warden's arena; a `landmark` is the building that stands behind its entrance.
- **A species:** Create > Pulse > Enemy, pick its Behaviour, give it names for its mini boss and boss, then add it to the Species list of a biome in `Data/World/Scenery.asset`. What a Behaviour does, and what its mini boss and boss add, is in `EnemySystem.cs`: one method per Behaviour. What a rank does to hull, size, damage and speed is in `Ranks.cs`.
- **A reaction:** the matrix is in `Reactions.cs`; what a reaction does is in `EnemySystem.React`. To have it in the Codex, add it to `Codex.Built` with a line of text.
- **An Echo:** add a kind to `EchoKind`, check `Echoes.Has(kind)` where its rule applies, give it a sign in `EchoIcon.Paint`, then Create > Pulse > Echo and add the asset to `Echoes` in `GameConfig`. `joinsAfter` names the dungeon that has to be cleared once before it is offered.
- **A weapon that hits in a new way:** how each `Delivery` fires is one method in `WeaponRunner.cs`. Mines live in `ProjectileSystem`.

"Create Default Content" only creates assets and fills fields that are missing, so values changed in the Inspector are kept.

## Models

Every model is built by `Tools/blender/build_models.py` from many small bevelled parts, and has four material slots in a fixed order: Base, Accent, Dark and Glow. The game assigns the real materials while it runs, so an accent shows the state a weapon applies and a glow shows the biome or a status. Enemies and the lobby character have separate leg and arm models that the game swings as they walk; species without legs squash and stretch instead. A mini boss or boss is the same model drawn larger. To rebuild all models, run this from the project folder:

```bash
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/blender/build_models.py -- Assets/Game/Art/Models ArtSource/pulse_models.blend
```

A data asset without a mesh is shown as a plain block, so new content works before it has a model.

## Look

- **Post-processing, fog and light** are set in one file, `Scripts/Game/Look.cs`: bloom, tone mapping, vignette and a slight grade.
- **Glow:** the state colours and Pulse green are the only glowing materials, so bloom marks exactly the things that matter in a fight.
- **Biomes and scenery** are listed in `Data/World/Scenery.asset`: for each biome its floor colours, ambient light, glow, props and species, plus the cliff rim of the world and the pillars around a dungeon. Counts, sizes, which props block the machine and which of those are hard are set there. Props are placed from a seed, so the world looks the same every run until the seed changes.
- **The floor** is generated (`Scripts/Game/GroundBuilder.cs`): the shades of a biome form patches with natural outlines, taken from smooth noise. Neighbouring shades are close, so the floor reads as level.
- **Weapon effects:** bolts spin, pulse and leave streaks; the oil sprayer lobs a glob that splashes into a pool; the flamethrower sprays a jet of cubes; the tesla coil throws jagged arcs. Enemies flash white when hit and burst into debris.
- **The lobby** is described by `Data/World/Roundhouse.asset`.
- **Particles** are small cubes (`Scripts/Combat/Vfx.cs`): sparks on hits, debris on kills, flames on burning ground, dust behind the wheels.
- **Ground zones** are particles only: flat tiles on the ground plus whatever rises from them. They have no fill and no outline.
- **The storm** (`Scripts/Game/Storm.cs`) is six flat sheets of fog at different heights with one cloud pattern at different scales, so it has depth when the camera moves and never pops. Lightning is drawn by `Fx.Lightning`.

## Notes

- Enemies are plain data in one array and are drawn with instancing; there is no GameObject per enemy.
- With Unity's default settings, changing a script while the game is playing breaks the running session. Stop Play first.
- Tests and capture scripts must not touch the player's profile: set `Profile.Key` to a name of their own first and call `Profile.Erase()` when they are done. The key goes back to `pulse.profile` every time Play starts.
- The lobby's collisions are in `LobbyWalk.cs`: areas the character may be in, and circles and boxes it walks around. A new object gets a `walk.Block` call next to where it is placed, with half its width and half its depth.
- After changing `Assets/Game/UI/Pulse.uss`, reimport it (right-click, Reimport). The theme the screens use does not always notice the change by itself.
- Plates have a thickness (`Scripts/UI/Plate.cs`, gui-style.md section 4). It is set per class in `Pulse.uss` with `--plate-depth`, and it is drawn under the element, so a plate must not have `overflow: hidden`.
- A new room is a class that derives from `LobbyRoom`: it says where its machine stands and furnishes itself in `Furnish`, with `Solid` and `Round` for everything that blocks the way, and moves what moves in `Run`. Places are given with `Side(toWall, depth)`, so the room comes out right whichever side its wall is on. Its models are listed by name in `PulseSetup` and come from `Tools/blender/build_models.py`.
- Wall pieces and anything else built from several boxes must not have two faces in the same plane: they flicker. Let one stand a few centimetres proud of the other.
- Warnings and marks on the ground are drawn by `Scripts/Combat/Marks.cs`: `Ring`, `Lane` and `Fissure` for one frame at a time, `Crack` and `Scorch` to stay and fade.
- Two areas of `LobbyWalk` that only touch cannot be walked between: the character has to fit into one of them with its whole width. Let a doorway overlap both sides by more than the character is wide. The PlayMode test of the Tuning Room walks through its door for that reason.
- The arms in the lobby are driven by `Workshop.cs`: where they stand, how long they work and how far the deck turns are constants at its top. The arm is three pieces (`Arm_Upper` 2.0 m, `Arm_Fore` 1.6 m, tool 0.4 m) bent to reach a point; change the lengths in the model script and in the constants together.
- How tight the machine turns is `turnRate` in `Data/Machine/Wheels.asset`, in degrees per second. A lower number means a wider arc. The three Nitro values are in the same asset.
- Fonts: Chakra Petch and Barlow, both under the SIL Open Font License. The licence texts are next to the font files.
