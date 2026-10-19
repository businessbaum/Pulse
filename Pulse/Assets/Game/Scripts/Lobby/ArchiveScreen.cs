using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pulse
{
    // The Archivist's screen. It has two sides, and one button swaps them.
    //
    // Parts: every part there is, sorted by kind. The kinds are on the left, the parts of the
    // highlighted kind as tiles in the middle, and the data sheet of the highlighted part on the
    // right. A part the player has not found is a hatched plate that only says where it is kept. A
    // part found since the last visit is marked NEW until it has been looked at.
    //
    // Codex: what the player has met out there. Reactions, Strays and Echoes, laid out the same way.
    // The Archivist pays Beats for everything that is new to it: an entry it still owes for carries
    // the sum on its tile, and looking at the entry is what gets it paid.
    //
    // Like the Mechanic's screen, nothing here takes its size from its content: all tiles and all
    // lines of the data sheet are built once and only refilled.
    public sealed class ArchiveScreen
    {
        sealed class Tile
        {
            public Plate root;
            public PartIcon icon;
            public CodexIcon glyph;
            public Hatch hatch;
            public Label name, tag;
        }

        sealed class StatRow
        {
            public VisualElement root, bar, fill;
            public Label label, value;
        }

        const int Columns = 3, Tiles = 12, StatRows = 6;

        readonly GameConfig config;
        readonly VisualElement overlay, chips;
        readonly List<Plate> cards = new List<Plate>();
        readonly List<Label> cardNames = new List<Label>(), cardNotes = new List<Label>(), cardTags = new List<Label>();
        readonly List<KindIcon> cardIcons = new List<KindIcon>();
        readonly List<CodexIcon> cardGlyphs = new List<CodexIcon>();
        readonly List<Tile> tiles = new List<Tile>();
        readonly List<StatRow> statRows = new List<StatRow>();
        readonly List<ScriptableObject> shown = new List<ScriptableObject>();
        readonly List<Codex.Entry> entries = new List<Codex.Entry>();
        readonly List<PartStat> stats = new List<PartStat>();
        readonly List<KindIcon> kindIcons = new List<KindIcon>();
        readonly CodexIcon sheetGlyph;
        readonly Label total, purse, kindLabel, partName, text;
        readonly PlateButton back, swap;
        // The part that was new when it was highlighted. It keeps its mark until another is highlighted.
        ScriptableObject justSeen;
        int kind, item;
        bool inTiles, codex;

        public bool IsOpen { get; private set; }
        // Which side is up.
        public bool InCodex => codex;
        // How many entries the highlighted section of the Codex has.
        public int EntryCount => entries.Count;

        int Kinds => codex ? Codex.Sections : Parts.Kinds;
        int Count => codex ? entries.Count : shown.Count;

        public ArchiveScreen(VisualElement root, GameConfig config)
        {
            this.config = config;

            overlay = Ui.Add(root, new VisualElement(), "overlay");
            var panel = Ui.Add(overlay, new Plate(), "station");
            var header = Ui.Add(panel, new VisualElement(), "station__header");
            Ui.Add(header, new Label("ARCHIVIST"), "display");
            var purses = Ui.Add(header, new VisualElement(), "station__purses");
            var beats = Ui.Add(purses, new VisualElement(), "station__purse");
            Ui.Add(beats, new Label("BEATS"), "label");
            purse = Ui.Add(beats, new Label(), "num");
            var count = Ui.Add(purses, new VisualElement(), "station__purse");
            Ui.Add(count, new Label("FOUND"), "label");
            total = Ui.Add(count, new Label(), "num");

            var body = Ui.Add(panel, new VisualElement(), "station__body");

            var list = Ui.Add(body, new VisualElement(), "station__list");
            for (int i = 0; i < Parts.Kinds; i++)
            {
                int index = i;
                var card = Ui.Add(list, new Plate(), "card");
                var line = Ui.Add(card, new VisualElement(), "card__line");
                cardIcons.Add(Ui.Add(line, new KindIcon((PartKind)i)));
                cardGlyphs.Add(Ui.Add(line, new CodexIcon(), "kind-icon"));
                var name = Ui.Add(line, new Label(), "title");
                name.AddToClassList("card__name");
                cardNames.Add(name);
                cardTags.Add(Ui.Add(line, new Label("NEW"), "tag"));
                cardNotes.Add(Ui.Add(card, new Label(), "small"));
                card.AddManipulator(new Clickable(() => { kind = index; item = 0; inTiles = false; Refresh(); }));
                cards.Add(card);
            }

            var grid = Ui.Add(body, new VisualElement(), "archive__grid");
            for (int i = 0; i < Tiles; i++)
            {
                int index = i;
                var tile = new Tile { root = Ui.Add(grid, new Plate { Cut = 8f }, "tile") };
                tile.hatch = Ui.Add(tile.root, new Hatch());
                tile.tag = Ui.Add(tile.root, new Label("NEW"), "tag");
                tile.tag.AddToClassList("tile__tag");
                tile.icon = Ui.Add(tile.root, new PartIcon(), "tile__icon");
                tile.glyph = Ui.Add(tile.root, new CodexIcon(), "tile__icon");
                tile.name = Ui.Add(tile.root, new Label(), "tile__name");
                tile.root.AddManipulator(new Clickable(() =>
                {
                    if (index >= Count) return;
                    item = index;
                    inTiles = true;
                    Refresh();
                }));
                tiles.Add(tile);
            }

            var sheet = Ui.Add(body, new VisualElement(), "archive__sheet");
            // The symbol of the highlighted kind stands large over its data sheet.
            var kindRow = Ui.Add(sheet, new VisualElement(), "archive__kind");
            for (int i = 0; i < Parts.Kinds; i++)
            {
                var icon = Ui.Add(kindRow, new KindIcon((PartKind)i));
                icon.style.display = DisplayStyle.None;
                kindIcons.Add(icon);
            }
            sheetGlyph = Ui.Add(kindRow, new CodexIcon(), "kind-icon");
            kindLabel = Ui.Add(kindRow, new Label(), "label");
            partName = Ui.Add(sheet, new Label(), "title");
            partName.AddToClassList("clip");
            chips = Ui.Add(sheet, new VisualElement(), "archive__chips");
            text = Ui.Add(sheet, new Label(), "archive__text");
            var lines = Ui.Add(sheet, new VisualElement(), "archive__stats");
            for (int i = 0; i < StatRows; i++)
            {
                var row = new StatRow { root = Ui.Add(lines, new VisualElement(), "stat-row") };
                var words = Ui.Add(row.root, new VisualElement(), "stat-row__words");
                row.label = Ui.Add(words, new Label(), "label");
                row.value = Ui.Add(words, new Label(), "num-small");
                row.bar = Ui.Add(row.root, new VisualElement(), "stat-row__bar");
                row.fill = Ui.Add(row.bar, new VisualElement(), "stat-row__fill");
                statRows.Add(row);
            }
            // Nothing is decided on this screen, so it has no primary action. The second button
            // turns the screen over to its other side.
            var footer = Ui.Add(panel, new VisualElement(), "station__footer");
            back = Ui.Add(footer, new PlateButton("Back", Close));
            swap = Ui.Add(footer, new PlateButton("Codex", () => ShowCodex(!codex)));
        }

        public void Open()
        {
            IsOpen = true;
            overlay.style.display = DisplayStyle.Flex;
            justSeen = null;
            codex = false;
            // Start where something new is waiting: a part first, then whatever the Archivist owes for.
            kind = item = 0;
            inTiles = false;
            for (int k = 0; k < Parts.Kinds; k++)
            {
                Parts.Found(config, (PartKind)k, out _, out int fresh);
                if (fresh == 0) continue;
                kind = k;
                Parts.All(config, (PartKind)k, shown);
                item = Mathf.Max(0, shown.FindIndex(Profile.IsFresh));
                inTiles = true;
                Refresh();
                return;
            }
            if (Codex.Owed(config) > 0) ShowCodex(true);
            else Refresh();
        }

        public void Close()
        {
            IsOpen = false;
            overlay.style.display = DisplayStyle.None;
        }

        // Highlights one part, as the keyboard or a click would. Also what a test calls.
        public void Show(PartKind which, int index)
        {
            codex = false;
            kind = (int)which;
            item = index;
            inTiles = true;
            Refresh();
        }

        // Turns the screen to the Codex or back to the parts. In the Codex it starts on the first
        // section the Archivist still owes for.
        public void ShowCodex(bool show)
        {
            codex = show;
            kind = item = 0;
            inTiles = false;
            justSeen = null;
            if (show)
                for (int s = 0; s < Codex.Sections; s++)
                {
                    Codex.Known(config, (Codex.Section)s, out _, out int owed);
                    if (owed == 0) continue;
                    kind = s;
                    break;
                }
            Refresh();
        }

        // Highlights one entry of the Codex, which also gets it paid. Also what a test calls.
        public void Show(Codex.Section section, int index)
        {
            codex = true;
            kind = (int)section;
            item = index;
            inTiles = true;
            Refresh();
        }

        // Called every frame while the screen is open.
        public void Update()
        {
            var nav = Nav.Read();
            if (nav.back) { back.Press(); return; }
            if (nav.save) { swap.Press(); return; }
            int was = kind * 100 + item + (inTiles ? 10000 : 0);
            if (!inTiles)
            {
                if (nav.up) kind = (kind + Kinds - 1) % Kinds;
                else if (nav.down) kind = (kind + 1) % Kinds;
                else if (nav.right || nav.confirm) inTiles = true;
                if (!inTiles) item = 0;
            }
            else
            {
                int count = Count;
                // Left from the first column of tiles leads back to the kinds.
                if (nav.left && item % Columns == 0) inTiles = false;
                else if (nav.left) item--;
                else if (nav.right && item % Columns < Columns - 1 && item + 1 < count) item++;
                else if (nav.up && item >= Columns) item -= Columns;
                else if (nav.down && item + Columns < count) item += Columns;
            }
            if (was != kind * 100 + item + (inTiles ? 10000 : 0)) Refresh();
        }

        // Shows the highlighted kind and what is highlighted in it. Only texts, colours and the small chips change.
        void Refresh()
        {
            kind = Mathf.Clamp(kind, 0, Kinds - 1);
            chips.Clear();
            stats.Clear();
            if (codex) RefreshCodex();
            else RefreshParts();
            purse.text = Profile.Beats.ToString();
            // The way to the other side says when something is waiting there.
            int owed = codex ? 0 : Codex.Owed(config);
            swap.Label.text = codex ? "PARTS" : owed > 0 ? $"CODEX  +{owed}" : "CODEX";

            for (int i = 0; i < statRows.Count; i++)
            {
                var row = statRows[i];
                bool used = i < stats.Count;
                row.root.style.visibility = used ? Visibility.Visible : Visibility.Hidden;
                if (!used) continue;
                row.label.text = stats[i].label.ToUpperInvariant();
                row.value.text = stats[i].value;
                row.bar.style.display = stats[i].share >= 0f ? DisplayStyle.Flex : DisplayStyle.None;
                row.fill.style.width = Length.Percent(100f * Mathf.Max(0f, stats[i].share));
            }
        }

        // One card of the list on the left.
        void Card(int k, bool used, string name, string note, string tag)
        {
            cards[k].style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
            if (!used) return;
            cardNames[k].text = name.ToUpperInvariant();
            cardNotes[k].text = note;
            cardTags[k].text = tag ?? "";
            cardTags[k].style.display = string.IsNullOrEmpty(tag) ? DisplayStyle.None : DisplayStyle.Flex;
            cardIcons[k].style.display = codex ? DisplayStyle.None : DisplayStyle.Flex;
            cardGlyphs[k].style.display = codex ? DisplayStyle.Flex : DisplayStyle.None;
            cards[k].Border = k != kind ? Tokens.Seam : inTiles ? Tokens.Chalk : Tokens.PulseGreen;
            cards[k].Fill = k == kind ? Tokens.Plate2 : Tokens.Plate;
            cards[k].Raised = k == kind;
        }

        // One tile of the grid. have: whether the player has found what it shows.
        void Paint(Tile tile, int i, bool have, string name, string tag)
        {
            bool focused = i == item;
            tile.root.Border = !focused ? Tokens.Seam : inTiles ? Tokens.PulseGreen : Tokens.Chalk;
            // The stripes of what is not found are the lighter grey, so its plate stays the darker one.
            tile.root.Fill = focused && have ? Tokens.Plate2 : Tokens.Plate;
            // What is found stands on the panel, and the highlighted one higher. What is not is let into it.
            tile.root.Raised = focused && have;
            tile.root.Sunk = !have;
            tile.hatch.style.display = have ? DisplayStyle.None : DisplayStyle.Flex;
            tile.icon.style.display = codex ? DisplayStyle.None : DisplayStyle.Flex;
            tile.glyph.style.display = codex ? DisplayStyle.Flex : DisplayStyle.None;
            tile.name.text = have ? name.ToUpperInvariant() : "NOT FOUND";
            tile.name.EnableInClassList("tile__name--dim", !have);
            tile.tag.text = tag ?? "";
            tile.tag.style.display = string.IsNullOrEmpty(tag) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void RefreshParts()
        {
            int found = 0, all = 0;
            for (int k = 0; k < cards.Count; k++)
            {
                int have = Parts.Found(config, (PartKind)k, out int of, out int fresh);
                found += have;
                all += of;
                Card(k, true, Parts.Plural[k], $"{have} / {of}", fresh > 0 ? "NEW" : null);
            }
            total.text = $"{found} / {all}";

            Parts.All(config, (PartKind)kind, shown);
            item = Mathf.Clamp(item, 0, Mathf.Max(0, shown.Count - 1));
            var part = shown.Count > 0 ? shown[item] : null;
            bool known = Profile.IsFound(part);
            // Looking at a part is what takes its mark away, but not while it is still being looked at.
            if (Profile.IsFresh(part))
            {
                Profile.Seen(part);
                justSeen = part;
            }
            else if (part != justSeen) justSeen = null;

            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                bool used = i < shown.Count;
                tile.root.style.visibility = used ? Visibility.Visible : Visibility.Hidden;
                if (!used) continue;
                var one = shown[i];
                bool have = Profile.IsFound(one);
                Paint(tile, i, have, Parts.Name(one), Profile.IsFresh(one) || one == justSeen ? "NEW" : null);
                tile.icon.Show(one, have);
            }

            // The data sheet.
            kindLabel.text = Parts.Singular[kind].ToUpperInvariant();
            for (int i = 0; i < kindIcons.Count; i++) kindIcons[i].style.display = i == kind ? DisplayStyle.Flex : DisplayStyle.None;
            sheetGlyph.style.display = DisplayStyle.None;
            if (part == null)
            {
                partName.text = "";
                text.text = "";
            }
            else if (!known)
            {
                partName.text = "NOT FOUND";
                var keeper = Parts.KeptIn(config, part);
                text.text = keeper != null ? $"Kept in the {keeper.displayName}."
                    : part is FrameDef ? "The Smelter builds it." : "";
            }
            else
            {
                partName.text = Parts.Name(part).ToUpperInvariant();
                text.text = Parts.Description(part);
                PartUi.Chips(chips, part);
                Parts.Stats(config, part, stats);
            }
        }

        void RefreshCodex()
        {
            int found = 0, all = 0;
            for (int k = 0; k < cards.Count; k++)
            {
                bool used = k < Codex.Sections;
                if (!used)
                {
                    Card(k, false, "", "", null);
                    continue;
                }
                int have = Codex.Known(config, (Codex.Section)k, out int of, out int owed);
                found += have;
                all += of;
                Card(k, true, Codex.Titles[k], $"{have} / {of}", owed > 0 ? "+" + owed : null);
                cardGlyphs[k].ShowSection((Codex.Section)k);
            }
            total.text = $"{found} / {all}";

            Codex.All(config, (Codex.Section)kind, entries);
            item = Mathf.Clamp(item, 0, Mathf.Max(0, entries.Count - 1));
            var entry = entries.Count > 0 ? entries[item] : null;
            // Looking at an entry is what makes the Archivist pay for it.
            if (entry != null && Codex.Claim(entry) > 0)
            {
                // The sums on the cards have changed with that.
                for (int k = 0; k < Codex.Sections; k++)
                {
                    int have = Codex.Known(config, (Codex.Section)k, out int of, out int owed);
                    Card(k, true, Codex.Titles[k], $"{have} / {of}", owed > 0 ? "+" + owed : null);
                }
            }

            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                bool used = i < entries.Count;
                tile.root.style.visibility = used ? Visibility.Visible : Visibility.Hidden;
                if (!used) continue;
                var one = entries[i];
                bool have = one.Known;
                int owed = one.Owed;
                Paint(tile, i, have, one.name, owed > 0 ? "+" + owed : null);
                tile.glyph.Show(one, have);
            }

            // The data sheet: what it is, and how often each of its steps has happened.
            kindLabel.text = Codex.Singular[kind].ToUpperInvariant();
            foreach (var icon in kindIcons) icon.style.display = DisplayStyle.None;
            sheetGlyph.style.display = DisplayStyle.Flex;
            sheetGlyph.ShowSection((Codex.Section)kind);
            if (entry == null)
            {
                partName.text = "";
                text.text = "";
            }
            else if (!entry.Known)
            {
                partName.text = "NOT FOUND";
                text.text = entry.hint;
            }
            else
            {
                partName.text = entry.name.ToUpperInvariant();
                text.text = entry.text;
                if (entry.section == Codex.Section.Reactions)
                {
                    PartUi.Chip(chips, entry.first.ToString(), Tokens.Of(entry.first), entry.first);
                    PartUi.Chip(chips, entry.second.ToString(), Tokens.Of(entry.second), entry.second);
                }
                else if (entry.first != StateType.None) PartUi.Chip(chips, entry.first.ToString(), Tokens.Of(entry.first), entry.first);
                foreach (var step in entry.steps)
                    stats.Add(new PartStat
                    {
                        label = step.label, share = -1f,
                        value = Profile.Knows(step.key) ? Profile.Count(step.key).ToString() : "NOT YET",
                    });
            }
        }
    }
}
