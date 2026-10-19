# Machine concept (working title: "the Rig")

The player is a machine built from a frame, a reactor, a drive, weapons, systems and support modules. Wires on the frame connect the slots, and those wires decide which parts combine. The build is always visible on the machine's body.

All names are placeholders. The rules below work for a mech, a ship, a train or anything else.

Related files: lore.md (world and story), lobby.md (home base), gui-style.md (interface rules).

## 1. Anatomy

| Part | What it does |
|---|---|
| **Frame** | The body. Sets how many slots exist, where they sit, how they are wired, plus base speed and hull. Picking a frame is the "character select". |
| **Reactor** | Sets the Power cap. Every module costs Power. Each reactor has one passive. |
| **Drive slot** | One per frame. The drive decides how the machine moves and handles. |
| **Hardpoints** | Weapon slots on the outside of the frame. On screen each goes by where it sits: Front, Turret, Rear. |
| **System slots** | Slots inside the frame for systems. A system changes whatever it is wired to. (Called system slots until the eighth round.) |
| **Armor slots** | Slots for plates: what takes damage off hits, adds hull or answers a hit. Some are wired to a system slot, some are not. (Called armor slots until the eighth round.) |
| **Sockets** | A slot that takes either a system or an armor module. A system in a socket acts on what the socket is wired to, exactly like one in a system slot. Small frames have sockets, so that one slot still gives a real choice. |
| **Wires** | Fixed connections between slots, drawn on the frame. A system only affects slots on its wires. |

The frame everyone starts with is the Buggy: one weapon on a turret and one socket wired to it. Larger frames are bought (section 6). The largest so far is the Crawler:

```
             {Armor 1}                    {Armor 2}
                 │                            │
[Front]────(System 1)────[Turret]────(System 2)────[Rear]

{Drive}     {Armor 3, unwired}
```

Each system slot reaches two hardpoints and one armor slot. The Turret is wired to both, so it is the slot where two systems stack on one weapon. A slot is numbered on screen only when its frame has several of that kind: the Rover's one system slot is just "System".

Any weapon fits any hardpoint. The position decides how it aims:

- **Front** fires where the machine is heading. Hardest to aim, so it gets a damage bonus.
- **Turret** auto-aims in 360°. No bonus.
- **Flank** fires sideways from both sides.
- **Rear** fires or drops behind the machine. Rewards leading enemies through your trail.

## 2. Weapons

Every weapon has two tags. Together they decide how it plays and what it can combine with.

- **Delivery** is how it hits: Projectile, Beam, Area, Deployable or Contact.
- **State** is what it leaves on the enemy: Heat, Shock, Cryo, Oil or Magnet. Weapons without a state are Kinetic: highest raw damage, and the only ones a Feed system can convert (see 3.2).

A state does little by itself; the reaction is the payoff. Heat burns for small damage over time, Oil slows, Shock stuns for a moment, and Cryo chills: a chilled enemy keeps 65 percent of its pace. Magnet is not built yet.

Starter roster:

| Weapon | Delivery | State |
|---|---|---|
| Autocannon | Projectile | Kinetic |
| Railgun | Projectile | Shock |
| Magnet Harpoon | Projectile | Magnet |
| Flamethrower | Beam | Heat |
| Coolant Jet | Beam | Cryo |
| Tesla Coil | Area | Shock |
| Oil Sprayer | Area | Oil |
| Mortar | Area | Kinetic |
| Mine Layer | Deployable | Kinetic |
| Saw Ram | Contact | Kinetic |

