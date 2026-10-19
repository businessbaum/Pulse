using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pulse
{
    // The Smelter's screen: frames. A frame is what a machine is built on, and it alone decides which
    // slots there are and how they are wired. Here the player sees every frame, buys the ones not yet
    // owned and makes owned ones tougher, Mark by Mark, all for Beats. A Mark never changes the slots;
    // only another frame does.
    //
    // The frames are on the left, the slots of the highlighted one in the middle, and its Marks on
    // the right. The one button does the next thing there is to do for that frame.
    public sealed class SmelterScreen
    {
        sealed class Card
        {
            public Plate root;
            public Hatch hatch;
            public Label name, note, owned;
        }

        sealed class Step
        {
            public Plate root;
            public Label name, gives, cost;
        }

        const int Cards = 6, Steps = 4;

        readonly GameConfig config;
        readonly Action changed;
        readonly VisualElement overlay;
        readonly List<Card> cards = new List<Card>();
        readonly List<Step> steps = new List<Step>();
        readonly SchematicView schematic;
        readonly Loadout bare;
        readonly Label beats, frameName, layout, text;
        readonly PlateButton act, back;
        int focus, shownBeats = -1;

        public bool IsOpen { get; private set; }

        public SmelterScreen(VisualElement root, GameConfig config, Action changed)
        {
            this.config = config;
            this.changed = changed;
            // The diagram shows a frame with nothing on it.
            bare = ScriptableObject.CreateInstance<Loadout>();

            overlay = Ui.Add(root, new VisualElement(), "overlay");
            var panel = Ui.Add(overlay, new Plate(), "station");
            var header = Ui.Add(panel, new VisualElement(), "station__header");
            Ui.Add(header, new Label("SMELTER"), "display");
            var purse = Ui.Add(header, new VisualElement(), "station__purse");
            Ui.Add(purse, new Label("BEATS"), "label");
            beats = Ui.Add(purse, new Label(), "num");

            var body = Ui.Add(panel, new VisualElement(), "station__body");
            var list = Ui.Add(body, new VisualElement(), "station__list");
            for (int i = 0; i < Cards; i++)
            {
                int index = i;
                var card = new Card { root = Ui.Add(list, new Plate(), "card") };
                card.hatch = Ui.Add(card.root, new Hatch());
                var line = Ui.Add(card.root, new VisualElement(), "card__line");
                card.name = Ui.Add(line, new Label(), "title");
                card.name.AddToClassList("card__name");
                card.name.AddToClassList("clip");
                card.owned = Ui.Add(line, new Label(), "tag");
                card.note = Ui.Add(card.root, new Label(), "small");
                card.root.AddManipulator(new Clickable(() => { if (index < config.frames.Length) { focus = index; Refresh(); } }));
                cards.Add(card);
            }

            var middle = Ui.Add(body, new VisualElement(), "station__middle");
            var names = Ui.Add(middle, new VisualElement(), "smelter__names");
            frameName = Ui.Add(names, new Label(), "title");
            layout = Ui.Add(names, new Label(), "small");
            schematic = Ui.Add(middle, new SchematicView { SlotSize = 40f }, "smelter__schematic");
            text = Ui.Add(middle, new Label(), "archive__text");

            // The Marks of the frame, one under the other: what each gives and what it costs.
            var right = Ui.Add(body, new VisualElement(), "station__parts");
            Ui.Add(Ui.Add(right, new VisualElement(), "station__fits"), new Label("MARKS"), "label");
            for (int i = 0; i < Steps; i++)
            {
                var step = new Step { root = Ui.Add(right, new Plate { Cut = 6f }, "step") };
                step.name = Ui.Add(step.root, new Label(), "title");
                step.name.AddToClassList("step__name");
                step.gives = Ui.Add(step.root, new Label(), "num-small");
                step.gives.AddToClassList("step__gives");
                step.cost = Ui.Add(step.root, new Label(), "num-small");
                step.cost.AddToClassList("step__cost");
                steps.Add(step);
            }

            var footer = Ui.Add(panel, new VisualElement(), "station__footer");
            back = Ui.Add(footer, new PlateButton("Back", Close));
            act = Ui.Add(footer, new PlateButton("Buy", () => Act(), primary: true), "station__take");
        }

        public void Open()
        {
            IsOpen = true;
            overlay.style.display = DisplayStyle.Flex;
            // Start at the frame of the machine on the turntable.
            focus = Mathf.Max(0, Array.IndexOf(config.frames, Profile.Machine.frame));
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
            // The count throbs like every piece of the Pulse.
            float throb = 1f + 0.1f * Heart.Beat(Time.time);
            beats.style.scale = new Scale(new Vector3(throb, throb, 1f));

            var nav = Nav.Read();
            int count = config.frames.Length;
            if (nav.back) back.Press();
            else if (nav.confirm) act.Press();
            else if (nav.up) { focus = (focus + count - 1) % count; Refresh(); }
            else if (nav.down) { focus = (focus + 1) % count; Refresh(); }
        }

        // Highlights one frame, as a click would. Also what a test calls.
        public void Show(FrameDef frame)
        {
            focus = Mathf.Max(0, Array.IndexOf(config.frames, frame));
            Refresh();
        }

        // Does the next thing there is to do for the highlighted frame: buys it, or raises its Mark.
        // Returns false when there is nothing to do or not enough Beats.
        public bool Act()
        {
            var frame = config.frames[focus];
            bool done = Profile.IsFound(frame) ? Profile.Upgrade(frame) : Profile.Buy(frame);
            if (done) changed();
            Refresh();
            return done;
        }

        void Refresh()
        {
            if (Profile.Beats != shownBeats) beats.text = (shownBeats = Profile.Beats).ToString();
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                bool used = i < config.frames.Length;
                card.root.style.visibility = used ? Visibility.Visible : Visibility.Hidden;
                if (!used) continue;
                var one = config.frames[i];
                bool have = Profile.IsFound(one);
                card.root.Border = i == focus ? Tokens.PulseGreen : Tokens.Seam;
                card.root.Fill = i == focus && have ? Tokens.Plate2 : Tokens.Plate;
                card.root.Raised = i == focus && have;
                card.root.Sunk = !have;
                card.hatch.style.display = have ? DisplayStyle.None : DisplayStyle.Flex;
                card.name.text = one.displayName.ToUpperInvariant();
                card.note.text = Parts.Layout(one);
                card.owned.text = have ? Parts.MarkName(Profile.MarkOf(one)).ToUpperInvariant() : one.price.ToString();
                card.owned.EnableInClassList("tag--dim", !have);
            }

            var frame = config.frames[focus];
            bool owned = Profile.IsFound(frame);
            int mark = Profile.MarkOf(frame), cost = Profile.NextCost(frame);
            frameName.text = frame.displayName.ToUpperInvariant();
            layout.text = Parts.Layout(frame);
            text.text = frame.description;
            bare.frame = frame;
            schematic.Loadout = bare;

            // One step for the frame itself and one for each further Mark.
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                bool used = i <= frame.marks.Length;
                step.root.style.visibility = used ? Visibility.Visible : Visibility.Hidden;
                if (!used) continue;
                bool done = owned && i <= mark, next = owned ? i == mark + 1 : i == 0;
                step.root.Border = next ? Tokens.PulseGreen : done ? Tokens.Chalk : Tokens.Seam;
                step.root.Fill = done || next ? Tokens.Plate2 : Tokens.Plate;
                step.root.Raised = next;
                step.name.text = Parts.MarkName(i).ToUpperInvariant();
                float armor = frame.ArmorAt(i);
                step.gives.text = armor > 0f ? $"Hull {frame.HullAt(i):0}    Armor {armor:0}" : $"Hull {frame.HullAt(i):0}";
                int price = i == 0 ? frame.price : frame.marks[i - 1].cost;
                step.cost.text = done ? "YOURS" : price.ToString();
                step.cost.EnableInClassList("step__cost--done", done);
                step.cost.EnableInClassList("over", next && price > Profile.Beats);
            }

            act.Usable = cost > 0 && cost <= Profile.Beats;
            act.Label.text = cost <= 0 ? "FULLY UPGRADED"
                : cost > Profile.Beats ? $"{cost - Profile.Beats} BEATS SHORT"
                : owned ? $"UPGRADE    {cost}" : $"BUY    {cost}";
        }
    }
}
