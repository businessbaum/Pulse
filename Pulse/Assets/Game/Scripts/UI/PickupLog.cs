using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pulse
{
    // The log of what the machine brought in, on the right edge between the top and the middle.
    // A new line comes in from the right, on top of the others, stays for a few seconds and fades.
    // Beats that come in while their line is still there are added to it, so a trail of them is
    // one line that counts up. Hearts and other power-ups are not listed; see Loot.
    public sealed class PickupLog
    {
        sealed class Line
        {
            public Plate root;
            public PartIcon icon;
            public VisualElement gem;
            public Label name, detail;
            public Notches notches;
            public float age, life, y, bump;
            public int beats;
        }

        const int Lines = 5;
        // Stays: seconds a line is there. Appear and Fade: how long it takes to come and to go.
        // Step: from one line to the next, in pixels. SlideIn: how far to the right a line starts.
        const float Stays = 4.5f, Appear = 0.18f, Fade = 0.45f, Step = 51f, SlideIn = 90f, Settle = 16f, BumpTime = 0.22f;

        readonly Stack<Line> spare = new Stack<Line>();
        // Newest first.
        readonly List<Line> shown = new List<Line>();

        public PickupLog(VisualElement parent)
        {
            var box = Ui.Add(parent, new VisualElement(), "hud-log");
            box.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < Lines; i++)
            {
                var line = new Line { root = Ui.Add(box, new Plate { Cut = 6f }, "log-line") };
                line.root.pickingMode = PickingMode.Ignore;
                line.icon = Ui.Add(line.root, new PartIcon(), "log-line__icon");
                line.gem = Ui.Add(line.root, new VisualElement(), "log-line__gem");
                line.name = Ui.Add(line.root, new Label(), "log-line__name");
                line.notches = Ui.Add(line.root, new Notches());
                line.detail = Ui.Add(line.root, new Label(), "log-line__detail");
                line.root.style.display = DisplayStyle.None;
                spare.Push(line);
            }
        }

        // How many lines are there, and what one says. Line 0 is the newest.
        public int Count => shown.Count;

        public string Text(int index) => (shown[index].name.text + " " + shown[index].detail.text).Trim();

        // Called every frame.
        public void Tick(float dt)
        {
            while (Loot.Waiting.Count > 0) Add(Loot.Waiting.Dequeue());

            for (int i = shown.Count - 1; i >= 0; i--)
            {
                var line = shown[i];
                line.age += dt;
                if (line.age < line.life) continue;
                line.root.style.display = DisplayStyle.None;
                shown.RemoveAt(i);
                spare.Push(line);
            }
            for (int i = 0; i < shown.Count; i++)
            {
                var line = shown[i];
                float arrived = Ui.EaseOut(line.age / Appear);
                // A line moves down when a newer one comes in over it, and back up when one above it goes.
                line.y = Mathf.Lerp(line.y, i * Step, 1f - Mathf.Exp(-Settle * dt));
                line.root.style.translate = new Translate(SlideIn * (1f - arrived), line.y);
                line.root.style.opacity = Mathf.Min(arrived, Mathf.Clamp01((line.life - line.age) / Fade));
                // More Beats on a line that is already there: it swells for a moment.
                line.bump = Mathf.Max(0f, line.bump - dt);
                float swell = 1f + 0.1f * Mathf.Sin(line.bump / BumpTime * Mathf.PI);
                line.root.style.scale = new Scale(new Vector3(swell, swell, 1f));
            }
        }

        void Add(Loot.Entry entry)
        {
            if (entry.part == null)
            {
                // Beats join the line they already have, unless it is on its way out.
                foreach (var one in shown)
                {
                    if (one.beats <= 0 || one.life - one.age < Fade) continue;
                    one.beats += entry.beats;
                    one.name.text = "+" + one.beats;
                    one.life = one.age + Stays;
                    one.bump = BumpTime;
                    return;
                }
            }

            // With every line in use, the oldest makes room.
            if (spare.Count == 0)
            {
                var oldest = shown[shown.Count - 1];
                shown.RemoveAt(shown.Count - 1);
                spare.Push(oldest);
            }
            var line = spare.Pop();
            line.age = line.bump = 0f;
            line.life = Stays;
            line.y = 0f;
            line.beats = entry.part == null ? entry.beats : 0;
            bool part = entry.part != null;
            var rarity = entry.item != null ? entry.item.rarity : Rarity.None;
            line.icon.style.display = part ? DisplayStyle.Flex : DisplayStyle.None;
            line.gem.style.display = part ? DisplayStyle.None : DisplayStyle.Flex;
            line.notches.Rarity = rarity;
            line.notches.style.display = rarity != Rarity.None ? DisplayStyle.Flex : DisplayStyle.None;
            if (part)
            {
                line.icon.Show(entry.part, true);
                line.name.text = Parts.Name(entry.part).ToUpperInvariant();
                line.detail.text = entry.item != null && entry.item.ability != null ? entry.item.ability.displayName.ToUpperInvariant() : "";
            }
            else
            {
                line.name.text = "+" + entry.beats;
                line.detail.text = "BEATS";
            }
            line.detail.style.display = line.detail.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            line.root.style.display = DisplayStyle.Flex;
            line.root.style.opacity = 0f;
            line.root.style.translate = new Translate(SlideIn, 0f);
            line.root.BringToFront();
            shown.Insert(0, line);
        }
    }
}
