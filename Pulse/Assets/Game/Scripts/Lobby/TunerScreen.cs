using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pulse
{
    // The Tuner's screen: the player's parts. A part can be raised to a higher level for Beats, up
    // to level five, or broken up for a few Beats when it is not wanted.
    //
    // Parts are grouped. On the left are the kinds of part the player owns, under a heading for
    // weapons, systems and armor: one line for the Flamethrower however many of them there are. In
    // the middle are the parts of the highlighted kind, one by one: five Flamethrowers are five
    // rows there, each with its level and, if an enemy dropped it, its ability. On the right is the
    // highlighted part: what it does now and what it would do one level up. As on the other
    // screens, every row is built once and only refilled.
    public sealed class TunerScreen
    {
        sealed class Row
        {
            public Plate root;
            public Notches notches;
            public Label name, mark, text;
        }

        sealed class StatRow
        {
            public VisualElement root, fill;
            public Label label, value;
        }

        const int Rows = 7, StatRows = 3;
        // The kinds of part that are tuned. Drives, reactors and frames have no levels.
        static readonly PartKind[] Kinds = { PartKind.Weapon, PartKind.System, PartKind.Armor };

        readonly GameConfig config;
        readonly Action changed;
        readonly VisualElement overlay;
        readonly GroupList groups;
        readonly List<Row> rows = new List<Row>();
        readonly List<StatRow> statRows = new List<StatRow>();
        readonly List<VisualElement> pips = new List<VisualElement>();
        readonly List<Item> shown = new List<Item>();
        readonly List<PartStat> now = new List<PartStat>(), next = new List<PartStat>();
        readonly Notches rank;
        readonly Label beats, groupName, groupCount, rankName, partName, ability, levelValue;
        readonly PlateButton upgrade, scrap, back;
        // The part that was new when it was highlighted. It keeps its mark until another is highlighted.
        Item justSeen;
        // Which kind of part is highlighted, and which of the player's parts of that kind.
        ModuleDef module;
        int item, first, shownBeats = -1;
        bool inRows;

        public bool IsOpen { get; private set; }

        public TunerScreen(VisualElement root, GameConfig config, Action changed)
        {
            this.config = config;
            this.changed = changed;

            overlay = Ui.Add(root, new VisualElement(), "overlay");
            var panel = Ui.Add(overlay, new Plate(), "station");
            var header = Ui.Add(panel, new VisualElement(), "station__header");
            Ui.Add(header, new Label("TUNER"), "display");
            var purse = Ui.Add(header, new VisualElement(), "station__purse");
            Ui.Add(purse, new Label("BEATS"), "label");
            beats = Ui.Add(purse, new Label(), "num");

            var body = Ui.Add(panel, new VisualElement(), "station__body");
            groups = new GroupList(Ui.Add(body, new VisualElement(), "station__list"), picked =>
            {
                module = picked;
                item = first = 0;
                inRows = false;
                Refresh();
            });

            var middle = Ui.Add(body, new VisualElement(), "station__middle");
            middle.RegisterCallback<WheelEvent>(wheel =>
            {
                if (shown.Count <= Rows) return;
                first = Mathf.Clamp(first + (wheel.delta.y > 0f ? 1 : -1), 0, shown.Count - Rows);
                item = Mathf.Clamp(item, first, first + Rows - 1);
                inRows = true;
                Refresh();
            });
            // What the rows under it are all of.
            var over = Ui.Add(middle, new VisualElement(), "station__fits");
            groupName = Ui.Add(over, new Label(), "label");
            groupCount = Ui.Add(over, new Label(), "small");
            for (int i = 0; i < Rows; i++)
            {
                int index = i;
                var row = new Row { root = Ui.Add(middle, new Plate { Cut = 6f }, "part") };
                var head = Ui.Add(row.root, new VisualElement(), "part__head");
                row.name = Ui.Add(head, new Label(), "num-small");
                row.name.AddToClassList("part__name");
                row.name.AddToClassList("clip");
                row.notches = Ui.Add(head, new Notches());
                row.mark = Ui.Add(head, new Label(), "small");
                row.mark.AddToClassList("part__cost");
                row.text = Ui.Add(row.root, new Label(), "part__text");
                row.text.AddToClassList("clip");
                row.root.AddManipulator(new Clickable(() =>
                {
                    if (first + index >= shown.Count) return;
                    item = first + index;
                    inRows = true;
                    Refresh();
                }));
                rows.Add(row);
            }

            // The highlighted part: how good its ability is, what the ability does, its level, and
            // its numbers now and one level up.
            var sheet = Ui.Add(body, new VisualElement(), "station__parts");
            var top = Ui.Add(sheet, new VisualElement(), "tuner__rank");
            rankName = Ui.Add(top, new Label(), "label");
            rank = Ui.Add(top, new Notches());
            partName = Ui.Add(sheet, new Label(), "title");
            partName.AddToClassList("clip");
            ability = Ui.Add(sheet, new Label(), "archive__text");
            var level = Ui.Add(sheet, new VisualElement(), "tuner__level");
            var levelRow = Ui.Add(level, new VisualElement(), "row");
            Ui.Add(levelRow, new Label("LEVEL"), "label");
            levelValue = Ui.Add(levelRow, new Label(), "num-small");
            var pipRow = Ui.Add(level, new VisualElement(), "pips");
            for (int i = 0; i < Tuning.MaxLevel; i++) pips.Add(Ui.Add(pipRow, new VisualElement(), "pip"));
            var lines = Ui.Add(sheet, new VisualElement(), "tuner__stats");
            for (int i = 0; i < StatRows; i++)
            {
                var row = new StatRow { root = Ui.Add(lines, new VisualElement(), "stat-row") };
                var words = Ui.Add(row.root, new VisualElement(), "stat-row__words");
                row.label = Ui.Add(words, new Label(), "label");
                row.value = Ui.Add(words, new Label(), "num-small");
                row.fill = Ui.Add(Ui.Add(row.root, new VisualElement(), "stat-row__bar"), new VisualElement(), "stat-row__fill");
                statRows.Add(row);
            }

            var footer = Ui.Add(panel, new VisualElement(), "station__footer");
            back = Ui.Add(footer, new PlateButton("Back", Close));
            var act = Ui.Add(footer, new VisualElement(), "station__buttons");
            scrap = Ui.Add(act, new PlateButton("Scrap", () => Scrap()), "station__save");
            upgrade = Ui.Add(act, new PlateButton("Upgrade", () => Upgrade(), primary: true), "station__take");
        }

        public void Open()
        {
            IsOpen = true;
            overlay.style.display = DisplayStyle.Flex;
            justSeen = null;
            // Start where something new is waiting.
            module = null;
            item = first = 0;
            inRows = false;
            var fresh = Profile.Items.Find(one => one.fresh && Array.IndexOf(Kinds, Parts.KindOf(one.def)) >= 0);
            if (fresh != null)
            {
                Show(fresh);
                return;
            }
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
            float throb = 1f + 0.1f * Heart.Beat(Time.time);
            beats.style.scale = new Scale(new Vector3(throb, throb, 1f));

            var nav = Nav.Read();
            if (nav.back) { back.Press(); return; }
            if (!inRows)
            {
                if (nav.up || nav.down)
                {
                    module = groups.Step(module, nav.down ? 1 : -1);
                    item = first = 0;
                }
                else if (nav.right || nav.confirm) inRows = shown.Count > 0;
                else return;
            }
            else if (nav.left) inRows = false;
            else if (nav.up && item > 0) item--;
            else if (nav.down && item < shown.Count - 1) item++;
            else if (nav.confirm) { upgrade.Press(); return; }
            else if (nav.remove) { scrap.Press(); return; }
            else return;
            Refresh();
        }

        // ---- What a test calls in place of the keyboard ----

        public void Show(Item wanted)
        {
            if (wanted != null && Profile.Items.Contains(wanted))
            {
                module = wanted.def;
                List();
                item = Mathf.Max(0, shown.IndexOf(wanted));
                inRows = true;
            }
            Refresh();
        }

        public Item Shown => item < shown.Count ? shown[item] : null;
        // How many kinds of part the left column lists, and how many parts of the highlighted kind the middle one.
        public int GroupCount => groups.Count;
        public int ShownCount => shown.Count;

        // Raises the highlighted part by one level. False when it is at the top or the Beats are short.
        public bool Upgrade()
        {
            bool done = Profile.Upgrade(Shown);
            if (done) changed();
            Refresh();
            return done;
        }

        // Breaks the highlighted part up for Beats. False when it sits on a machine.
        public bool Scrap()
        {
            bool done = Profile.Scrap(Shown);
            if (done) changed();
            Refresh();
            return done;
        }

        // ---- What is shown ----

        // The player's parts of the highlighted kind, the best first.
        void List()
        {
            shown.Clear();
            foreach (var one in Profile.Items)
                if (one.def == module) shown.Add(one);
            shown.Sort((a, b) => Worth(b).CompareTo(Worth(a)));
        }

        static int Worth(Item one) => one.level * 10 + (int)one.rarity;

        static string Titled(Item one) => $"{(one.ability != null ? one.ability.displayName : "Plain")}    Lv {one.level}";

        void Refresh()
        {
            if (Profile.Beats != shownBeats) beats.text = (shownBeats = Profile.Beats).ToString();

            // The left column: every kind of part the player has at least one of.
            module = groups.Fill(config, Kinds, module, inRows);
            List();
            item = Mathf.Clamp(item, 0, Mathf.Max(0, shown.Count - 1));
            if (shown.Count == 0) inRows = false;
            first = Mathf.Clamp(first, Mathf.Max(0, item - Rows + 1), item);
            first = Mathf.Clamp(first, 0, Mathf.Max(0, shown.Count - Rows));
            var chosen = Shown;
            // Looking at a part is what takes its mark away, but not while it is still being looked at.
            if (chosen != null && chosen.fresh && inRows)
            {
                Profile.Seen(chosen);
                justSeen = chosen;
                groups.Fill(config, Kinds, module, inRows);
            }
            else if (chosen != justSeen) justSeen = null;

            groupName.text = module != null ? module.displayName.ToUpperInvariant() : "";
            groupCount.text = shown.Count > 1 ? shown.Count.ToString() : "";
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                int index = first + i;
                bool used = index < shown.Count;
                row.root.style.visibility = used ? Visibility.Visible : Visibility.Hidden;
                if (!used)
                {
                    row.notches.Rarity = Rarity.None;
                    continue;
                }
                var one = shown[index];
                bool focused = index == item;
                row.root.Border = !focused ? Tokens.Seam : inRows ? Tokens.PulseGreen : Tokens.Chalk;
                row.root.Fill = focused ? Tokens.Plate2 : Tokens.Plate;
                row.root.Raised = focused;
                // They are all the same kind of part, so a row is named after what sets it apart.
                row.name.text = Titled(one);
                row.notches.Rarity = one.rarity;
                row.text.text = one.ability != null ? one.ability.Describe(one.rarity) : one.def.description;
                bool isNew = one.fresh || one == justSeen;
                row.mark.text = isNew ? "NEW" : Profile.IsFitted(one) ? "FITTED" : "";
                row.mark.EnableInClassList("part__cost--on", isNew);
            }

            // The sheet of the highlighted part.
            bool any = chosen != null;
            rank.Rarity = any ? chosen.rarity : Rarity.None;
            rankName.text = !any ? "" : chosen.rarity == Rarity.None ? "PLAIN" : Tuning.Name(chosen.rarity).ToUpperInvariant();
            partName.text = any ? chosen.Name.ToUpperInvariant() : "";
            ability.text = !any ? "" : chosen.ability != null ? $"{chosen.ability.displayName}: {chosen.ability.Describe(chosen.rarity)}" : chosen.def.description;
            levelValue.text = any ? $"{chosen.level} / {Tuning.MaxLevel}" : "";
            for (int i = 0; i < pips.Count; i++)
            {
                pips[i].style.visibility = any ? Visibility.Visible : Visibility.Hidden;
                pips[i].EnableInClassList("pip--on", any && i < chosen.level);
            }
            now.Clear();
            next.Clear();
            bool top = any && chosen.level >= Tuning.MaxLevel;
            if (any)
            {
                Tuning.Sheet(chosen, chosen.level, now);
                Tuning.Sheet(chosen, Mathf.Min(chosen.level + 1, Tuning.MaxLevel), next);
            }
            for (int i = 0; i < statRows.Count; i++)
            {
                var row = statRows[i];
                bool used = i < now.Count;
                row.root.style.visibility = used ? Visibility.Visible : Visibility.Hidden;
                if (!used) continue;
                row.label.text = now[i].label.ToUpperInvariant();
                // What it is now, and what one more level would make of it.
                bool grows = !top && i < next.Count && next[i].value != now[i].value;
                row.value.text = grows ? $"{now[i].value}    >    {next[i].value}" : now[i].value;
                row.fill.parent.style.display = now[i].share >= 0f ? DisplayStyle.Flex : DisplayStyle.None;
                row.fill.style.width = Length.Percent(100f * Mathf.Clamp01(now[i].share));
            }

            int cost = any ? Tuning.UpgradeCost(chosen) : 0;
            upgrade.Usable = any && !top && cost <= Profile.Beats;
            upgrade.Label.text = !any ? "UPGRADE" : top ? "TOP LEVEL" : cost > Profile.Beats ? $"{cost - Profile.Beats} BEATS SHORT" : $"UPGRADE    {cost}";
            bool fitted = any && Profile.IsFitted(chosen);
            scrap.Usable = any && !fitted;
            scrap.Label.text = !any ? "SCRAP" : fitted ? "FITTED" : $"SCRAP    +{Tuning.ScrapValue(chosen)}";
        }
    }

    // A column that lists the kinds of part the player owns, under a heading for each sort: one
    // line for the Flamethrower, however many Flamethrowers there are, with their number next to
    // it. Its lines are built once and refilled. A list longer than the column moves with the
    // highlighted line.
    public sealed class GroupList
    {
        sealed class Line
        {
            public VisualElement root;
            public KindIcon icon;
            public Label name, count, tag;
        }

        struct Entry
        {
            public bool heading;
            public PartKind kind;
            public ModuleDef module;
            public int count;
            public bool fresh;
        }

        const int Lines = 17;

        readonly List<Line> lines = new List<Line>();
        readonly List<Entry> entries = new List<Entry>();
        readonly Action<ModuleDef> picked;
        int first;

        // How many kinds of part are listed.
        public int Count { get; private set; }

        public GroupList(VisualElement column, Action<ModuleDef> picked)
        {
            this.picked = picked;
            for (int i = 0; i < Lines; i++)
            {
                int index = i;
                var line = new Line { root = Ui.Add(column, new VisualElement(), "group-row") };
                line.icon = Ui.Add(line.root, new KindIcon(PartKind.Weapon));
                line.name = Ui.Add(line.root, new Label(), "group-row__name");
                line.name.AddToClassList("clip");
                line.tag = Ui.Add(line.root, new Label("NEW"), "tag");
                line.count = Ui.Add(line.root, new Label(), "group-row__count");
                line.root.AddManipulator(new Clickable(() =>
                {
                    int at = first + index;
                    if (at < entries.Count && !entries[at].heading) this.picked(entries[at].module);
                }));
                lines.Add(line);
            }
        }

        // Lists the kinds of part the player has among these sorts and highlights one of them: the
        // one asked for if it is there, otherwise the first. Returns the one that is highlighted.
        // elsewhere: the player is working in another column, so the highlight is only a marker.
        public ModuleDef Fill(GameConfig config, PartKind[] sorts, ModuleDef wanted, bool elsewhere)
        {
            entries.Clear();
            Count = 0;
            foreach (var sort in sorts)
            {
                int heading = entries.Count;
                entries.Add(new Entry { heading = true, kind = sort });
                foreach (var module in config.modules)
                {
                    if (module == null || Parts.KindOf(module) != sort) continue;
                    int count = 0;
                    bool fresh = false;
                    foreach (var one in Profile.Items)
                    {
                        if (one.def != module) continue;
                        count++;
                        fresh |= one.fresh;
                    }
                    if (count == 0) continue;
                    entries.Add(new Entry { module = module, kind = sort, count = count, fresh = fresh });
                    Count++;
                }
                // A sort the player has nothing of is not listed at all.
                if (entries.Count == heading + 1) entries.RemoveAt(heading);
            }

            int at = entries.FindIndex(e => !e.heading && e.module == wanted);
            if (at < 0) at = entries.FindIndex(e => !e.heading);
            var chosen = at >= 0 ? entries[at].module : null;
            // The list moves only as far as it must to keep the highlighted line, and its heading, on screen.
            if (at >= 0)
            {
                first = Mathf.Clamp(first, Mathf.Max(0, at - Lines + 1), Mathf.Max(0, at - 1));
                first = Mathf.Clamp(first, 0, Mathf.Max(0, entries.Count - Lines));
            }
            else first = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                int index = first + i;
                bool used = index < entries.Count;
                line.root.style.visibility = used ? Visibility.Visible : Visibility.Hidden;
                if (!used) continue;
                var entry = entries[index];
                line.root.EnableInClassList("group-row--head", entry.heading);
                line.root.EnableInClassList("group-row--on", !entry.heading && index == at);
                line.root.EnableInClassList("group-row--focus", !entry.heading && index == at && !elsewhere);
                line.icon.style.display = entry.heading ? DisplayStyle.Flex : DisplayStyle.None;
                if (entry.heading) line.icon.Kind = entry.kind;
                line.name.text = entry.heading ? Parts.Plural[(int)entry.kind].ToUpperInvariant() : entry.module.displayName;
                line.count.text = entry.heading ? "" : entry.count.ToString();
                line.tag.style.display = !entry.heading && entry.fresh ? DisplayStyle.Flex : DisplayStyle.None;
            }
            return chosen;
        }

        // The kind of part one line up or down from another, going round at the ends.
        public ModuleDef Step(ModuleDef from, int by)
        {
            int at = entries.FindIndex(e => !e.heading && e.module == from);
            if (at < 0) return from;
            for (int tries = 0; tries < entries.Count; tries++)
            {
                at = (at + by + entries.Count) % entries.Count;
                if (!entries[at].heading) return entries[at].module;
            }
            return from;
        }
    }
}
