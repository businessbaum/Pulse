using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Pulse
{
    // The end of a run. A hatched banner, what destroyed the machine, eight numbers that count up,
    // a row of smaller facts, and one large button to go back. Everything arrives piece by piece.
    // It animates itself from Tick, on real time, because the game stands still while it shows.
    public sealed class GameOverScreen
    {
        sealed class Counter
        {
            public Label label;
            public float target, start;
            public bool asTime;
        }

        // Step: delay between two pieces arriving. Slide: how long one takes. Count: how long numbers run.
        const float Step = 0.07f, Slide = 0.18f, Count = 0.9f, Rise = 22f;

        readonly VisualElement overlay, banner, line;
        readonly Label machine, cause, place;
        readonly List<(VisualElement element, float start)> pieces = new List<(VisualElement, float)>();
        readonly List<Counter> counters = new List<Counter>();
        readonly Dictionary<string, Counter> byName = new Dictionary<string, Counter>();
        readonly Dictionary<string, Label> facts = new Dictionary<string, Label>();
        readonly PlateButton back;
        float clock, next = 0.3f;

        public bool IsOpen { get; private set; }

        public GameOverScreen(VisualElement root, Action restart)
        {
            overlay = Ui.Add(root, new VisualElement(), "overlay");
            overlay.AddToClassList("overlay--heavy");
            var panel = Ui.Add(overlay, new Plate(), "gameover");

            // The banner: hatched like every warning, with the headline on top of it.
            var band = Ui.Add(panel, new VisualElement(), "gameover__band");
            Ui.Add(band, new Hatch());
            banner = Ui.Add(band, new VisualElement(), "gameover__headline");
            Ui.Add(banner, new Label("RUN OVER"), "label");
            Ui.Add(banner, new Label("RIG DESTROYED"), "gameover__title");
            line = Ui.Add(Ui.Add(panel, new VisualElement(), "gameover__rule"), new VisualElement(), "gameover__line");

            var body = Ui.Add(panel, new VisualElement(), "gameover__body");
            var who = Piece(Ui.Add(body, new VisualElement(), "gameover__who"));
            machine = Pair(who, "Machine");
            cause = Pair(who, "Destroyed by");
            place = Pair(who, "Where");

            var grid = Ui.Add(body, new VisualElement(), "gameover__grid");
            foreach (string name in new[] { "Time", "Kills", "Bosses beaten", "Dungeons cleared", "Reactions", "Damage dealt", "Damage taken", "Beats lost" })
            {
                var tile = Piece(Ui.Add(grid, new Plate(), "stat"));
                Ui.Add(tile, new Label(name.ToUpperInvariant()), "label");
                var counter = new Counter { label = Ui.Add(tile, new Label("0"), "stat__num"), start = next, asTime = name == "Time" };
                counters.Add(counter);
                byName[name] = counter;
            }

            // Two rows of smaller facts: what the strands did together, and what else there was.
            foreach (var row in new[]
            {
                new[] { "Firestorm", "Flash", "Plasma", "Thermal Shock", "Superconductor", "Black Ice" },
                new[] { "Mini bosses", "Bosses", "Parts", "Echoes", "Veins", "Crates" },
            })
            {
                var chips = Piece(Ui.Add(body, new VisualElement(), "gameover__facts"));
                foreach (string name in row)
                {
                    var chip = Ui.Add(chips, new Plate { Cut = 5f }, "chip");
                    Ui.Add(chip, new Label(name.ToUpperInvariant()), "chip__label");
                    facts[name] = Ui.Add(chip, new Label("0"), "chip__num");
                }
            }

            back = Piece(Ui.Add(body, new PlateButton("Back to the Roundhouse", restart, primary: true), "button--huge"));
        }

        // Registers an element that arrives after the ones before it.
        T Piece<T>(T element) where T : VisualElement
        {
            pieces.Add((element, next));
            next += Step;
            element.style.opacity = 0f;
            return element;
        }

        static Label Pair(VisualElement parent, string name)
        {
            var box = Ui.Add(parent, new VisualElement(), "gameover__pair");
            Ui.Add(box, new Label(name.ToUpperInvariant()), "label");
            return Ui.Add(box, new Label(), "title");
        }

        public void Show()
        {
            IsOpen = true;
            clock = 0f;
            var director = Combat.Director;
            machine.text = Combat.Rig.Loadout.displayName.ToUpperInvariant();
            cause.text = (string.IsNullOrEmpty(RunStats.LastHitBy) ? "Unknown" : RunStats.LastHitBy).ToUpperInvariant();
            place.text = (director.Mode == RunMode.Dungeon ? director.Dungeon(director.DungeonIndex).displayName : Combat.Scenery.BiomeName).ToUpperInvariant();

            byName["Time"].target = Mathf.Floor(Combat.Time);
            byName["Kills"].target = Combat.Kills;
            byName["Bosses beaten"].target = RunStats.MiniBosses + RunStats.Bosses;
            byName["Dungeons cleared"].target = director.ClearedCount;
            byName["Reactions"].target = Combat.ReactionCount;
            byName["Damage dealt"].target = Mathf.Round(RunStats.DamageDealt);
            byName["Damage taken"].target = Mathf.Round(RunStats.DamageTaken);
            byName["Beats lost"].target = RunStats.Beats;

            facts["Firestorm"].text = RunStats.Firestorms.ToString();
            facts["Flash"].text = RunStats.Flashes.ToString();
            facts["Plasma"].text = RunStats.Plasmas.ToString();
            facts["Mini bosses"].text = RunStats.MiniBosses.ToString();
            facts["Bosses"].text = RunStats.Bosses.ToString();
            facts["Thermal Shock"].text = RunStats.ThermalShocks.ToString();
            facts["Superconductor"].text = RunStats.Superconductors.ToString();
            facts["Black Ice"].text = RunStats.BlackIces.ToString();
            facts["Parts"].text = RunStats.Parts.ToString();
            facts["Echoes"].text = RunStats.EchoCount.ToString();
            facts["Veins"].text = RunStats.Veins.ToString();
            facts["Crates"].text = RunStats.Caches.ToString();

            overlay.style.display = DisplayStyle.Flex;
            Tick(0f);
        }

        // Called every frame with real time, since game time stands still.
        public void Tick(float dt)
        {
            if (!IsOpen) return;
            clock += dt;

            // The headline slides in from the left; the green line under it draws itself.
            float arrive = Ui.EaseOut(clock / 0.25f);
            banner.style.translate = new Translate(-140f * (1f - arrive), 0f);
            banner.style.opacity = arrive;
            line.style.width = Length.Percent(100f * Ui.EaseOut((clock - 0.15f) / 0.5f));

            foreach (var (element, start) in pieces)
            {
                float shown = Ui.EaseOut((clock - start) / Slide);
                element.style.opacity = shown;
                element.style.translate = new Translate(0f, Rise * (1f - shown));
            }
            foreach (var counter in counters)
            {
                float value = Mathf.Round(counter.target * Ui.EaseOut((clock - counter.start) / Count));
                counter.label.text = counter.asTime ? $"{(int)value / 60}:{(int)value % 60:00}" : ((int)value).ToString();
            }

            // The one thing that pulses on this screen: the way back.
            if (clock > next + Slide) back.Glow = 0.5f + 0.5f * Mathf.Sin(clock * Mathf.PI * 2f);
        }

        // The keys that leave the screen. The mouse works through the button itself. Space is left out
        // on purpose: it is the Nitro key, and a player who dies holding it should still see the screen.
        public void Update()
        {
            if (!IsOpen || clock < 0.5f) return;
            var keys = Keyboard.current;
            var pad = Gamepad.current;
            bool confirm = (keys != null && (keys.enterKey.wasPressedThisFrame || keys.eKey.wasPressedThisFrame))
                || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            if (confirm) back.Press();
        }
    }
}
