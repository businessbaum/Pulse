using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pulse
{
    // The Mechanic's screen: what the machine on the turntable is built from. Three columns, read
    // from left to right, from the general to the particular.
    //   Slots   the wiring diagram of the machine and a row for its frame, each of its slots and
    //           its reactor
    //   Kinds   what fits the highlighted row: frames, reactors, drives, or the kinds of part the
    //           player has, one line for each kind however many of it there are
    //   Parts   the player's parts of the highlighted kind, one by one: each has its own level and,
    //           if an enemy dropped it, an ability
    // One part sits in one slot; picking a part that is already on the machine moves it.
    //
    // Which machine stands on the turntable is not decided here but at the display next to it; see
    // PresetScreen. Picking something puts it in at once, but only on this screen: the machine is
    // changed for good by "Save". A machine that draws more Power than its reactor gives cannot be
    // saved. Putting it on another frame keeps every part that still has a slot there.
    //
    // Follows gui-style.md: one primary action bottom right, Back bottom left, the highlighted thing
    // outlined in Pulse green. Nothing takes its size from its content: every box has a fixed size in
    // Pulse.uss and every row is built once and only refilled, so the screen never moves.
    public sealed class MechanicScreen
    {
        sealed class SlotRow
        {
            public VisualElement root;
            public Label slot, name;
        }

        sealed class PartRow
        {
            public Plate root;
            public VisualElement chips;
            public Notches notches;
            public Label name, count, cost, text;
        }

        // What one row of the left column stands for: the frame, a slot of it, or the reactor.
        struct Place
        {
            public SlotDef slot;
            public bool frame, reactor;

            public string Id => frame || reactor ? null : slot.id;
        }

        const int SlotRows = 11, Rows = 7, Pips = 16;
        const int Slots = 0, Kinds = 1, Items = 2;

        readonly GameConfig config;
        readonly Action changed;
        readonly VisualElement overlay, info;
        readonly List<VisualElement> pips = new List<VisualElement>();
        readonly List<SlotRow> slotRows = new List<SlotRow>();
        readonly List<PartRow> kindRows = new List<PartRow>(), itemRows = new List<PartRow>();
        readonly List<Place> places = new List<Place>();
        // What can go into the highlighted row: a frame, a reactor, a drive, or a kind of module the
        // player has at least one of. A null entry stands for leaving a slot empty.
        readonly List<UnityEngine.Object> kinds = new List<UnityEngine.Object>();
        // The player's parts of the highlighted kind, the best first.
        readonly List<Item> items = new List<Item>();
        readonly SchematicView schematic;
        readonly Label machineName, unsaved, powerValue, kindsLabel, itemsLabel, itemsCount, infoTitle, infoText;
        readonly PlateButton save, back;
        // The machine as it is on this screen, and which of the player's machines it is.
        Loadout draft;
        int machine;
        // The highlighted row of each column, where the two lists start, and the column in use.
        int place, kind, item, firstKind, firstItem, column;

        public bool IsOpen { get; private set; }

        public MechanicScreen(VisualElement root, GameConfig config, Action changed)
        {
            this.config = config;
            this.changed = changed;

            overlay = Ui.Add(root, new VisualElement(), "overlay");
            var panel = Ui.Add(overlay, new Plate(), "station");
            var header = Ui.Add(panel, new VisualElement(), "station__header");
            Ui.Add(header, new Label("MECHANIC"), "display");
            var power = Ui.Add(header, new VisualElement(), "station__power");
            var powerRow = Ui.Add(power, new VisualElement(), "row");
            Ui.Add(powerRow, new Label("POWER"), "label");
            powerValue = Ui.Add(powerRow, new Label(), "num-small");
            var pipRow = Ui.Add(power, new VisualElement(), "pips");
            for (int i = 0; i < Pips; i++) pips.Add(Ui.Add(pipRow, new VisualElement(), "pip"));

            var body = Ui.Add(panel, new VisualElement(), "station__body");

            // Left: the machine. Its name, its wiring diagram, and a row for everything it is built from.
            var left = Ui.Add(body, new VisualElement(), "station__list");
            var names = Ui.Add(left, new VisualElement(), "mech__names");
            machineName = Ui.Add(names, new Label(), "title");
            machineName.AddToClassList("clip");
            machineName.AddToClassList("card__name");
            unsaved = Ui.Add(names, new Label("UNSAVED"), "tag");
            unsaved.AddToClassList("tag--dim");
            schematic = Ui.Add(left, new SchematicView { SlotSize = 28f }, "mech__schematic");
            schematic.Picked = id =>
            {
                int at = places.FindIndex(p => p.Id == id);
                if (at >= 0) ClickSlot(at);
            };
            for (int i = 0; i < SlotRows; i++)
            {
                int index = i;
                var row = new SlotRow { root = Ui.Add(left, new VisualElement(), "slot-row") };
                row.slot = Ui.Add(row.root, new Label(), "label");
                row.slot.AddToClassList("mech__slot");
                row.name = Ui.Add(row.root, new Label(), "num-small");
                row.name.AddToClassList("card__name");
                row.name.AddToClassList("clip");
                row.root.AddManipulator(new Clickable(() => ClickSlot(index)));
                slotRows.Add(row);
            }

            // Middle: what fits there, one line for each kind.
            var middle = Ui.Add(body, new VisualElement(), "station__middle");
            middle.RegisterCallback<WheelEvent>(wheel => Wheel(wheel, kinds.Count, ref firstKind, ref kind));
            kindsLabel = Ui.Add(Ui.Add(middle, new VisualElement(), "station__fits"), new Label(), "label");
            for (int i = 0; i < Rows; i++)
            {
                int index = i;
                var row = Row(middle);
                row.root.AddManipulator(new Clickable(() => ClickKind(firstKind + index)));
                kindRows.Add(row);
            }

            // Right: the player's parts of that kind. For something there is only one of, what it is.
            var right = Ui.Add(body, new VisualElement(), "station__parts");
            right.RegisterCallback<WheelEvent>(wheel => Wheel(wheel, items.Count, ref firstItem, ref item));
            var over = Ui.Add(right, new VisualElement(), "station__fits");
            itemsLabel = Ui.Add(over, new Label(), "label");
            itemsCount = Ui.Add(over, new Label(), "small");
            info = Ui.Add(right, new VisualElement(), "mech__info");
            infoTitle = Ui.Add(info, new Label(), "title");
            infoTitle.AddToClassList("clip");
            infoText = Ui.Add(info, new Label(), "archive__text");
            for (int i = 0; i < Rows; i++)
            {
                int index = i;
                var row = Row(right);
                row.root.AddManipulator(new Clickable(() => ClickItem(firstItem + index)));
                itemRows.Add(row);
            }

            var footer = Ui.Add(panel, new VisualElement(), "station__footer");
            back = Ui.Add(footer, new PlateButton("Back", Close));
            // Its label changes with what it will do, so its width is fixed.
            save = Ui.Add(Ui.Add(footer, new VisualElement(), "station__buttons"), new PlateButton("Save", Save, primary: true), "station__take");
        }

        static PartRow Row(VisualElement column)
        {
            var row = new PartRow { root = Ui.Add(column, new Plate { Cut = 6f }, "part") };
            var head = Ui.Add(row.root, new VisualElement(), "part__head");
            row.name = Ui.Add(head, new Label(), "num-small");
            row.name.AddToClassList("part__name");
            row.name.AddToClassList("clip");
            row.count = Ui.Add(head, new Label(), "tag");
            row.count.AddToClassList("tag--dim");
            row.count.AddToClassList("part__count");
            row.notches = Ui.Add(head, new Notches());
            row.chips = Ui.Add(head, new VisualElement(), "chips");
            row.cost = Ui.Add(head, new Label(), "small");
            row.cost.AddToClassList("part__cost");
            row.text = Ui.Add(row.root, new Label(), "part__text");
            row.text.AddToClassList("clip");
            return row;
        }

        // A list that is longer than its column is moved with the mouse wheel.
        void Wheel(WheelEvent wheel, int count, ref int first, ref int chosen)
        {
            if (count <= Rows) return;
            first = Mathf.Clamp(first + (wheel.delta.y > 0f ? 1 : -1), 0, count - Rows);
            chosen = Mathf.Clamp(chosen, first, first + Rows - 1);
            Refresh();
        }

        // Opens on the machine that stands on the turntable.
        public void Open()
        {
            IsOpen = true;
            overlay.style.display = DisplayStyle.Flex;
            machine = Profile.Current;
            draft = Profile.Copy(Profile.Machine, Profile.Machine.displayName);
            column = Slots;
            place = 0;
            Refresh(newPlace: true);
        }

        public void Close()
        {
            IsOpen = false;
            overlay.style.display = DisplayStyle.None;
        }

        // ---- What the player can do ----

        // Called every frame while the screen is open.
        public void Update()
        {
            var nav = Nav.Read();
            if (nav.back) back.Press();
            else if (nav.save) save.Press();
            else if (nav.left && column > Slots) { column--; Refresh(); }
            else if (nav.right && column == Slots) { column = Kinds; Refresh(); }
            else if (nav.right && column == Kinds && items.Count > 0) { column = Items; Refresh(); }
            else if (nav.up || nav.down) Step(nav.down ? 1 : -1);
            else if (nav.confirm) Confirm();
        }

        void Step(int by)
        {
            switch (column)
            {
                case Slots:
                    place = (place + by + places.Count) % places.Count;
                    Refresh(newPlace: true);
                    break;
                case Kinds:
                    kind = Mathf.Clamp(kind + by, 0, kinds.Count - 1);
                    Refresh(newKind: true);
                    break;
                default:
                    item = Mathf.Clamp(item + by, 0, items.Count - 1);
                    Refresh();
                    break;
            }
        }

        void Confirm()
        {
            if (column == Slots) { column = Kinds; Refresh(); }
            else if (column == Kinds) FitKind(kind);
            else FitItem(item);
        }

        void ClickSlot(int index)
        {
            if (index >= places.Count) return;
            column = Slots;
            place = index;
            Refresh(newPlace: true);
        }

        void ClickKind(int index)
        {
            if (index >= kinds.Count) return;
            column = Kinds;
            FitKind(index);
        }

        void ClickItem(int index)
        {
            if (index >= items.Count) return;
            column = Items;
            FitItem(index);
        }

        // Puts what stands in a row of the middle column into the highlighted row of the machine.
        // For a kind of part the player has several of, that only leads on to the right column.
        void FitKind(int index)
        {
            if (index < 0 || index >= kinds.Count) return;
            var at = places[place];
            var chosen = kinds[index];
            bool other = index != kind;
            kind = index;
            if (at.frame) Reframe(draft, (FrameDef)chosen);
            else if (at.reactor) draft.reactor = (ReactorDef)chosen;
            else if (at.slot.type == SlotType.Drive) draft.drive = (DriveDef)chosen;
            else if (chosen == null) Put(draft, at.slot.id, null);
            else
            {
                List((ModuleDef)chosen);
                if (items.Count == 1) Put(draft, at.slot.id, items[0]);
                else if (items.Count > 1) column = Items;
            }
            Refresh(newKind: other);
        }

        void FitItem(int index)
        {
            if (index < 0 || index >= items.Count) return;
            item = index;
            Put(draft, places[place].slot.id, items[index]);
            Refresh();
        }

        // Puts a part into a slot. If it sat in another slot of this machine, it leaves that one.
        static void Put(Loadout draft, string slot, Item item)
        {
            var entries = new List<Loadout.Entry>();
            foreach (var entry in draft.modules)
                if (entry.slot != slot && entry.module != null && (item == null || entry.item != item)) entries.Add(entry);
            if (item != null) entries.Add(new Loadout.Entry { slot = slot, module = item.def, item = item });
            draft.modules = entries.ToArray();
        }

        // Moves the machine onto another frame. A part stays where a slot of the same name takes it;
        // everything else comes off.
        static void Reframe(Loadout draft, FrameDef frame)
        {
            if (draft.frame == frame) return;
            var kept = new List<Loadout.Entry>();
            foreach (var slot in frame.slots)
                foreach (var entry in draft.modules)
                    if (entry.slot == slot.id && Parts.Fits(entry.module, slot.type)) kept.Add(entry);
            draft.frame = frame;
            draft.modules = kept.ToArray();
        }

        void Save()
        {
            if (!Edited() || Over(draft)) return;
            Profile.Replace(machine, draft);
            changed();
            Refresh();
        }

        // ---- The same, without the mouse or a key: what a test calls ----

        public Loadout Shown => draft;
        public bool HasChanges => Edited();
        // How many lines the middle column has, and how many parts the right one.
        public int KindCount => kinds.Count;
        public int ItemCount => items.Count;

        // Puts something into a row of the machine. The row is a slot's id, "frame" or "reactor".
        // For a module, the player's best spare part of that kind is taken. Pass null to empty a slot.
        public bool Fit(string row, ScriptableObject chosen)
        {
            if (!Go(row)) return false;
            if (chosen is ModuleDef module)
            {
                var spare = Profile.Spare(draft, module);
                return spare != null && Pick(spare);
            }
            int at = kinds.IndexOf(chosen);
            if (at < 0) return false;
            ClickKind(at);
            return true;
        }

        // Puts one particular part into a slot.
        public bool Fit(string row, Item item) => Go(row) && Pick(item);

        // Highlights a row of the machine and the line of one kind of part, as the keys would.
        public bool Look(string row, ModuleDef module)
        {
            if (!Go(row)) return false;
            int at = kinds.IndexOf(module);
            if (at < 0) return false;
            column = Kinds;
            kind = at;
            Refresh(newKind: true);
            return true;
        }

        bool Go(string row)
        {
            int at = places.FindIndex(p => row == "frame" ? p.frame : row == "reactor" ? p.reactor : p.Id == row);
            if (at < 0) return false;
            ClickSlot(at);
            return true;
        }

        bool Pick(Item wanted)
        {
            if (wanted == null) return false;
            int at = kinds.IndexOf(wanted.def);
            if (at < 0) return false;
            kind = at;
            List(wanted.def);
            int index = items.IndexOf(wanted);
            if (index < 0) return false;
            column = Items;
            FitItem(index);
            return true;
        }

        public void SaveChanges() => Save();

        // ---- What is shown ----

        static bool Over(Loadout loadout) => loadout.PowerUsed > loadout.reactor.powerCap;

        // Whether the machine on screen differs from the one that is saved.
        bool Edited()
        {
            var stored = Profile.Presets[machine];
            if (draft.reactor != stored.reactor || draft.drive != stored.drive || draft.frame != stored.frame) return true;
            foreach (var slot in draft.frame.slots)
                if (draft.In(slot.id) != stored.In(slot.id) || draft.ItemIn(slot.id) != stored.ItemIn(slot.id)) return true;
            return false;
        }

        static object Fitted(Loadout draft, Place at)
        {
            if (at.frame) return draft.frame;
            if (at.reactor) return draft.reactor;
            if (at.slot.type == SlotType.Drive) return draft.drive;
            return draft.ItemIn(at.slot.id);
        }

        // The slot of this machine an item sits in, or null.
        static string SlotOf(Loadout draft, Item item)
        {
            foreach (var entry in draft.modules)
                if (entry.item == item) return entry.slot;
            return null;
        }

        static string Titled(Item item) => item.level > 1 ? $"{item.Name}  Lv {item.level}" : item.Name;

        // What sets a part apart from the others of its kind.
        static string Variant(Item item) => $"{(item.ability != null ? item.ability.displayName : "Plain")}    Lv {item.level}";

        // The player's parts of one kind, the best first.
        void List(ModuleDef module)
        {
            items.Clear();
            if (module == null) return;
            foreach (var one in Profile.Items)
                if (one.def == module) items.Add(one);
            items.Sort((a, b) => Worth(b).CompareTo(Worth(a)));
        }

        // Which of two parts of one kind comes first in the list.
        static int Worth(Item item) => item.level * 10 + (int)item.rarity;

        // Redraws everything. Only texts and colours change; no element is added or removed except
        // the small chips inside their fixed strip. newPlace: another row of the machine is
        // highlighted, so the middle column starts again at what sits in it. newKind: another kind
        // is highlighted, so the right column starts again.
        void Refresh(bool newPlace = false, bool newKind = false)
        {
            bool over = Over(draft), edited = Edited();
            machineName.text = draft.displayName.ToUpperInvariant();
            unsaved.style.display = edited ? DisplayStyle.Flex : DisplayStyle.None;
            schematic.Loadout = draft;

            int usedPower = draft.PowerUsed, cap = draft.reactor.powerCap;
            powerValue.text = $"{usedPower} / {cap}";
            powerValue.EnableInClassList("over", over);
            for (int i = 0; i < pips.Count; i++)
            {
                pips[i].style.visibility = i < Mathf.Max(cap, usedPower) ? Visibility.Visible : Visibility.Hidden;
                pips[i].EnableInClassList("pip--on", i < usedPower);
                pips[i].EnableInClassList("pip--over", over && i >= cap);
            }

            // The left column: the frame, one row for each of its slots, then the reactor.
            places.Clear();
            places.Add(new Place { frame = true });
            foreach (var slot in draft.frame.slots) places.Add(new Place { slot = slot });
            places.Add(new Place { reactor = true });
            place = Mathf.Clamp(place, 0, places.Count - 1);
            var at = places[place];
            schematic.Selected = at.Id;
            for (int i = 0; i < slotRows.Count; i++)
            {
                var row = slotRows[i];
                bool used = i < places.Count;
                row.root.style.visibility = used ? Visibility.Visible : Visibility.Hidden;
                if (!used) continue;
                var here = places[i];
                object fitted = Fitted(draft, here);
                row.slot.text = here.frame ? "FRAME" : here.reactor ? "REACTOR" : Parts.SlotName(draft.frame, here.slot);
                row.name.text = fitted is Item sitting ? Titled(sitting) : fitted is UnityEngine.Object thing ? Parts.Name(thing) : "Empty";
                row.name.EnableInClassList("slot-row__name--empty", fitted == null);
                row.root.EnableInClassList("slot-row--on", i == place);
                row.root.EnableInClassList("slot-row--focus", i == place && column == Slots);
            }

            // The middle column: what fits there. A slot for a module can also be left empty, and
            // of modules only the kinds the player has are listed, in the order of the catalogue.
            kinds.Clear();
            if (at.frame) { foreach (var frame in config.frames) if (Profile.IsFound(frame)) kinds.Add(frame); }
            else if (at.reactor) { foreach (var reactor in config.reactors) if (Profile.IsFound(reactor)) kinds.Add(reactor); }
            else if (at.slot.type == SlotType.Drive) { foreach (var drive in config.drives) if (Profile.IsFound(drive)) kinds.Add(drive); }
            else
            {
                kinds.Add(null);
                foreach (var module in config.modules)
                    if (module != null && Parts.Fits(module, at.slot.type) && Profile.Items.Exists(one => one.def == module)) kinds.Add(module);
            }
            object inPlace = Fitted(draft, at);
            var kindInPlace = inPlace is Item fittedItem ? fittedItem.def : inPlace as UnityEngine.Object;
            if (newPlace) kind = Mathf.Max(0, kinds.IndexOf(kindInPlace));
            kind = Mathf.Clamp(kind, 0, kinds.Count - 1);
            // A list moves only as far as it must to keep the highlighted row on screen.
            firstKind = Mathf.Clamp(firstKind, Mathf.Max(0, kind - Rows + 1), kind);
            firstKind = Mathf.Clamp(firstKind, 0, Mathf.Max(0, kinds.Count - Rows));

            // The right column: the player's parts of that kind.
            var shown = kinds[kind];
            List(shown as ModuleDef);
            if (newPlace || newKind) item = Mathf.Max(0, items.IndexOf(inPlace as Item));
            item = Mathf.Clamp(item, 0, Mathf.Max(0, items.Count - 1));
            if (items.Count == 0 && column == Items) column = Kinds;
            firstItem = Mathf.Clamp(firstItem, Mathf.Max(0, item - Rows + 1), item);
            firstItem = Mathf.Clamp(firstItem, 0, Mathf.Max(0, items.Count - Rows));

            kindsLabel.text = at.frame ? "FRAME" : at.reactor ? "REACTOR" : Parts.SlotName(draft.frame, at.slot);
            for (int i = 0; i < kindRows.Count; i++)
            {
                var row = kindRows[i];
                int index = firstKind + i;
                bool used = index < kinds.Count;
                row.root.style.visibility = used ? Visibility.Visible : Visibility.Hidden;
                row.notches.Rarity = Rarity.None;
                row.notches.style.display = DisplayStyle.None;
                if (!used) continue;
                var one = kinds[index];
                bool sits = one != null ? one == kindInPlace : inPlace == null, focused = index == kind;
                row.root.Border = !focused ? (sits ? Tokens.Chalk : Tokens.Seam) : column == Kinds ? Tokens.PulseGreen : Tokens.Chalk;
                row.root.Fill = sits || focused ? Tokens.Plate2 : Tokens.Plate;
                row.root.Raised = sits || focused;
                row.cost.EnableInClassList("part__cost--on", sits && one != null);
                int owned = 0;
                if (one is ModuleDef module)
                    foreach (var have in Profile.Items)
                        if (have.def == module) owned++;
                row.count.text = owned.ToString();
                row.count.style.display = owned > 1 ? DisplayStyle.Flex : DisplayStyle.None;
                row.name.text = one != null ? Parts.Name(one) : "Empty";
                row.text.text = one is FrameDef layout ? Parts.Layout(layout) : one != null ? Parts.Description(one) : "";
                row.cost.text = one != null && sits ? "FITTED" : one is ModuleDef costs ? costs.powerCost + " Power"
                    : one is ReactorDef gives ? "gives " + gives.powerCap : "";
                if (one is ScriptableObject thing) PartUi.Chips(row.chips, thing);
                else row.chips.Clear();
            }

            itemsLabel.text = shown != null ? Parts.Name(shown).ToUpperInvariant() : "";
            itemsCount.text = items.Count > 1 ? items.Count.ToString() : "";
            // With one part of a kind, or with something that is not a part, there is nothing to
            // choose on the right: it says what the thing is.
            bool choice = items.Count > 1;
            info.style.display = choice ? DisplayStyle.None : DisplayStyle.Flex;
            var only = items.Count == 1 ? items[0] : null;
            infoTitle.text = only != null ? Variant(only).ToUpperInvariant() : "";
            infoText.text = only != null ? (only.ability != null ? $"{only.ability.displayName}: {only.ability.Describe(only.rarity)}" : only.def.description)
                : shown is FrameDef frameShown ? Parts.Layout(frameShown) + ". " + Parts.Description(shown)
                : shown != null ? Parts.Description(shown) : "";
            for (int i = 0; i < itemRows.Count; i++)
            {
                var row = itemRows[i];
                int index = firstItem + i;
                bool used = choice && index < items.Count;
                row.root.style.display = choice ? DisplayStyle.Flex : DisplayStyle.None;
                row.root.style.visibility = used ? Visibility.Visible : Visibility.Hidden;
                row.count.style.display = DisplayStyle.None;
                row.chips.Clear();
                if (!used)
                {
                    row.notches.Rarity = Rarity.None;
                    continue;
                }
                var one = items[index];
                bool sits = ReferenceEquals(one, inPlace), focused = index == item && column == Items;
                row.root.Border = focused ? Tokens.PulseGreen : sits ? Tokens.Chalk : Tokens.Seam;
                row.root.Fill = sits || focused ? Tokens.Plate2 : Tokens.Plate;
                row.root.Raised = sits || focused;
                row.cost.EnableInClassList("part__cost--on", sits);
                // They are all the same kind of part, so a row is named after what sets it apart.
                row.name.text = Variant(one);
                row.notches.Rarity = one.rarity;
                row.text.text = one.ability != null ? one.ability.Describe(one.rarity) : one.def.description;
                row.cost.text = sits ? "FITTED" : SlotOf(draft, one) != null ? "ON THE MACHINE" : "";
            }

            save.Usable = edited && !over;
            save.Label.text = over ? "TOO MUCH POWER" : edited ? "SAVE" : "SAVED";
        }
    }
}
