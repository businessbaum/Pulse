# GUI style: "Plate and Pulse"

Steel plates with cut corners and a visible thickness on a dark background, one green signal colour for the Pulse, and five state colours that are used for nothing else. Every screen in the game follows these rules: HUD, menus, lobby panels and tooltips.

A rendered reference is in gui-style-preview.html. It shows colours, type and shapes; it was made before plates had a thickness (section 4) and does not show that.

## 1. Principles

1. **Flat colours, real thickness.** The 3D art uses flat colours on low-poly shapes, and the interface does the same. No gradients, soft shadows, glows, textures or rounded corners. But nothing is paper: every plate is a piece of steel with a thickness, lit from the top left, and depth is shown the way the 3D art shows it, with more flat shapes. See "Thickness" in section 4.
2. **One accent.** Pulse green means "act here" or "this is alive". It marks the selection, the confirm button, Power, Cores and live wires. Nothing else is green.
3. **State colours belong to states.** The five state colours appear only where that state is meant. They are never used for rarity, warnings or decoration.
4. **Never colour alone.** Every state has its own glyph, every slot type its own shape, and rarity is shown with notches. The game stays readable without colour vision.
5. **One pulse per screen.** At most one element pulses at a time. It is the thing the player should look at next.
6. **The schematic is the centrepiece.** Wherever the machine is shown in a menu, it is drawn as the same wiring diagram. The HUD of a run does not show it: in a fight only Hull, Nitro and Power matter.

## 2. Colours

| Token | Hex | Used for |
|---|---|---|
| `soot` | `#14171C` | Screen background |
| `plate` | `#1F242B` | Panels, cards, buttons |
| `plate-2` | `#2A313A` | Focused or raised plates, hatch pattern |
| `seam` | `#3D4752` | Outlines, dividers, idle wires, empty slots |
| `chalk` | `#E8E4DA` | Main text, Hull, filled slots |
| `dust` | `#9AA3AD` | Secondary text, labels, Scrap |
| `pulse` | `#5CF2A0` | The single accent |
| `heat` | `#FF5A3C` | Heat |
| `shock` | `#FFD93B` | Shock |
| `cryo` | `#5CD6FF` | Cryo |
| `oil` | `#A77BFF` | Oil |
| `magnet` | `#FF5FB4` | Magnet |

There is no red for danger and no rarity colours. Red appears in exactly one place, and it is in the world, not the interface: the heart that enemies drop. Warnings, locked and disabled elements use a diagonal hatch of `plate` and `plate-2`.

## 3. Type

| Role | Font | Size at 1080p | Notes |
|---|---|---|---|
| Display | Chakra Petch Bold | 40 | Uppercase |
| Title | Chakra Petch SemiBold | 24 | Names of modules, screens, machines |
| Label | Chakra Petch SemiBold | 14 to 16 | Uppercase, wide letter spacing |
| Numbers | Chakra Petch SemiBold | Any | Every number in the game uses this font |
| Body | Barlow Regular | 18 | Sentence case |
| Small | Barlow Regular | 14 | The smallest size allowed |

Both fonts are free under the Open Font License. Sizes scale with the resolution, and a UI scale setting covers 80 to 130 percent.

## 4. Shape and spacing

- **Cut corners.** Every plate has its top-left and bottom-right corner cut at 45 degrees: 12 px on panels and cards, 6 px on buttons and chips.
- **Outline.** 2 px in `seam`. Focus changes the outline to `pulse`.
- **Spacing.** A 4 px grid with the steps 4, 8, 16, 24, 32 and 48.
- **Padding.** 16 px inside a plate, 24 px between plates.

**Thickness.** The light comes from the top left, everywhere. A plate shows it in two ways, both flat shapes with hard edges:

- **Side.** Under the plate its side shows: one plain dark band of the same cut-corner shape, a few pixels lower. It has a little of the outline's colour in it, so the side of the highlighted plate is its own. A panel is 10 px thick, a card or a tile 6, a row in a list 4, a sign 3, a chip 2. A button's face has none of its own, because it stands on its slab.
- **Outline.** The 2 px outline is lighter along the top and the left and darker along the bottom and the right. This also holds when it is `pulse` or `chalk`.

**One edge.** A plate has one edge and not two. The side has no outline of its own, the slab under a button has none either, no line runs inside the outline, and nothing throws a shadow. Two parallel lines along an edge read as a drawing mistake, not as depth.

Three conditions follow from this:

