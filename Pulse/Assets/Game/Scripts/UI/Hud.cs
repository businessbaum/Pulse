using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pulse
{
    // The in-run HUD. Layout and colours come from Pulse.uss; only the edges of the screen are used,
    // and nothing is written on it that the player does not need in that moment.
    public sealed class Hud
    {
        readonly Label clock, kills, reactions, beats, hullValue, nitroValue;
        readonly Label whereLabel, whereTitle, whereDetail;
        readonly VisualElement layer, hullFill, nitroBar, nitroFill;
        // The title card of a Warden.
        readonly VisualElement card, cardWords, cardLine, wingLeft, wingRight;
        readonly Plate cardBand;
        readonly Label cardLabel, cardName;
        readonly TimerBar timer;
        readonly BossBars bossBars;
        readonly SaveMark saved;
        readonly PickupLog log;
        // The Echoes the machine holds, as small plates over the Hull bar.
        readonly Plate[] echoChips = new Plate[Echoes.Max];
        readonly EchoIcon[] echoIcons = new EchoIcon[Echoes.Max];
        readonly Label[] echoCounts = new Label[Echoes.Max];
        int shownEchoes = -1, shownStreak = -1;
        Camera view;
        int shownSeconds = -1, shownKills = -1, shownReactions = -1, shownBeats = -1, shownHull = -1, shownNitro = -1;
        string shownTitle, shownDetail;
        bool shownReady, shownBurning;

        public PauseMenu Pause { get; }
        public GameOverScreen GameOver { get; }
        public EchoScreen Echo { get; }

        // toLobby: after the machine is destroyed. recall: the player asks to go home from the pause menu.
        public Hud(VisualElement root, Action toLobby, Action recall)
        {
            root.Clear();
            var screen = Ui.Add(root, new VisualElement(), "screen");
            // Bars over mini bosses and bosses sit behind everything else.
            var worldLayer = Ui.Add(screen, new VisualElement(), "sign-layer");
            // Everything of the HUD proper is in one layer, so it can fade in as a whole.
            layer = Ui.Add(screen, new VisualElement(), "hud-layer");
            layer.pickingMode = PickingMode.Ignore;

            var counters = Ui.Add(layer, new VisualElement(), "hud-top-left");
            clock = Counter(counters, "Time");
            kills = Counter(counters, "Kills");
            reactions = Counter(counters, "Reactions");
            beats = Counter(counters, "Beats");

            // Top centre: where the machine is and what it should do next.
            var where = Ui.Add(layer, new VisualElement(), "hud-top-center");
            whereLabel = Ui.Add(where, new Label(), "label");
            whereTitle = Ui.Add(where, new Label(), "title");
            whereDetail = Ui.Add(where, new Label(), "small");
            bossBars = new BossBars(worldLayer, where);

            // Bottom left: Hull and Nitro side by side.
            var panel = Ui.Add(layer, new Plate(), "hud-bottom-left");
            var stats = Ui.Add(panel, new VisualElement(), "hud-stats");
            var hull = Ui.Add(stats, new VisualElement(), "hud-stat");
            hullValue = Row(hull, "Hull");
            hullFill = Ui.Add(Ui.Add(hull, new VisualElement(), "bar"), new VisualElement(), "bar__fill");
            var nitro = Ui.Add(stats, new VisualElement(), "hud-stat");
            nitroValue = Row(nitro, "Nitro");
            nitroBar = Ui.Add(nitro, new VisualElement(), "bar");
            nitroBar.AddToClassList("bar--nitro");
            nitroFill = Ui.Add(nitroBar, new VisualElement(), "bar__fill");

            var echoRow = Ui.Add(layer, new VisualElement(), "hud-echoes");
            for (int i = 0; i < Echoes.Max; i++)
            {
                echoChips[i] = Ui.Add(echoRow, new Plate { Cut = 6f }, "echo-chip");
                echoIcons[i] = Ui.Add(echoChips[i], new EchoIcon(), "echo-chip__icon");
                echoCounts[i] = Ui.Add(echoChips[i], new Label(), "echo-chip__count");
                echoChips[i].style.display = DisplayStyle.None;
            }

            timer = Ui.Add(Ui.Add(layer, new VisualElement(), "hud-subtitle"), new TimerBar());
            saved = Ui.Add(layer, new SaveMark());
            // Right edge, between the top and the middle: what was just picked up.
            log = new PickupLog(layer);

            // The title card of a Warden, in the middle of the screen: a plate with hatched ends, a
            // small line over a large name, a rule in the Warden's colour under it, and a wing on
            // either side. It opens from its middle and closes back into it.
            card = Ui.Add(layer, new VisualElement(), "boss-card");
            wingLeft = Wing(card, left: true);
            cardBand = Ui.Add(card, new Plate { Cut = 16f, Border = Tokens.Chalk }, "boss-card__band");
            Ui.Add(Ui.Add(cardBand, new VisualElement(), "boss-card__cap"), new Hatch());
            cardWords = Ui.Add(cardBand, new VisualElement(), "boss-card__middle");
            var over = Ui.Add(cardWords, new VisualElement(), "boss-card__over");
            Ui.Add(over, new VisualElement(), "boss-card__rule");
            Ui.Add(over, new VisualElement(), "boss-card__gem");
            cardLabel = Ui.Add(over, new Label(), "boss-card__label");
            Ui.Add(over, new VisualElement(), "boss-card__gem");
            Ui.Add(over, new VisualElement(), "boss-card__rule");
            cardName = Ui.Add(cardWords, new Label(), "boss-card__name");
            cardLine = Ui.Add(Ui.Add(cardWords, new VisualElement(), "boss-card__under"), new VisualElement(), "boss-card__line");
            Ui.Add(Ui.Add(cardBand, new VisualElement(), "boss-card__cap"), new Hatch());
            wingRight = Wing(card, left: false);
            card.style.display = DisplayStyle.None;

            Echo = new EchoScreen(screen);
            Pause = new PauseMenu(screen,
                new (string, Action)[] { ("Return to the Roundhouse", recall), ("Quit game", PauseMenu.Quit) },
                new[]
                {
                    ("W / S", "Drive forward or reverse"),
                    ("A / D", "Turn. Wheels need speed to turn."),
                    ("Space / RT", "Nitro: a burst of speed while held"),
                    ("R / Y", "Go home with the Beats you carry: charges four seconds, any hit breaks it"),
                    ("Stand in a green entrance", "Enter that dungeon"),
                    ("Drive over a drop", "Pick it up"),
                    ("Stay next to a crate", "Open it"),
                });
            GameOver = new GameOverScreen(screen, toLobby);
        }

        // One wing of the title card: a rule that ends in a small gem on the far side.
        static VisualElement Wing(VisualElement parent, bool left)
        {
            var wing = Ui.Add(parent, new VisualElement(), "boss-card__wing");
            wing.AddToClassList(left ? "boss-card__wing--left" : "boss-card__wing--right");
            if (left) Ui.Add(wing, new VisualElement(), "boss-card__gem");
            Ui.Add(wing, new VisualElement(), "boss-card__rule");
            if (!left) Ui.Add(wing, new VisualElement(), "boss-card__gem");
            return wing;
        }

        // From 0 to 1: how visible the HUD is. A run that starts from the lobby fades it in.
        public float Fade
        {
            set => layer.style.opacity = value;
        }

        public void Refresh()
        {
            var rig = Combat.Rig;
            int seconds = (int)Combat.Time, hull = Mathf.CeilToInt(rig.Hull), nitro = Mathf.RoundToInt(rig.Nitro * 100f);

            if (seconds != shownSeconds) clock.text = $"{seconds / 60}:{seconds % 60:00}";
            if (Combat.Kills != shownKills) kills.text = Combat.Kills.ToString();
            if (Combat.ReactionCount != shownReactions) reactions.text = Combat.ReactionCount.ToString();
            // What the machine carries, not what the player owns: these are lost with the machine.
            if (RunStats.Beats != shownBeats) beats.text = (shownBeats = RunStats.Beats).ToString();
            // The counter throbs with the Beats on the ground.
            beats.style.scale = new Scale(Vector3.one * (1f + 0.14f * Heart.Beat(Time.time)));
            if (hull != shownHull)
            {
                hullValue.text = $"{hull} / {Mathf.CeilToInt(rig.MaxHull)}";
                hullFill.style.width = Length.Percent(100f * rig.Hull / rig.MaxHull);
            }
            // Nitro is grey while it is too low to light and Pulse green once Space will do something.
            if (nitro != shownNitro || rig.NitroReady != shownReady || rig.Boosting != shownBurning)
            {
                nitroFill.style.width = Length.Percent(nitro);
                nitroValue.text = rig.Boosting ? "BURNING" : rig.NitroReady ? "SPACE" : $"{nitro} %";
                nitroBar.EnableInClassList("bar--ready", rig.NitroReady);
            }
            shownSeconds = seconds;
            shownKills = Combat.Kills;
            shownReactions = Combat.ReactionCount;
            shownHull = hull;
            shownNitro = nitro;
            shownReady = rig.NitroReady;
            shownBurning = rig.Boosting;

            RefreshEchoes();
            RefreshWhere(Combat.Director);
            RefreshCard(Combat.Director.Boss);
            if (view == null && Combat.Camera != null) view = Combat.Camera.GetComponent<Camera>();
            bossBars.Refresh(view);
            GameOver.Tick(Time.unscaledDeltaTime);
            saved.Tick();
            log.Tick(Time.deltaTime);
        }

        // One small plate for each Echo the machine holds. Crescendo counts on its plate, and the
        // plate of an Encore goes dim once it is used up.
        void RefreshEchoes()
        {
            int held = Echoes.Held.Count;
            if (held != shownEchoes)
            {
                shownEchoes = held;
                shownStreak = -1;
                for (int i = 0; i < echoChips.Length; i++)
                {
                    echoChips[i].style.display = i < held ? DisplayStyle.Flex : DisplayStyle.None;
                    if (i < held) echoIcons[i].Show(Echoes.Held[i]);
                }
            }
            for (int i = 0; i < held; i++)
            {
                var kind = Echoes.Held[i].kind;
                if (kind == EchoKind.Encore) echoChips[i].style.opacity = Echoes.EncoreReady ? 1f : 0.35f;
                if (kind != EchoKind.Crescendo || Echoes.Streak == shownStreak) continue;
                shownStreak = Echoes.Streak;
                echoCounts[i].text = shownStreak > 0 ? shownStreak.ToString() : "";
            }
        }

        // Fills the room under the controls of the pause menu with what the held Echoes do.
        public void ListEchoes()
        {
            var box = Pause.Extra;
            box.Clear();
            if (Echoes.Held.Count == 0) return;
            Ui.Add(box, new Label("ECHOES"), "label").AddToClassList("pause__heading");
            foreach (var echo in Echoes.Held)
            {
                var row = Ui.Add(box, new VisualElement(), "pause__key");
                Ui.Add(row, new Label(echo.displayName), "num-small");
                Ui.Add(row, new Label(echo.text), "small");
            }
        }

        void RefreshWhere(RunDirector director)
        {
            string label, title, detail = "", entering = null;

            if (director.Mode == RunMode.Dungeon)
            {
                var dungeon = director.Dungeon(director.DungeonIndex);
                label = dungeon.displayName;
                int wave = director.Waves.Wave;
                // A Warden's arena has no waves: there is the Warden, and nothing else.
                if (dungeon.boss != null) title = dungeon.bossTitle;
                else
                {
                    title = wave < 0 ? "Get ready" : $"Wave {wave + 1} / {director.Waves.WaveCount}";
                    if (wave >= 0) detail = $"{Combat.Enemies.Count} left";
                }
            }
            else if (Combat.Storm != null && Combat.Storm.Depth > 0f)
            {
                // Past the edge of the map. This replaces everything else until the machine is back.
                label = "The storm";
                title = "Turn back";
            }
            else
            {
                label = Combat.Scenery.BiomeName;
                if (director.Banner != null) title = director.Banner;
                else if (director.TargetGate < 0) title = "All dungeons cleared";
                else
                {
                    string name = director.Dungeon(director.TargetGate).displayName;
                    if (director.EnterProgress > 0f) entering = "Entering " + name;
                    title = name;
                    if (entering == null) detail = $"{Mathf.RoundToInt(director.TargetDistance)} m";
                }
            }

            whereLabel.text = label.ToUpperInvariant();
            if (title != shownTitle) whereTitle.text = shownTitle = title;
            if (detail != shownDetail) whereDetail.text = shownDetail = detail;

            // The bar at the bottom: entering a dungeon, or the teleporter home charging.
            var recall = Combat.Recall;
            if (recall != null && recall.Charging) timer.Show("Going home", recall.Progress);
            else if (recall != null && recall.Broken) timer.Show("Broken off", 0f);
            else if (entering != null) timer.Show(entering, director.EnterProgress);
            else if (Combat.Finds != null && Combat.Finds.Opening >= 0f) timer.Show("Opening", Combat.Finds.Opening);
            else timer.Hide();
        }

        // The card opens from its middle, the words come up on it, the rule under the name draws out
        // and the wings grow. At the end it all goes back the way it came.
        void RefreshCard(BossFight boss)
        {
            bool show = boss != null && boss.CardLeft > 0f;
            card.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            cardLabel.text = (boss.CardLabel ?? "").ToUpperInvariant();
            cardName.text = (boss.CardName ?? "").ToUpperInvariant();
            cardLine.style.backgroundColor = boss.CardColor;

            float into = boss.CardTotal - boss.CardLeft, left = boss.CardLeft;
            float open = Mathf.Min(Ui.EaseOut(into / 0.28f), Ui.EaseOut(left / 0.25f));
            float words = Mathf.Min(Mathf.Clamp01((into - 0.18f) / 0.2f), Mathf.Clamp01(left / 0.2f));
            float drawn = Mathf.Min(Ui.EaseOut((into - 0.3f) / 0.5f), Mathf.Clamp01(left / 0.25f));
            cardBand.style.scale = new Scale(new Vector3(open, 1f, 1f));
            cardWords.style.opacity = words;
            cardLine.style.width = Length.Percent(100f * drawn);
            wingLeft.style.scale = wingRight.style.scale = new Scale(new Vector3(drawn, 1f, 1f));
            wingLeft.style.opacity = wingRight.style.opacity = words;
        }

        // The name of the place the machine is in, for the pause menu.
        public string Place => whereLabel.text;

        public PickupLog Log => log;

        static Label Counter(VisualElement parent, string name)
        {
            var box = Ui.Add(parent, new VisualElement(), "counter");
            Ui.Add(box, new Label(name.ToUpperInvariant()), "label");
            return Ui.Add(box, new Label("0"), "num");
        }

        static Label Row(VisualElement parent, string name)
        {
            var row = Ui.Add(parent, new VisualElement(), "row");
            Ui.Add(row, new Label(name.ToUpperInvariant()), "label");
            return Ui.Add(row, new Label(), "num-small");
        }
    }
}