Ten weapons are built. Six of them are on the list above or stand in for one of it (the Longshot for the Railgun's long reach before there was one, the Frost Jet for the Coolant Jet):

| Weapon | Delivery | State | How it fights |
|---|---|---|---|
| Autocannon | Projectile | Kinetic | Fast shots at whatever is nearest |
| Longshot | Projectile | Kinetic | One heavy slug at a time, from far away |
| Railgun | Projectile | Shock | One bolt that goes through five enemies before the sixth stops it. Rewards lining them up. |
| Flamethrower | Beam | Heat | A short cone |
| Frost Jet | Beam | Cryo | A short cone |
| Tesla Coil | Area | Shock | A pulse around the machine |
| Oil Sprayer | Area | Oil | Lobs a pool |
| Mortar | Area | Kinetic | Lobs a shell that bursts where it lands, 2.7 m around. Slow: a weapon for crowds. With a feed on its wire, the state stays on the ground where the shell burst. |
| Mine Layer | Deployable | Kinetic | Lays a mine every second and a half while something is near. A mine lies for 22 seconds and goes off under the first enemy that walks onto it. At most fourteen lie at once. On a rear mount they fall behind the machine, which is where whatever follows it will walk. |
| Saw Ram | Contact | Kinetic | Cuts what is right in front of it, five times a second. The cut is 60 percent of its damage on a machine that stands and 160 percent at top speed, more with Nitro: it is the weapon for ramming. |

A part is raised from level 1 to 5 at the Tuner, between runs (section 6.1).

Weapons are preset. The player never edits a weapon's internals and there is no weapon crafting. Variety comes from which presets are mounted, where, and what they are wired to.

## 3. Synergies

There are three layers. Each one works at a different moment: reactions in combat, wiring while building, fusions as the goal of a run.

### 3.1 Reactions (on the enemy)

When an enemy carries two different states, they react and both states are consumed. Five states give ten reactions, which is small enough to learn and large enough to build around.

| | Shock | Cryo | Oil | Magnet |
|---|---|---|---|---|
| **Heat** | Plasma | Thermal Shock | Firestorm | Slag |
| **Shock** | | Superconductor | Flash | Coil |
| **Cryo** | | | Black Ice | Shard Burst |
| **Oil** | | | | Tar Ball |

- **Plasma** (Heat + Shock): arcs jump from the target and ignore armor.
- **Thermal Shock** (Heat + Cryo): armor cracks; the target takes more damage from everything.
- **Firestorm** (Heat + Oil): a burning pool that spreads to other oiled enemies.
- **Slag** (Heat + Magnet): the kill drops extra scrap. This is the economy reaction.
- **Superconductor** (Shock + Cryo): lightning chains between frozen enemies without losing damage.
- **Flash** (Shock + Oil): instant explosion, where Firestorm is a slow burn.
- **Coil** (Shock + Magnet): the enemy becomes a pylon that zaps its neighbours until it dies.
- **Black Ice** (Cryo + Oil): enemies lose footing and slide into each other for collision damage.
- **Shard Burst** (Cryo + Magnet): the target shatters and the shards fly to the next magnetized enemy.
- **Tar Ball** (Oil + Magnet): nearby enemies clump into one slow mass that area weapons hit all at once.

Six of the ten are built, the six that need no Magnet. As they are built:

| Reaction | What happens |
|---|---|
| Firestorm | Burning ground for 4 seconds, 2.6 m around, that sets every pool of oil it touches alight |
| Flash | 30 damage at once, 3.2 m around |
| Plasma | Arcs to up to four enemies within 6 m, 18 damage each, past their armor |
| Thermal Shock | 24 damage past armor to everything within 2.4 m, and for 6 seconds whatever it hit takes 40 percent more damage from everything |
| Superconductor | Lightning that jumps from enemy to enemy, up to six times and up to 7 m each time, and deals the same 16 damage at every jump |
| Black Ice | Everything within 3.6 m loses its footing for 1.3 seconds, slides away from the middle and takes 12 damage, plus a quarter more for every other enemy that stood there with it, up to three times as much |

Thermal Shock is the reaction against one tough target, Superconductor against a line, Black Ice against a crowd, and Black Ice is also the one that buys time.

### 3.2 Systems and wiring (on the machine)

A system changes every weapon wired to it. There are three kinds.

**Shapers** change how a weapon delivers. The effect depends on the weapon's Delivery tag, so one system produces a different result on each weapon:

| Shaper | Projectile | Beam | Area | Deployable |
|---|---|---|---|---|
| **Splitter** | Forks into three | Refracts to two more targets | Adds a second, outer ring | Drops two smaller copies |
| **Capacitor** | Holds fire, then one piercing shot | One wide burst | One large nova | All trigger at once |
| **Gyro** | Shots orbit the machine | Beam sweeps a full circle | Zone follows the machine | Copies orbit as satellites |

**Feeds** give a state to Kinetic weapons on their wires. There is one per state: Incendiary, Arc, Cryo, Oil and Magnet Feed. A Mine Layer on a Cryo Feed lays freezing mines. Built: the Incendiary, Arc, Cryo and Oil Feed, so every reaction that exists can be started by a Kinetic weapon on the right wire.

**Relay** links two weapons. When the first one hits, the second fires at that spot. A Magnet Harpoon relayed to a Mortar drops a shell on whatever the harpoon tagged.

About twenty parts (ten weapons, three shapers, five feeds, the relay) already produce well over a hundred distinct weapon behaviours, and none of them needs to be designed one by one.

### 3.3 Fusions (the run goal)

Two weapons at level 5 that share a system slot can be fused at a workshop. The result is a unique weapon that takes one hardpoint and frees the other. These are hand-made; eight to ten is enough for a first version.

| Parts | Fusion | Effect |
|---|---|---|
| Flamethrower + Tesla Coil | Plasma Lance | Continuous beam that chains between enemies and ignores armor |
| Coolant Jet + Railgun | Ice Lance | Pierces the whole screen and freezes the line; frozen targets shatter in sequence |
| Magnet Harpoon + Saw Ram | Reel Saw | A saw on a tether that launches, bites and drags enemies back into the machine |
| Mortar + Tesla Coil | Storm Shell | Shells leave a thundercloud that keeps striking the area |
| Mine Layer + Oil Sprayer | Powder Trail | A continuous fuse behind the machine; lighting it detonates the whole line |

### 3.4 Example build

Crawler frame with Flamethrower (Front), Oil Sprayer (Turret) and Mine Layer (Rear).

- System slot 1 holds a **Gyro**. The flame beam sweeps a circle and the oil zone follows the machine. You drive around inside a moving Firestorm.
- System slot 2 holds a **Cryo Feed**. The Mine Layer is Kinetic, so its mines now freeze. The Oil Sprayer already has a state and is unaffected.
- Enemies chasing you hit a mine on oiled ground. That triggers Black Ice, and they slide forward into the fire.

Three weapons and two systems produce a loop the player assembled, not one the game handed over.

### 3.5 Echoes

Echoes sit on top of the three layers. An Echo is a fragment of the Pulse that changes one rule for the rest of a run, for example "every reaction triggers twice". Echoes need no slot and no Power, and a run holds at most three. They are the main source of extreme combinations, and they are what makes a run grow: the machine is built at home and stays as it is, and Echoes are what it gains on the way. The story behind them is in lore.md, section 5.

**How one is got.** Three things give an Echo up: a cleared dungeon, a Warden, and a boss of the open world. It lies on the ground as a shard of the Pulse with two smaller ones beside it, and it lies until it is taken. Driving onto it stops the run and puts three cards on the screen. One is chosen, with left and right and the confirm key or with a click; there is no way to take none. A machine that holds three already gets 25 Beats for the shard.

**What is offered** comes from a pool. Eight Echoes belong to no strand and are in it from the first run. The other four are fragments of a strand, and each joins the pool once its dungeon has been cleared for the first time, in any run. An Echo that would change nothing for the machine is not offered.

| Echo | Strand | Joins after | The rule it bends |
|---|---|---|---|
| **Second Beat** | | | Every reaction goes off twice. The second comes 0.4 seconds later and is half as strong. |
| **Undertow** | | | Drops fly to the machine from nine metres away. |
| **Crescendo** | | | Every kill adds 1 percent damage, up to 40. Any hull lost ends it. |
| **Afterburn** | | | Nitro refills twice as fast, and while it burns the machine leaves fire behind. |
| **Double Time** | | | Every weapon fires a quarter faster. |
| **Encore** | | | Once in a run, the blow that would destroy the machine leaves it with 30 percent of its hull and throws everything around it back. |
| **Heavy Purse** | | | Beats count double. Nothing leaves a heart. |
| **Long Wire** | | | Every system reaches every weapon, wired to it or not. Only offered to a machine with a system and a weapon that have no wire between them. |
| **Ember** | Heat | Kiln's Keep | What dies burning sets its neighbours alight. |
| **Afterimage** | Shock | Cinder Pit | Every tenth hit chains lightning through three enemies. |
| **Deep Cold** | Cryo | Rime Vault | Chill lasts twice as long, and a chilled enemy keeps 40 percent of its pace. |
| **Seep** | Oil | Tar Sump | Oil the machine lays lies three times as long. |

The held Echoes show as small plates over the Hull bar. Crescendo counts on its plate, and the plate of an Encore goes dim once it is used up. The pause menu says what each of them does.

Some of them are choices about how to play, not plain gains: Heavy Purse trades hearts for Beats that are lost with the machine anyway, Crescendo pays only for a driver who is not hit, and Afterburn turns the dodge into a weapon.

## 4. Support modules

Since the eighth round the slot these go in is called an armor slot, and the three modules that are built are all plates: Plating, Heavy Plating and Reactive Plating. The rest of this section is the plan from before that.

Support modules cover everything that is not a weapon: moving, surviving and collecting. They sit in their own slots, so they never compete with systems for a system slot. They do cost Power, and that is where the trade-off against firepower comes from.

### 4.1 Machine stats

- **Hull** is health. At zero the run ends.
- **Armor** removes a flat amount from every hit. Strong against many small hits, weak against big ones.
- **Shield** is a second bar over the hull that refills after a few seconds without damage.
- **Speed** and **Handling** are top speed and turn rate.
- **Nitro** is a tank that fills by itself. Holding the Nitro key while driving forward burns it for a burst of speed: 1.7 times top speed for up to 2.5 seconds. It refills in about ten seconds, starting shortly after the key is let go. Lighting it takes a fifth of a tank. It is the machine's dodge.
- **Pickup range** is how far away scrap is collected.

### 4.2 Drive

Each frame has one drive slot. The drive decides how the machine handles, which changes how the hardpoint positions play.

| Drive | Handling | Special |
|---|---|---|
| **Wheels** | Fastest, wide turning circle | Collision damage grows with speed |
| **Tracks** | Slow, turns on the spot, cannot be pushed | Crushes small enemies it drives over |
| **Legs** | Medium, moves sideways without turning | Front weapons stay on target while dodging |
| **Hover** | Fast, drifts through turns | Ignores ground hazards |

### 4.3 Modules

Some modules have a trigger: an event that produces a hit. The Delivery tag of that hit is in brackets. Section 4.4 explains why it matters.

| Type | Module | Effect | Trigger |
|---|---|---|---|
| Mobility | **Booster** | Dash on a cooldown | Leaves a trail along the dash (Area) |
| Mobility | **Ram Prow** | No hull damage from colliding with small enemies | Each collision is a hit (Contact) |
| Mobility | **Outriggers** | Standing still raises fire rate and Armor | — |
| Protection | **Plating** | Armor | — |
| Protection | **Reactive Plating** | Some Armor | Taking a hit sets off a burst around the machine (Area) |
| Protection | **Shield Generator** | Adds a Shield bar | When the shield breaks, a pulse pushes enemies back (Area) |
| Protection | **Point Defense** | Shoots down enemy projectiles | — |
| Protection | **Decoy Launcher** | Drops a decoy that draws enemies | The decoy explodes when destroyed (Deployable) |
| Health | **Bulkhead** | More maximum Hull | — |
| Health | **Repair Drones** | Slow repair while not taking damage | — |
| Health | **Scrap Furnace** | Part of collected scrap repairs Hull instead of counting toward upgrades | — |
| Health | **Siphon** | Every reaction you trigger repairs a little Hull, capped per second | — |
| Health | **Failsafe** | Survives one lethal hit per run, then burns out | A large blast when it activates (Area) |
| Utility | **Scrap Magnet** | Larger pickup range | — |
| Utility | **Auxiliary Cell** | Raises the Power cap; costs a slot, not Power | — |
| Utility | **Scanner** | Marks elites and hidden caches | — |
| Utility | **Fabricator** | One extra option on every upgrade pick | — |

### 4.4 Triggers join the synergy system

A trigger counts as a Kinetic weapon with the Delivery tag shown above. On a wired armor slot, the system in that system slot acts on the trigger exactly as it would on a weapon:

- A **Feed** gives the trigger a state.
- A **Relay** lets the trigger fire a weapon on the same system slot, at the spot where the trigger went off.
- A **Shaper** reshapes the trigger according to its Delivery tag.

No new rules are needed; the three system kinds simply reach one slot further.

| Combination | Result |
|---|---|
| Reactive Plating + Cryo Feed | Whatever hits you is frozen |
| Booster + Oil Feed | Each dash lays an oil line to ignite or freeze |
| Decoy Launcher + Magnet Feed | The decoy's explosion magnetizes the crowd it gathered |
| Ram Prow + Arc Feed | Everything you plough through is shocked |
| Shield Generator + Relay + Mortar | A broken shield calls a mortar strike on your own position |

Modules without a trigger gain nothing from a wire, so they belong in unwired slots. Wired armor slots are scarce and worth saving for trigger modules.

Some pairings work without any wiring:

- **Scrap Furnace** with the Slag reaction: extra scrap becomes extra repair.
- **Siphon** with any build that triggers reactions constantly.
- **Tracks** or **Legs** with Front weapons: both make fixed-forward weapons easier to keep on target.
- **Outriggers** with a Capacitor: stand still, charge faster, release.

Continuing the example build from 3.4: a Booster in Support B shares the Cryo Feed in System slot 2. The dash leaves a freezing trail over the oiled ground, so the escape move sets up Black Ice behind the machine.

## 5. Limits inside a run

- **Power.** Every module costs Power and the reactor sets the cap. The choice is between more weapons, better-connected weapons and staying alive.
- **Rewiring.** Modules can only be moved between slots at a workshop, never in combat.
- **Levels.** Fusion needs both weapons at level 5, so a fusion build commits early.

## 6. Progress across runs

Progress runs on two tracks.

- **Tiers** make the machine plainly stronger. Better frames, more powerful weapons and better upgrades unlock over many runs, so a player always knows what they are working toward.
- **Combinations** reward time spent testing. The strongest builds in the game are not unlocked; they are found by wiring the right parts together, and they are allowed to be extremely strong.

One guard: a tier step should be worth less than a good combination. Otherwise the player can brute-force everything and stops combining.

There are two persistent currencies. **Beats** are the small one: every destroyed enemy can drop one, and a cleared dungeon pays a hoard of them, more the harder it is. Beats picked up in a run are **carried**, not owned: they become the player's when the machine comes home through the teleporter (section 9.4), and they are lost when the machine is destroyed. The counter in a run shows what is carried; what the player owns is shown in the Roundhouse. Parts are different: a part goes home the moment it is picked up. They buy frames and the Marks of frames at the Smelter. **Cores** are the large one, carried by Wardens, and not in the prototype yet; the table below still names Cores wherever a later price is meant.

| What persists | How it is earned | Effect on future runs |
|---|---|---|
| **Blueprints** | Rare drops, or bought with Cores | Adds a weapon, system or support module to the pool |
| **Marks (Mk I to III)** | Cores, once the blueprint is owned | A stronger version of the same part: more slots and hull for a frame, more damage and a bigger effect for a weapon or module |
| **Upgrade rarity** | Milestones | Rare and Epic options start appearing in upgrade picks |
| **Frames** | Milestones, such as winning with a given build | A new slot layout to start with |
| **Reactors and drives** | Milestones | A different Power cap and passive, or a different way of moving |
| **Codex** | Setting off a reaction, destroying a kind of Stray or carrying an Echo, each for the first time | The entry is the player's for good, and the Archivist pays Beats for it once: section 6.2 |
| **Legacy part** | End of every run, win or lose | Keep one module from this run and start the next run with it at level 1 |
| **Workshop perks** | Cores | Small conveniences: one more reroll, one more starting Power, a banish |
| **Threat levels** | Winning | Harder modifiers that pay more Cores |

Reactions and fusions start hidden. Finding them is a goal in itself, and the Codex is where a player sees how much is left.

Frames are where layouts change the puzzle, and only frames do. A frame is bought as it is. Raising its Mark makes it tougher and never touches its slots or wires: whoever wants another arrangement needs another frame.

Built so far, all bought and upgraded at the Smelter for Beats:

| Frame | Slots | Wires | Price | Hull at Mk I / II / III | Mk II / III cost |
|---|---|---|---|---|---|
| **Buggy** | 1 weapon (turret), 1 socket | The socket reaches the weapon | Everyone has it | 100 / 120 / 150, and 1 armor at Mk III | 60 / 140 |
| **Rover** | 2 weapons (front, turret), 1 system, 1 armor | The system reaches both weapons and the armor | 150 | 120 / 145 / 180, and 1 armor at Mk III | 120 / 260 |
| **Crawler** | 3 weapons, 2 systems, 3 armor | As drawn in section 1 | 500 | 150 / 180 / 225, and 1 armor at Mk III | 250 / 500 |

A new player has one machine: the Buggy with an Autocannon and Plating. The player can keep two machines. The eight parts of the first prototype and the Extender are everyone's from the start, one of each; everything else is found in dungeons, dropped by enemies or bought.

### 6.1 Parts are items: levels and abilities

Every weapon, system and armor module the player owns is one item. Two Autocannons are two items, and one item sits in one slot. An item has two things of its own.

**A level, from 1 to 5.** The Tuner raises it for Beats: 40, 90, 160 and 260 for the four steps. What a level adds depends on the kind of part:

| Kind | Each level above the first |
|---|---|
| Weapon | 12 percent more damage |
| Armor | 15 percent more of everything the plate gives: armor, hull, the damage of its burst |
| System | 4 percent more damage for everything it is wired to; an Extender also adds 15 percent to its own range bonus |

**An ability, if an enemy dropped it.** A dropped part carries one ability, and the ability has a rarity: Common, Uncommon, Rare, Epic or Legendary. The rarity is how strong that ability is. Mythic has a name and a place above Legendary, and nothing rolls it yet. The parts everyone starts with, and the ones dungeons keep, are plain: no ability.

| Ability | On | What it does | Common to Legendary |
|---|---|---|---|
| Keen | Weapon | More damage | 10, 18, 28, 40, 60 percent |
| Rapid | Weapon | Fires faster | 8, 14, 22, 32, 45 percent |
| Reaching | Weapon | More range | 10, 18, 28, 40, 55 percent |
| Searing, Arcing, Slick, Chilling | Kinetic weapon | A share of its hits apply Heat, Shock, Oil or Cryo | 6, 10, 16, 24, 35 percent |
| Thick | Armor | More hull | 8, 14, 22, 32, 45 |
| Hardened | Armor | Takes more off every hit | 0.5, 1, 1.5, 2, 3 |
| Mending | Armor | Repairs hull all the time | 0.3, 0.5, 0.8, 1.2, 2 a second |
| Spiked | Armor | Whatever hits the machine takes damage | 6, 10, 16, 24, 36 |
| Tuned | System | What it is wired to deals more damage | 4, 7, 11, 16, 24 percent |
| Wide | System | What it is wired to reaches further | 5, 8, 12, 17, 25 percent |

Searing, Arcing, Slick and Chilling are where abilities meet the synergy system: a Kinetic weapon that sometimes applies a state starts reactions by itself, with no feed on its wire.

**Where parts come from.** Any module of the game can drop. An ordinary enemy drops one 1.5 percent of the time; a mini boss and a boss always drop one, a Warden two. Who drops it decides how the rarity is rolled:

| Dropped by | Common | Uncommon | Rare | Epic | Legendary |
|---|---|---|---|---|---|
| Ordinary enemy | 60 | 27 | 10 | 2.5 | 0.5 |
| Mini boss | 34 | 38 | 21 | 6 | 1 |
| Boss | 8 | 34 | 36 | 17 | 5 |
| Warden | 0 | 14 | 40 | 34 | 12 |

A dropped part is a small case on the ground that throws up a spark now and then. The machine has to drive onto it. It goes straight to the Roundhouse: the machine is built there, not in the open world. What it was shows in the log on the right edge of the screen: its name, its rarity and its ability. Beats are listed there too, a trail of them as one line that counts up. Hearts are not.

**Scrap.** A part that is not on a machine can be broken up at the Tuner for a few Beats: 4, plus 6 for each level, plus 9 for each step of rarity. That is how a full shelf is emptied.

### 6.2 The Codex

The Archivist keeps a record of what the player has met out there, in three parts: the reactions, the Strays and the Echoes. Something is written down the first time it happens in a run, and it stays written down however that run ends. What is not known yet is a hatched plate that only hints at where to look.

The Archivist pays Beats for everything that is new to it. An entry it still owes for carries the sum on its tile, and looking at the entry is what gets it paid. So a run that ended badly still brings something home, and every run is worth a visit to the Archivist afterwards.

| Entry | Paid for it |
|---|---|
| A reaction set off for the first time | 30 |
| A kind of Stray destroyed for the first time | 10 |
| Its mini boss | 25 more |
| Its boss | 50 more |
| A Warden | 150 |
| An Echo carried for the first time | 15 |

Each entry also counts: how often the reaction has gone off, how many of the species have been destroyed, how often the Echo was carried. With six reactions, twelve Strays and twelve Echoes the whole Codex pays 1,295 Beats, once.

Planned frames, not built:

| Frame | Layout | Armor slots | Plays as |
|---|---|---|---|
| **Broadside** | Four Flank hardpoints, one system slot wired to all | One wired, two unwired | One system applied to everything |
| **Spider** | One Turret wired to four system slots | Four wired, one per system slot | One weapon with four stacked systems; defence built from triggers |
| **Hauler** | Two hardpoints, four system slots, slow | Two wired, four unwired | Few weapons, heavy modification, the most hull |

## 7. First prototype

The smallest version that tests whether the idea is fun:

- One frame (Crawler) with one drive (Wheels)
- Four weapons: Autocannon, Flamethrower, Oil Sprayer, Tesla Coil
- Three states (Heat, Oil, Shock), which gives three reactions
- Two systems: Splitter and Incendiary Feed
- Two support modules: Plating as a plain stat, Reactive Plating to test a wired trigger
- No fusions and no progress across runs

If reactions and wiring feel good at that size, everything else is content.

Status: this prototype is built in the Unity project in the `Pulse` folder, together with the run structure from section 9. Its README explains how to run it. It has since grown past the list above: four more parts that dungeons keep (the Longshot, the Frost Jet, the Arc Feed and Heavy Plating), a second reactor, a profile that remembers what was found, and machines the player builds and saves at the Fitter.

## 8. Tools

- **Engine:** Unity 6, with the Unity CLI so changes can be tested from the terminal.
- **Models:** Blender 5.2, exported as FBX. The first set is generated by a script in the project, so every model can be rebuilt with one command.
- **Module data:** one ScriptableObject asset per weapon, system, armor module and ability, holding its tags, Power cost and values. New content is a new asset, not new code.
- **Slots:** each frame model carries one empty object per slot, and modules attach to those.
- **Interface:** UI Toolkit, following gui-style.md.
- **Rendering:** Universal Render Pipeline, stylised low-poly with flat colours.

## 9. Run structure

- A run takes place in an open world. Enemies spawn there at a constant rate and never get stronger with time.
- The open world has no wall. It ends in fog that stands still and grows denser the further in the machine goes. Inside it a storm rages: wind, sheet lightning that lights the fog from within, and forked lightning that strikes the machine harder and more often the deeper it is, from 3 hull a second at the edge to 40 at the far end, 24 m in. Enemies do not follow into it.
- Enemies in the open world stay idle until the machine comes within their range. Once they have noticed it, they follow it for good. Shooting an idle enemy also alerts it.
- Dungeon entrances are spread over the world. Standing in an open entrance for a moment takes the machine inside.
- A dungeon is a closed place fought in waves. Each dungeon has a fixed difficulty, so the player chooses how hard the run gets by choosing which dungeon to enter.
- Clearing a dungeon returns the machine to the open world and opens further entrances. It pays Beats, it gives up the parts it kept, and it leaves an Echo lying where the machine comes out: see 9.2 and 3.5.
- Between the entrances there are things to find: veins of Beats and caches. See 9.5.
- One entrance leads to a Warden instead of waves: see 9.3.
- The hull carries over between the open world and dungeons. Being destroyed anywhere ends the run.

In the prototype the open world has three biomes that meet at the starting point: the Cinder Flats, the Frostline and the Tar Marsh. Each holds one dungeon, and the three unlock in a chain: Cinder Pit, Rime Vault and Tar Sump. Kiln's Keep stands in the north-east of the Cinder Flats and is open from the start. A run starts in the lobby and returns there when the machine is destroyed.

The machine is built in the lobby and stays as it is for the whole run. There is no way to change machines in the open world.

### 9.1 Species of the open world

Each biome has three species of its own. What spawns depends on the biome the spawn point lies in, so the player meets a different mix in each part of the map. The waves of a dungeon still use the plain Grunt and Brute for now.

| Biome | Species | How it fights | Mini boss | Boss |
|---|---|---|---|---|
| Cinder Flats | Cinder Mite | Weak alone, comes in packs of four | **Mite Matron**: keeps hatching mites | **Brood Mother**: hatches more and faster, and a last brood when it dies |
| Cinder Flats | Slag Ram | Shows a line, then rams along it | **Slag Breaker**: the ram ends in a shockwave | **Furnace Ram**: shockwave, and a burning trail along the ram |
| Cinder Flats | Ember Spitter | Keeps its distance and lobs a glob that burns where it lands | **Ember Mortar**: three globs side by side | **Kiln Maw**: five globs, more often |
| Frostline | Rime Stalker | Circles the machine, darts in and backs off | **Rime Hunter**: every dart leaves frost | **Whiteout**: darts twice as often, and its hit chills |
| Frostline | Frost Hulk | Slow and tough; the ground around it flickers, then it pounds that ground | **Frost Crusher**: a wider reach, and it slows | **Glacier**: wider still, and frost breaks out around it |
| Frostline | Shard Lancer | Stands off and shoots along a line it shows first | **Shard Volley**: a fan of three | **Icicle Battery**: a fan of five, twice in a row |
| Tar Marsh | Tar Leech | Fast; its bite slows the machine | **Tar Lamprey**: leaves oil where it goes | **Mire Tyrant**: oil trail, and it drags the machine toward itself |
| Tar Marsh | Bloater | Walks up, swells and bursts; the burst leaves an oil pool | **Tar Bloat**: a bigger burst that releases three Bloaters | **Sump Heart**: does not burst; throws oil around the machine, and releases five when it dies |
| Tar Marsh | Ooze | Falls apart into two small ones when it dies | **Great Ooze**: falls apart into four | **Tar Colossus**: falls apart into two Great Oozes |

Rules that hold for every species:

- **Ranks.** Any open-world spawn can be a mini boss (3 percent) or, more rarely, a boss (0.8 percent). At most two mini bosses and one boss are alive at once. The chances are constant, like everything else about the open world.
- **Same idea, more of it.** A mini boss or boss does what its species does and adds to it. It is the same model, larger, with a crown of pale motes rising around it: thin for a mini boss, twice as dense for a boss.
- **Every dangerous attack is shown first.** A line for a ram, a dart or a shot. For a slam, a burst or a glob that is about to land, the ground it will hit flickers with pale flecks that grow denser as the moment comes, and dust is drawn in from its edge. The enemy blinks white while it winds up. A line stays at full strength until the attack comes.
- **A circle means a blow is coming.** A ring on the ground says one thing: this spot is about to be hit, by a stomp or a burst, and it fills as the moment comes. Nothing else in the world is marked with a ring or a filled disc: not an area, not an item, not a boss. An area is shown by what it is made of. Burning ground is flames over scorched earth, oil is a ragged slick with bubbles, frost is a crust with ice in the air, and a pulse of electricity is a wave of sparks spreading from the machine. The only round things are the ones that are round in the fiction: a dungeon entrance and the pit under it.
- **Attacks charge up or home in.** This is the rule for every attack in the game: the player must be able to read it and dodge it. An attack either announces itself and then goes where it said, or it follows the machine slowly enough to be outrun. Nothing hits without warning.
- **How long the warning lasts is set per species** (`windUp` on the enemy asset, a multiple of the base time). The nine species of the first three biomes all use 1.6: a ram shows for 1.2 seconds, a slam for 1.4, a shot for 1.3, a dart for 0.7, and a glob is in the air for 1.5. Later biomes can be made harder simply by lowering it.
- **Hostile ground.** Fire, frost and oil left by enemies act on the machine: fire burns it, frost and oil slow it. Such an area also throws up thin sparks in the pale amber that every enemy warning uses. They still act on enemies too, so the machine can use them. Oil from a Bloater catches fire like any other oil, which makes Bloaters a free source of Firestorms for a machine with a Heat weapon.
- **Drops.** A destroyed enemy rolls a drop pool. The pool holds hearts, Beats and parts (section 6.1). A heart repairs 12 hull: 7 percent from an ordinary enemy, two from a mini boss, four from a boss. A Beat is worth 1: 75 percent from an ordinary enemy, three from a mini boss, five from a boss, ten from a Warden. Large Beats worth 5 come only from the strong: two from a mini boss, six from a boss, thirty from a Warden. Beats are Pulse green and throb in one shared rhythm, together with the counter on screen. Enemies that came out of another enemy (hatched mites, the pieces of an Ooze) and Bloaters that blew themselves up drop nothing. A heart is red, the one red thing in the game. The machine has to drive onto it to take it: nothing is drawn in from a distance until a magnet power-up exists (`Rig.Magnet` is where it will plug in). A heart lies for 25 seconds and is left alone while the hull is whole.
- **Hull bars.** Every mini boss and boss carries a small bar over its head. A boss that has noticed the machine also gets a wide bar at the top of the screen with its name and what it does.

### 9.2 Dungeons

Each dungeon has a map of its own, drawn as a grid of characters in its asset: walls, pillars, floor, the place the machine lands, the places waves come in, and vents. Walls and pillars block the machine, enemies and shots, and enemies find their way around them. A vent throws up the hazard of the biome above it again and again: fire under the Cinder Flats, frost under the Frostline, oil under the Tar Marsh.

| Dungeon | Shape | Pays | Keeps |
|---|---|---|---|
| Cinder Pit | A cross of four halls around a middle with pillars | 40 Beats | Longshot, Saw Ram |
| Rime Vault | A long hall split into three system slots by walls from both sides, with rows of pillars | 100 Beats | Frost Jet, Mortar, Cryo Feed |
| Tar Sump | A walled yard inside a ring corridor, with a gap in each of its four walls | 180 Beats | Arc Feed, Heavy Plating, Mine Layer, Oil Feed |
| Kiln's Keep | One wide arena with stands around it | 300 Beats | Kiln Reactor, Railgun |

A part a dungeon keeps is the player's from the moment the dungeon is cleared, for every later run. The harder the dungeon, the more Beats and the better the part.

Every cleared dungeon also leaves an Echo where the machine comes back out, in every run. And the first time a dungeon is cleared, the Echo of its strand joins the pool for all runs after that (section 3.5).

### 9.3 The Warden's arena

Kiln's Keep is a castle in the open world with an entrance in front of its gate. The floor of the entrance breaks like any other, and the machine lands in the arena under the keep.

- **The arena** is one wide room with four pillars and six vents. Stands run all the way round it, filled with Strays of every species. They hop and turn, wilder when Kiln lands a hit; they fall quiet when Kiln runs into a wall, and sink away when it falls.
- **The entrance.** The camera leaves the machine and travels to the gate. The gate slides open, Kiln walks in out of the dark with the ground shaking under each step, stops and brings both fists down. Its name crosses the screen on a hatched band, the crowd goes wild, the gate shuts and the camera comes back. Nothing can hurt Kiln until this is over.
- **First phase.** Kiln walks after the machine and makes one attack each time it has caught its breath. Slam: the ground around it flickers, then it pounds it and leaves fire in a ring. Charge: a thin line shows the direction and the whole lane flickers, then it rams along it and leaves a trail of fire; running into a wall leaves it dazed for a moment, which is the reward for dodging. Volley: fire is drawn into it, then it lobs five globs at and around where the machine is heading.
- **Second phase, Meltdown.** At half its hull Kiln stops, cannot be hurt for two seconds, and burns its plating off. It comes back faster, with shorter warnings, and the vents in the floor wake up. It adds Orbs, balls of fire that turn after the machine in a wide arc until they burn out or hit a wall, and Eruption, two lines of ground through the machine's position that flicker and then burst from the middle outward. A charge that misses is followed at once by a second.
- **Its fall.** Time slows, the camera goes to Kiln while it comes apart, and what is left of its Pulse goes up in one green wave. Then the machine is taken back to the open world with 300 Beats and the Kiln Reactor.

Kiln follows the same rule as every other enemy: each attack is shown first and then goes where it said, or it follows slowly enough to be outrun. Its wide bar at the top of the screen has a mark at half, where the second phase begins.

### 9.4 Going home

The machine carries a teleporter. R or the Y button starts it, and so does "Return to the Roundhouse" in the pause menu. It charges for four seconds: light is drawn into the machine from all round, streaks rise around it and a beam builds over it, wider and brighter as it fills. A bar at the bottom of the screen shows the charge. The machine may keep driving.

Any hull lost breaks the charge, whatever took it: a hit, burning ground, the storm. The light spills out of the machine, the bar says so, and the teleporter is dead for a second and a half. So it has to be started somewhere quiet, and it cannot be used to escape a fight that is going badly.

When the charge is full, the machine is drawn out into a line of light and pulled up the beam, and the dark closes in on the spot. The Roundhouse opens from that dark (lobby.md, section 2). This is how Beats get home. What the machine carries becomes the player's when it arrives, and the counter in the Roundhouse runs up by that much. A machine that is destroyed loses every Beat it carried; the end-of-run screen says how many. Found parts are safe either way, because they go home the moment they are picked up. So the question of every run is when to turn back.

### 9.5 Things to find

The open world holds more than entrances. Both kinds below stand somewhere else in every run, the same number in each biome, so the map is worth driving across each time.

- **Veins.** Beats that have grown out of the ground: a lump of rock with green shards on it that throb in the rhythm of every other Beat. There are nine in each biome. Driving into one breaks it and throws out three to five Beats, which still have to be picked up. One vein in five is rich and throws out a cluster of five besides. A vein does not block the machine; it is there to be driven through.
- **Caches.** Crates the Roundhouse once sent out. There are two in each biome, none near the starting point. A cache is solid. It opens for a machine that stays within four metres of it for six seconds; a bar at the bottom of the screen shows how far it is, and away from the cache the bar runs back. The moment the machine starts on it, the Strays of that place hear it: five of one species, or eight if it is one that comes in packs, set out for the cache from just outside the picture. It holds a part with an ability, rolled as a mini boss would drop it, fourteen Beats and a heart.

Veins make the drive between two fights worth something, and they are what a machine that is hurt can still do. A cache is a small fight the player starts on purpose, at a place and a moment of their choosing. Both add to what the machine carries, and so to what is at stake on the way home.

## Open decisions

- **Theme.** A first direction is in lore.md. Once it is confirmed, the module names here can be adjusted to fit.
- **What else Beats buy.** Frames, their Marks, the levels of parts and the door of the Tuner's room cost Beats. Blueprints are the next thing to price.
- **The socket.** Whether "one upgrade slot" on the first frame should take both systems and armor, as built, or only one of the two kinds.
- **Modules that are not plates.** Section 4 lists modules for moving and collecting. Their slot is an armor slot now and takes plates only; where the others will go is open.
- **How many parts a player may keep.** There is no limit yet; scrapping is the only way a shelf gets emptier.
- **Mythic.** The rarity above Legendary exists in name. What makes a part Mythic is open.
- **Cores.** Whether Wardens drop a second, rarer currency, or whether Beats are enough.
- **Nitro and modules.** Nitro is fixed per drive for now. Support modules that change it (a larger tank, a faster refill, a trigger on lighting it) fit section 4.
- **Dungeon rewards.** A cleared dungeon gives a full repair, Beats, the parts it kept and a choice of Echoes. Whether a harder dungeon should offer better Echoes, or more cards to choose from, is open.
- **How much Echoes give.** The twelve are a first set, with numbers that have only been tested for working, not for balance. Second Beat with a machine that already chains reactions is meant to feel unfair; whether the others are worth a slot next to it has to be played.
- **How much the map pays.** All veins and caches of a run together hold about 220 Beats, two thirds of what the first three dungeons pay. They are carried, so they can be lost. Whether that is too generous has to be played.
- **A full Codex.** It pays once. What a player who has filled it is given to look for next is open: the four reactions with Magnet, the other Wardens, fusions.
- **On foot.** Small places where the robot leaves the machine and fights weaker Strays with a blaster of its own. Proposed, not built.
- **Workshops.** Where the player rewires and fuses. Candidates are the entrance of a cleared dungeon or fixed places in the open world.
- **World layout.** Whether the map and its entrances are fixed or generated anew for each run.
- **Fixed-facing hardpoints.** Whether Front and Rear slots are fun with the chosen controls, or whether everything should auto-aim. Steering is now relative to the machine (forward, reverse, turn left, turn right), which makes aiming a Front weapon a matter of pointing the machine. First test in the prototype: an Autocannon fixed to the Front slot hit almost nothing, while the Flamethrower's cone worked well there. Narrow shots may need the Turret, or the Front slot may need some aim assist.