- **Raised.** The one in use among several (the highlighted card, tile or row) stands 2 px higher than the others, and that much more of its side shows.
- **Sunk.** What is not there to be used (a part that is not found, a frame that is not owned, a disabled button) is let into the surface: no side, and the light and dark of its outline change places.
- **Words.** Large words (display, title, numbers) and words that lie on the picture of the world have a hard shadow 2 to 5 px below them. Small text on a plate has none.

The side is drawn below the plate, in the gap to whatever comes next. It takes no room in the layout, so the gap between two plates must be larger than the thickness of the upper one.

## 5. Slot shapes and the schematic

| Slot | Shape |
|---|---|
| Hardpoint | Square |
| Bay | Circle |
| Support slot | Diamond |
| Drive slot | Wide bar |

- An empty slot has a dashed `seam` outline. A filled slot has a solid `chalk` outline and the module's glyph inside.
- A weapon shows its state glyph filled. A Feed shows the same glyph as an outline.
- A wire is `seam` when idle and `pulse` when the system is changing something at the other end. This shows the player at a glance which combinations are active.
- The same schematic appears in the build screen at full size and on upgrade picks to show where a module would go.

## 6. Components

**Button.** A plate with a label, standing on a slab. The slab has the same cut-corner shape, is drawn 6 px lower, has no outline and is a flat, darker colour: `soot` under a secondary button, dark green under a primary one. Every plate has a thickness; what makes a button look pressable is that its face moves. Primary buttons are filled with `pulse` and have dark text; there is one per screen. Secondary buttons are `plate` with a `seam` outline. Disabled buttons are hatched, with `dust` text, and do not move.

- Hover or focus: the outline turns `pulse` (on a primary button the fill brightens) and the face lifts by 1 px.
- Press: the face moves 5 px down onto its slab within 70 ms and comes back up. A press from the keyboard or a controller shows the same movement before the button acts.

**State chip.** Glyph plus name inside a small plate outlined in the state colour. The glyphs are: triangle for Heat, bolt for Shock, hexagon for Cryo, drop for Oil, horseshoe for Magnet, dot for Kinetic.

**Module card.** Used for upgrade picks, shops and the build screen. From top to bottom:

1. Slot shape and type label, with rarity notches on the right
2. Name
3. Chips for Delivery and State
4. Effect text, two lines at most
5. Power cost as pips and the key hint

**Bars.** A bar is a groove with a rod in it: the groove is dark along its top edge and light along its bottom edge, the rod light along its top and dark along its bottom. Hull is a solid `chalk` bar. Shield is a thinner `dust` bar directly above it. Nitro is a bar of the same size next to Hull: `dust` while there is too little to light it, `pulse` once the key will do something, with the key written above it. Power is a row of pips: filled with `pulse` when in use, outlined when free. Low Hull is shown by the bar blinking, not by a colour change.

**Reaction notation.** Two state chips joined by a wire to a result plate. Unknown results show as a hatched plate with question marks. The Codex, tooltips and the upgrade screen all use this one notation.

**Enemy bars.** A mini boss or boss carries a small framed `chalk` bar over its head, with its name on a `soot` strip and notches in front of it: one for a mini boss, two for a boss. The boss that is after the machine also gets a wide bar top centre, under the place name: a hatched badge reading BOSS, its name, the bar, and one line saying what it does. A Warden's badge reads WARDEN, and its bar has a mark at half that goes away when its second phase begins.

**Title card.** For the three moments of a Warden: its entrance, its second phase and its fall. It sits in the middle of the screen, in its lower half, and is 760 px wide: a plate with a `chalk` outline and hatched ends, a small label line between two rules and two gems, the name in 62 px under it, and under the name a rule in the colour of the Warden's strand. A thin `chalk` wing with a gem at its end stands on either side. It opens from its middle, the words come up, the rule draws out and the wings grow; after about two seconds it goes back the way it came.

**Marks in the world.** Particles show what moves and spreads: fire, dust, sparks, a slick of oil, a crust of frost. An area of that kind is made of particles in its state colour and has a ragged edge, and one made by an enemy also throws up thin sparks in pale amber, the colour of every enemy warning. But a shape the player has to read at a glance is drawn as that shape, flat on the ground, in the same pale amber:

- **Ring.** Where a stomp or a burst will land: a circle as wide as its reach, with a second one that grows inside it. The blow lands when the two meet.
- **Lane.** Where a charge will run: the strip from the enemy's feet to the next wall, with its two edges drawn, filling from the near end.
- **Fissure.** Ground that is about to burst: cracks that glow in the colour of what will come out of them, hotter and whiter as the moment comes.

