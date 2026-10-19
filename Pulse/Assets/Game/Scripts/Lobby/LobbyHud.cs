using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pulse
{
    // The lobby's interface: which machine is on the turntable, how many Beats the player has, a
    // floating sign over everything the player can use, a small prompt for whatever is nearby, and
    // the bar that fills while standing on the departure pad. It says as little as it can.
    public sealed class LobbyHud
    {
        sealed class Sign
        {
            public Plate plate;
            public Label label;
            public Vector3 pos;
            public float phase;
        }

        const float HoverHeight = 0.035f, HoverSpeed = 1.1f;

        readonly Label machineName, promptName, promptKey, promptPrice, beats;
        readonly VisualElement prompt, signLayer, top, purse;
        readonly SaveMark saved;
        readonly Plate keycap;
        readonly TimerBar timer;
        readonly List<Sign> signs = new List<Sign>();
        // The number on screen runs after the real one, so Beats that come home are seen arriving.
        float counted = -1f;
        int shownBeats = -1;

        // The screens of the lobby's machines are added here, so they share the fonts of the rest.
        public VisualElement Screen { get; }
        public PauseMenu Pause { get; }

        public LobbyHud(VisualElement root)
        {
            root.Clear();
            Screen = Ui.Add(root, new VisualElement(), "screen");
            // Signs sit behind the rest of the interface.
            signLayer = Ui.Add(Screen, new VisualElement(), "sign-layer");

            top = Ui.Add(Screen, new VisualElement(), "hud-top-left");
            var title = Ui.Add(top, new VisualElement());
            Ui.Add(title, new Label("THE ROUNDHOUSE"), "label");
            machineName = Ui.Add(title, new Label(), "title");

            // What the player has brought home. It throbs like every piece of the Pulse.
            purse = Ui.Add(Screen, new VisualElement(), "hud-top-right");
            Ui.Add(purse, new Label("BEATS"), "label");
            beats = Ui.Add(purse, new Label("0"), "num");

            // The prompt: the key to press, large and green, and the name of what it opens. It comes
            // up from below when the character steps next to something and drops away again.
            prompt = Ui.Add(Ui.Add(Screen, new VisualElement(), "hud-bottom-center"), new Plate { Cut = 8f }, "prompt");
            keycap = Ui.Add(prompt, new Plate { Cut = 5f, Fill = Tokens.PulseGreen, Border = Tokens.PulseGreen }, "keycap");
            promptKey = Ui.Add(keycap, new Label(), "keycap__label");
            promptName = Ui.Add(prompt, new Label(), "prompt__name");
            // For a door that has to be bought: what it costs.
            promptPrice = Ui.Add(prompt, new Label(), "prompt__price");
            saved = Ui.Add(Screen, new SaveMark());

            timer = Ui.Add(Ui.Add(Screen, new VisualElement(), "hud-subtitle"), new TimerBar());

            Pause = new PauseMenu(Screen,
                new (string, Action)[] { ("Quit game", PauseMenu.Quit) },
                new[]
                {
                    ("W A S D / stick", "Walk"),
                    ("E / A", "Use what you stand next to"),
                    ("Bay doors", "Lead to the rooms of the machines"),
                    ("Display at the turntable", "Choose which machine you take"),
                    ("Green half circle at the gate", "Leave for the open world"),
                    ("Esc / Start", "This menu, and back out of any screen"),
                });
        }

        public void ShowMachine(Loadout loadout) => machineName.text = loadout.displayName.ToUpperInvariant();

        // The number starts from here and runs up to what the player has.
        public void CountFrom(int value) => counted = value;

        // Called every frame: the number and its throb, and the key that pulses a little.
        public void Tick(int count)
        {
            if (counted < 0f) counted = count;
            counted = Mathf.MoveTowards(counted, count, Mathf.Max(40f, Mathf.Abs(count - counted) * 2.2f) * Time.unscaledDeltaTime);
            int rounded = Mathf.RoundToInt(counted);
            if (rounded != shownBeats) beats.text = (shownBeats = rounded).ToString();
            float throb = 1f + 0.12f * Heart.Beat(Time.time);
            beats.style.scale = new Scale(new Vector3(throb, throb, 1f));
            float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 6f);
            keycap.style.scale = new Scale(new Vector3(pulse, pulse, 1f));
            saved.Tick();
        }

        // A sign that hovers over something the player can use.
        public void AddSign(string title, Vector3 pos)
        {
            var plate = Ui.Add(signLayer, new Plate { Cut = 6f }, "sign");
            var label = Ui.Add(plate, new Label(title.ToUpperInvariant()), "sign__label");
            signs.Add(new Sign { plate = plate, label = label, pos = pos, phase = signs.Count * 1.3f });
        }

        // Changes what a sign says. lit: in Pulse green, for something that wants to be looked at.
        // shown: a sign over something that is not there, or not there any more, is hidden.
        public void NameSign(int index, string title, bool lit, bool shown = true)
        {
            if (index < 0 || index >= signs.Count) return;
            signs[index].label.text = title.ToUpperInvariant();
            signs[index].plate.Border = lit ? Tokens.PulseGreen : Tokens.Seam;
            signs[index].plate.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Keeps every sign above its object as the camera moves. It drifts only slightly.
        public void MoveSigns(Camera camera)
        {
            if (signLayer.panel == null) return;
            foreach (var sign in signs)
            {
                Vector3 world = sign.pos + Vector3.up * (Mathf.Sin(Time.time * HoverSpeed + sign.phase) * HoverHeight);
                Vector2 at = RuntimePanelUtils.CameraTransformWorldToPanel(signLayer.panel, world, camera);
                sign.plate.style.left = at.x - sign.plate.resolvedStyle.width * 0.5f;
                sign.plate.style.top = at.y - sign.plate.resolvedStyle.height;
            }
        }

        // The prompt for something usable: its name and the key that opens it. Pass a null name to
        // let it drop away. The words stay while it drops, so it does not empty before it is gone.
        // price: what using it costs, or zero. usable: whether the player has that much; if not,
        // the key is grey and the price is red.
        public void ShowPrompt(string name, string key, int price = 0, bool usable = true)
        {
            prompt.EnableInClassList("prompt--shown", name != null);
            if (name == null) return;
            promptName.text = name.ToUpperInvariant();
            promptKey.text = key;
            promptPrice.text = price > 0 ? price.ToString() : "";
            promptPrice.style.display = price > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            promptPrice.EnableInClassList("over", !usable);
            keycap.Fill = keycap.Border = usable ? Tokens.PulseGreen : Tokens.Seam;
        }

        // Pass a null text to hide the bar.
        public void ShowGate(string text, float progress)
        {
            if (text == null) timer.Hide();
            else timer.Show(text, progress);
        }

        // Hides everything but the picture, for the scenes in which the machine leaves or arrives.
        public void HideForScene()
        {
            signLayer.style.display = DisplayStyle.None;
            top.style.display = DisplayStyle.None;
            purse.style.display = DisplayStyle.None;
            ShowPrompt(null, null);
            timer.Hide();
        }

        public void ShowAfterScene()
        {
            signLayer.style.display = DisplayStyle.Flex;
            top.style.display = DisplayStyle.Flex;
            purse.style.display = DisplayStyle.Flex;
        }
    }
}
