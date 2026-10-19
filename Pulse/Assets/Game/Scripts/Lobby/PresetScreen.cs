using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pulse
{
    // The screen of the display that stands at the turntable: which of the player's machines is on
    // it. The machines are shown side by side, each with its wiring diagram and what it carries.
    // One of them is taken, which puts it on the turntable; a free place takes a copy of the
    // highlighted machine; a machine that is not wanted is deleted. What a machine is built from
    // is changed at the Mechanic, not here.
    public sealed class PresetScreen
    {
        sealed class Card
        {
            public Plate root;
            public VisualElement content;
            public SchematicView schematic;
            public Label name, active, frame, power, empty;
            public readonly List<Label> slots = new List<Label>(), parts = new List<Label>();
        }

        // How many parts of a machine are listed under its diagram.
        const int Lines = 8;

        readonly Action changed;
        readonly VisualElement overlay;
        readonly List<Card> cards = new List<Card>();
        readonly PlateButton take, remove, back;
        int focus;

        public bool IsOpen { get; private set; }

        public PresetScreen(VisualElement root, Action changed)
        {
            this.changed = changed;

            overlay = Ui.Add(root, new VisualElement(), "overlay");
            var panel = Ui.Add(overlay, new Plate(), "station");
            var header = Ui.Add(panel, new VisualElement(), "station__header");
            Ui.Add(header, new Label("MACHINES"), "display");

            var body = Ui.Add(panel, new VisualElement(), "station__body");
            for (int i = 0; i < Profile.MaxPresets; i++)
            {
                int index = i;
                var card = new Card { root = Ui.Add(body, new Plate(), "preset") };
                card.content = Ui.Add(card.root, new VisualElement(), "preset__content");
                var line = Ui.Add(card.content, new VisualElement(), "card__line");
                card.name = Ui.Add(line, new Label(), "title");
                card.name.AddToClassList("card__name");
                card.name.AddToClassList("clip");
                card.active = Ui.Add(line, new Label("ACTIVE"), "tag");
                var under = Ui.Add(card.content, new VisualElement(), "preset__under");
                card.frame = Ui.Add(under, new Label(), "small");
                card.power = Ui.Add(under, new Label(), "small");
                card.schematic = Ui.Add(card.content, new SchematicView { SlotSize = 38f }, "preset__schematic");
                for (int k = 0; k < Lines; k++)
                {
                    var row = Ui.Add(card.content, new VisualElement(), "preset__line");
                    card.slots.Add(Ui.Add(row, new Label(), "label"));
                    var part = Ui.Add(row, new Label(), "num-small");
                    part.AddToClassList("clip");
                    card.parts.Add(part);
                }
                // The place for a machine that has not been made yet.
                card.empty = Ui.Add(card.root, new Label("+ NEW"), "preset__empty");
                card.root.AddManipulator(new Clickable(() => Click(index)));
                cards.Add(card);
            }

            var footer = Ui.Add(panel, new VisualElement(), "station__footer");
            var leave = Ui.Add(footer, new VisualElement(), "station__buttons");
            back = Ui.Add(leave, new PlateButton("Back", Close));
            remove = Ui.Add(leave, new PlateButton("Delete", () => Delete()));
            // Its label changes with what it will do, so its width is fixed.
            take = Ui.Add(Ui.Add(footer, new VisualElement(), "station__buttons"), new PlateButton("Take", () => Take(), primary: true), "station__take");
        }

        public void Open()
        {
            IsOpen = true;
            overlay.style.display = DisplayStyle.Flex;
            focus = Profile.Current;
            Refresh();
        }

        public void Close()
        {
            IsOpen = false;
            overlay.style.display = DisplayStyle.None;
        }

        // Called every frame while the screen is open.
        public void Update()
        {
            var nav = Nav.Read();
            int places = Mathf.Min(Profile.Presets.Count + 1, Profile.MaxPresets);
            if (nav.back) back.Press();
            else if (nav.remove) remove.Press();
            else if (nav.confirm || nav.take) take.Press();
            else if (nav.left || nav.right)
            {
                focus = (focus + (nav.right ? 1 : places - 1)) % places;
                Refresh();
            }
        }

        void Click(int index)
        {
            if (index > Profile.Presets.Count) return;
            // A click on the free place makes the new machine at once.
            if (index == Profile.Presets.Count) { focus = index; New(); return; }
            focus = index;
            Refresh();
        }

        // ---- What the player can do; also what a test calls ----

        // Highlights a machine, or the free place after the last one.
        public void Show(int index)
        {
            focus = Mathf.Clamp(index, 0, Mathf.Min(Profile.Presets.Count, Profile.MaxPresets - 1));
            Refresh();
        }

        // Puts the highlighted machine on the turntable. On the free place it makes a new machine first.
        public bool Take()
        {
            if (focus >= Profile.Presets.Count && !New()) return false;
            Profile.Take(focus);
            changed();
            Close();
            return true;
        }

        // A new machine starts as a copy of the one on the turntable.
        public bool New()
        {
            var added = Profile.AddPreset(Profile.Machine);
            if (added == null) return false;
            focus = Profile.Presets.Count - 1;
            changed();
            Refresh();
            return true;
        }

        // The last machine cannot be deleted.
        public bool Delete()
        {
            if (focus >= Profile.Presets.Count || !Profile.RemovePreset(focus)) return false;
            focus = Mathf.Min(focus, Profile.Presets.Count - 1);
            changed();
            Refresh();
            return true;
        }

        // ---- What is shown ----

        void Refresh()
        {
            int count = Profile.Presets.Count;
            focus = Mathf.Clamp(focus, 0, Mathf.Min(count, Profile.MaxPresets - 1));
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                bool used = i < count, free = i == count, focused = i == focus;
                card.root.style.visibility = used || free ? Visibility.Visible : Visibility.Hidden;
                card.root.Border = focused ? Tokens.PulseGreen : Tokens.Seam;
                card.root.Fill = focused ? Tokens.Plate2 : Tokens.Plate;
                card.root.Raised = focused;
                card.content.style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
                card.empty.style.display = used ? DisplayStyle.None : DisplayStyle.Flex;
                if (!used) continue;

                var machine = Profile.Presets[i];
                card.name.text = machine.displayName.ToUpperInvariant();
                card.active.style.display = i == Profile.Current ? DisplayStyle.Flex : DisplayStyle.None;
                card.frame.text = $"{machine.frame.displayName}    {Parts.MarkName(Profile.MarkOf(machine.frame))}";
                card.power.text = $"Power {machine.PowerUsed} / {machine.reactor.powerCap}";
                card.schematic.Loadout = machine;
                // What sits in its slots, slot by slot. An empty slot is left out.
                int line = 0;
                foreach (var slot in machine.frame.slots)
                {
                    if (line >= Lines) break;
                    var item = machine.ItemIn(slot.id);
                    string what = slot.type == SlotType.Drive ? machine.drive.displayName
                        : item == null ? null : item.level > 1 ? $"{item.Name}  Lv {item.level}" : item.Name;
                    if (what == null) continue;
                    card.slots[line].text = Parts.SlotName(machine.frame, slot);
                    card.parts[line].text = what;
                    card.slots[line].parent.style.visibility = Visibility.Visible;
                    line++;
                }
                for (; line < Lines; line++) card.slots[line].parent.style.visibility = Visibility.Hidden;
            }

            bool onMachine = focus < count;
            take.Usable = !onMachine || focus != Profile.Current;
            take.Label.text = !onMachine ? "NEW MACHINE" : focus == Profile.Current ? "ON THE TURNTABLE" : "TAKE";
            remove.Usable = onMachine && count > 1;
        }
    }
}
