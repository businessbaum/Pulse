using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Pulse
{
    // The pause menu, shared by the run and the lobby. Opening it stops time; Resume is always the
    // first choice. Below the choices it lists the controls of the place it was opened in. "Options"
    // swaps the menu for the options, in the same place; going back from there returns to the menu.
    public sealed class PauseMenu
    {
        // One line of the options: a setting with its choices, stepped through with two small buttons.
        sealed class OptionRow
        {
            public Plate root;
            public Label value;
            public Options.Setting setting;
        }

        readonly VisualElement overlay, menu, options;
        readonly Label where;
        readonly List<PlateButton> buttons = new List<PlateButton>();
        readonly List<OptionRow> rows = new List<OptionRow>();
        readonly PlateButton erase, optionsBack;
        int focus, optionFocus;
        bool stickHeld, inOptions, eraseArmed;

        public bool IsOpen { get; private set; }
        // Room under the controls for whatever the place it was opened in wants to add.
        public VisualElement Extra { get; }

        public PauseMenu(VisualElement root, (string label, Action action)[] choices, (string keys, string does)[] controls)
        {
            overlay = Ui.Add(root, new VisualElement(), "overlay");
            menu = Ui.Add(overlay, new Plate(), "pause");
            where = Ui.Add(menu, new Label(), "label");
            Ui.Add(menu, new Label("PAUSED"), "display");

            var list = Ui.Add(menu, new VisualElement(), "pause__choices");
            Choice(list, "Resume", Close, primary: true);
            Choice(list, "Options", () => ShowOptions(true), primary: false);
            foreach (var (label, action) in choices) Choice(list, label, action, primary: false);

            Ui.Add(menu, new Label("CONTROLS"), "label").AddToClassList("pause__heading");
            foreach (var (keys, does) in controls)
            {
                var row = Ui.Add(menu, new VisualElement(), "pause__key");
                Ui.Add(row, new Label(keys), "num-small");
                Ui.Add(row, new Label(does), "small");
            }
            Extra = Ui.Add(menu, new VisualElement());

            // The options: one row for each setting, then the one thing that cannot be undone.
            options = Ui.Add(overlay, new Plate(), "pause");
            Ui.Add(options, new Label("OPTIONS"), "display");
            var settings = Ui.Add(options, new VisualElement(), "pause__choices");
            foreach (var setting in Options.All)
            {
                var row = new OptionRow { setting = setting, root = Ui.Add(settings, new Plate { Cut = 6f }, "option") };
                Ui.Add(row.root, new Label(setting.name.ToUpperInvariant()), "option__name");
                var captured = row;
                Ui.Add(row.root, new PlateButton("<", () => Step(captured, -1)), "button--small");
                row.value = Ui.Add(row.root, new Label(), "option__value");
                Ui.Add(row.root, new PlateButton(">", () => Step(captured, 1)), "button--small");
                rows.Add(row);
            }
            erase = Ui.Add(options, new PlateButton("Erase save", Erase), "button--wide");
            erase.AddToClassList("option__erase");
            optionsBack = Ui.Add(options, new PlateButton("Back", () => ShowOptions(false), primary: true), "button--wide");
            options.style.display = DisplayStyle.None;
        }

        void Choice(VisualElement list, string label, Action action, bool primary)
        {
            var button = Ui.Add(list, new PlateButton(label, action, primary), "button--wide");
            buttons.Add(button);
        }

        // place: where the player is, shown above the title.
        public void Open(string place)
        {
            IsOpen = true;
            where.text = place.ToUpperInvariant();
            overlay.style.display = DisplayStyle.Flex;
            Time.timeScale = 0f;
            ShowOptions(false);
            Focus(0);
        }

        public void Close()
        {
            IsOpen = false;
            overlay.style.display = DisplayStyle.None;
            Time.timeScale = 1f;
        }

        // Public so a test can go there without a key press.
        public void ShowOptions(bool show)
        {
            inOptions = show;
            eraseArmed = false;
            menu.style.display = show ? DisplayStyle.None : DisplayStyle.Flex;
            options.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show) FocusOption(0);
        }

        public bool InOptions => inOptions;

        // Called every frame while the menu is open.
        public void Update()
        {
            var keys = Keyboard.current;
            var pad = Gamepad.current;
            Vector2 stick = pad != null ? pad.leftStick.ReadValue() : Vector2.zero;
            bool stickUp = stick.y > 0.6f && !stickHeld, stickDown = stick.y < -0.6f && !stickHeld;
            bool stickLeft = stick.x < -0.6f && !stickHeld, stickRight = stick.x > 0.6f && !stickHeld;
            stickHeld = stick.magnitude > 0.4f;

            bool up = stickUp || (keys != null && (keys.wKey.wasPressedThisFrame || keys.upArrowKey.wasPressedThisFrame))
                || (pad != null && pad.dpad.up.wasPressedThisFrame);
            bool down = stickDown || (keys != null && (keys.sKey.wasPressedThisFrame || keys.downArrowKey.wasPressedThisFrame))
                || (pad != null && pad.dpad.down.wasPressedThisFrame);
            bool left = stickLeft || (keys != null && (keys.aKey.wasPressedThisFrame || keys.leftArrowKey.wasPressedThisFrame))
                || (pad != null && pad.dpad.left.wasPressedThisFrame);
            bool right = stickRight || (keys != null && (keys.dKey.wasPressedThisFrame || keys.rightArrowKey.wasPressedThisFrame))
                || (pad != null && pad.dpad.right.wasPressedThisFrame);
            bool confirm = (keys != null && (keys.eKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame))
                || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool back = (keys != null && keys.escapeKey.wasPressedThisFrame)
                || (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame));

            if (!inOptions)
            {
                if (back) Close();
                else if (confirm) buttons[focus].Press();
                else if (up) Focus((focus + buttons.Count - 1) % buttons.Count);
                else if (down) Focus((focus + 1) % buttons.Count);
                return;
            }

            // The options: the settings, then Erase, then Back.
            int count = rows.Count + 2;
            if (back) ShowOptions(false);
            else if (up) FocusOption((optionFocus + count - 1) % count);
            else if (down) FocusOption((optionFocus + 1) % count);
            else if (optionFocus < rows.Count)
            {
                if (left) Step(rows[optionFocus], -1);
                else if (right || confirm) Step(rows[optionFocus], 1);
            }
            else if (confirm) (optionFocus == rows.Count ? erase : optionsBack).Press();
        }

        void Focus(int index)
        {
            focus = index;
            for (int i = 0; i < buttons.Count; i++) buttons[i].Focused = i == focus;
        }

        void FocusOption(int index)
        {
            optionFocus = index;
            // Moving away from Erase takes its first press back.
            if (index != rows.Count) eraseArmed = false;
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].root.Border = i == index ? Tokens.PulseGreen : Tokens.Seam;
                rows[i].root.Fill = i == index ? Tokens.Plate2 : Tokens.Plate;
                rows[i].root.Raised = i == index;
                rows[i].value.text = rows[i].setting.choices[rows[i].setting.get()].ToUpperInvariant();
            }
            erase.Focused = index == rows.Count;
            optionsBack.Focused = index == rows.Count + 1;
            erase.Label.text = eraseArmed ? "PRESS AGAIN TO ERASE EVERYTHING" : "ERASE SAVE";
        }

        void Step(OptionRow row, int by)
        {
            Options.Step(row.setting, by);
            FocusOption(rows.IndexOf(row));
        }

        // Throws the profile away. The first press only asks; the second does it and goes back to
        // the Roundhouse, where a new profile starts.
        void Erase()
        {
            if (!eraseArmed)
            {
                eraseArmed = true;
                FocusOption(rows.Count);
                return;
            }
            Profile.Reset(Combat.Config);
            Time.timeScale = 1f;
            SceneManager.LoadScene(Application.CanStreamedLevelBeLoaded("Lobby") ? "Lobby" : SceneManager.GetActiveScene().name);
        }

        // True on the frame the player asks for the pause menu.
        public static bool Asked()
        {
            var keys = Keyboard.current;
            var pad = Gamepad.current;
            return (keys != null && keys.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
        }

        // Leaves the game. In the editor this stops Play mode.
        public static void Quit()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    // Small helpers every screen uses to build its elements.
    public static class Ui
    {
        public static T Add<T>(VisualElement parent, T child, string className = null) where T : VisualElement
        {
            if (className != null) child.AddToClassList(className);
            parent.Add(child);
            return child;
        }

        // Fast at first, then settling: for things that slide or count into place. t runs from 0 to 1.
        public static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }
    }
}