A warning is there for exactly as long as the enemy winds up, so it can never outlive the enemy. What a blow leaves behind stays for a quarter of a minute and fades: **cracks** in the floor where a stomp, a charge into a wall or a falling machine landed, and a dark, ragged **scorch** where something burned or blew up. Items and enemies are still not marked with rings: a ring on the ground always means "this spot is about to be hit".

**Timer bar.** For anything that takes a moment to complete, such as entering a dungeon or leaving the lobby. It sits where subtitles would be, bottom centre: a title-sized line of text above a framed bar that fills with `pulse`.

**Sign.** A small plate with a label that hovers over an object in the world and bobs gently. Signs for things the player cannot use yet have `dust` text.

**Prompt.** For something the character stands next to. A small plate at the bottom centre with two things on it: the key to press on a key cap filled with `pulse`, and the name of what it opens. The key cap pulses slightly. The plate comes up from below the screen in 180 ms and drops away the same way. It says nothing else, except for a door that has to be bought: then the price follows the name, and with too few Beats the key cap is `seam` and the price is `heat`.

**Lamp.** A small square with three conditions: off, steady and pulsing. It marks "something new" on lobby machines, menu tabs and Codex pages.

**Tag.** A small filled label in the top right corner of a card or a tile, with a darker lower edge so that it stands up like a tab. `pulse` with dark text means "this one": NEW on something the player has not looked at (looking at it removes it), ACTIVE on the machine that stands on the turntable, the Mark of a frame the player owns. `seam` with `chalk` text only states something: UNSAVED, a price. A sign over a lobby machine that has something new gets a `pulse` outline and says how many.

**Rarity.** Five slanted notches in a row, of which as many are `chalk` as the ability is good: one for Common, five for Legendary. A plain part shows none. Rarity is never a colour: every colour that could stand for it already means a state or the Pulse.

**Kind symbol.** Each kind of part has a line symbol, drawn in code: a crosshair for weapons, a node with three wires for systems, a shield for armor, a wheel for drives, a six-sided cell with a `pulse` core for reactors, a chassis on two wheels for frames. It stands in front of the kind's name wherever kinds are listed, and looks stamped out of the plate: the same lines once more, dark and 2 px lower, under the light ones.

**Save mark.** A small `pulse` diamond in the bottom right corner that lights for a second whenever the game has saved. It has no text and nothing can be done with it.

**Option row.** In the options: a plate with the name of the setting on the left and its current choice between two small buttons on the right. The highlighted row is outlined in `pulse`; left and right step through the choices.

**Part tile.** A square plate with the shape of the slot the part goes in, the glyph of its state inside, and its name under it. The shape stands on the tile like a key on a board, with its side showing below it. A part that has not been found is the same tile sunk and hatched, with a flat empty shape and the words NOT FOUND.

**Pickup log.** In a run, on the right edge between the top and the middle. Every part and every Beat the machine brings in gets a line: a small plate with the picture of the part, its name, its rarity notches and the name of its ability, or a `pulse` gem, the amount and the word BEATS. A new line comes in from the right on top of the others, stays for four and a half seconds and fades; at most five show. Beats that come in while their line is still there are added to it, and the line swells for a moment. Hearts and other power-ups are not listed: they are used up on the spot. What a cleared dungeon pays is listed here too, not in the line at the top.

**A choice of cards.** When the run offers Echoes, it stands still behind the dark overlay and up to three plates lie side by side under one word. A card is 330 by 400: its sign large at the top, its name, a chip for its strand if it has one, and one sentence. The highlighted card is raised and has the Pulse green edge. There is no button: the card is the button. Nothing else on the screen competes with it.

**Signs of Echoes.** An Echo of a strand is that strand's shape in a ring, in the strand's colour. The others each have a sign of their own, drawn in chalk with round line ends: two arcs for Second Beat, a horseshoe for Undertow, the hairpin for Crescendo, two jets, two notes under a beam, a ring that comes round on itself, two Beats, two nodes on a wire. On the HUD the held ones are 52 pixel plates in a row over the Hull bar; a number on such a plate is Pulse green and stands in its lower right corner.

**What is owed.** A sum the game owes the player is a green tag with a plus sign, "+30", in the corner where NEW would stand. A button that leads to where something is owed says so the same way: "CODEX +85".

## 7. Motion

- Transitions take 120 ms with no bounce and no overshoot.
- The pulse is one shared animation: a slow fade between full and half brightness, about once per second.
- Panels slide in from the side they belong to. They do not fade or scale.
- On the end-of-run screen the pieces rise into place one after another, 70 ms apart, and numbers count up in under a second.
- Going from one place to another is done with the iris: the dark around the world closes in on the machine in a circle, and opens again in the new place. The machine never leaves the picture, so there is no cut. The iris is drawn in the scene, not in the interface, so that it is graded exactly like the dark it has to match.
- Screen shake and flashes can be switched off in the settings.

## 8. Screen rules

- **As few words as possible.** A screen shows names, numbers and the one line that says what a part does. It does not explain itself: no key hints in footers, no sentences under titles, nothing that repeats what a shape or a colour already says. The controls are listed in one place, the pause menu. Before adding a line of text, ask whether the player decides anything by it.
- **Pointer.** The mouse pointer is only there while there is something to click: a menu or a screen. While the machine drives, the character walks or a scene plays, it is hidden.
- **Lists of parts are grouped.** Wherever the player's parts are listed, one line stands for a kind of part, with how many of it there are, and the parts of that kind are listed one by one under it or next to it. A list never repeats a name line after line.
- **HUD.** Only the edges of the screen are used. Hull and Nitro sit side by side bottom-left; time, kills, reactions and Beats sit along the top. The Beats there are the ones the machine carries in this run, not what the player owns: they are lost with the machine, so the total would promise too much. the place and the next goal sit top centre; what was just picked up sits on the right edge, between the top and the middle. Nothing else: Power and the machine's name belong to the lobby, where they can be changed. The centre stays empty. The Beats counter throbs in the rhythm of the Beats on the ground.
- **Screens of lobby machines.** One panel of 1400 by 800 with a header, three columns and a footer. The header holds the machine's name and, on the right, the one number the screen is about. The columns read from left to right, from the general to the particular: what there is, the thing chosen, its details. The highlighted item of the column in use is outlined in `pulse`; the chosen item of the other columns is outlined in `chalk`, so the path through the screen stays visible.
- **Menus.** One primary action per screen, always bottom-right. Back is always bottom-left.
- **Fixed sizes.** A menu panel has a fixed width and height, and so does every region inside it. Content that changes is refilled into rows that already exist; rows that are not needed are hidden, not removed. Text that may be too long is cut off with dots. Nothing on a menu moves because something else was highlighted.
- **Pause menu.** A column of full-width buttons with Resume first as the primary, then the controls of the place it was opened in. This is the one menu where the primary is not bottom-right, because the whole menu is a list of choices.
- **End of a run.** A hatched banner with the headline in 78 px, a `pulse` line under it, who destroyed the machine and where, eight number tiles, a row of chips with smaller facts, and one full-width primary button to go back. The pieces arrive one after another and the numbers count up. The button is the one thing that pulses.
- **Controller first.** Every screen works with a stick and two buttons. The focus outline is always visible, and mouse hover uses the same look as focus.
- **Text length.** Layouts leave room for text that is 30 percent longer than the English, so a German translation fits.
- **Words.** Labels are one or two words. Effect text says what happens in plain words and gives numbers only where the player decides by them.

## 9. Building it in Unity

- Use UI Toolkit. All colours, sizes and spacings above live in one shared style sheet, `Pulse.uss`, and screens never define their own.
- Cut corners are drawn in code by one shared `Plate` element, so no sprites are needed. It also draws the thickness: how thick a kind of plate is comes from the style sheet (`--plate-depth`), and `Raised` and `Sunk` are set by the screen.
- A plate must not have `overflow: hidden`: that cuts off its own side, which is drawn under it. Whatever has to stay inside a plate clips itself, as the hatch does.
- Wires, slots and glyphs in the schematic are drawn in code too, so any frame layout can be shown without new art.
- The colours exist twice: in `Pulse.uss` for style sheets and in `Tokens.cs` for drawn elements and 3D materials. Change both together.
- Buttons are the shared `PlateButton` element and the hatch pattern is the shared `Hatch` element, both next to `Plate`.
- After changing `Pulse.uss`, reimport it (right-click, Reimport). The theme that screens use does not always notice the change by itself.
- The two fonts are in `Assets/Game/UI/Fonts`, next to their licence files. The weight comes from the font file, so the style sheet never asks for bold.

## 10. Checklist for a new screen

- Is anything green that is not selection, confirm, Power, Cores or a live wire?
- Is a state colour used for something other than its state?
- Is anything shown by colour alone?
- Is more than one element pulsing?
- Is there exactly one primary button, bottom-right?
- Is there a line of text the player does not decide anything by?
- Does the panel keep its size and position whatever is highlighted?
- Does every plate have the two cut corners and no rounded ones?
- Does every plate that stands on something have a thickness, and is the gap below it larger than that?
- Is the light from the top left everywhere: light edges up and left, dark edges down and right?
- Does any edge show two parallel lines? Then one of them has to go.
- Does the text still fit when it is 30 percent longer?
