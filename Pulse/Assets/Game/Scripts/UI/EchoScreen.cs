using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pulse
{
    // The choice of an Echo. The run stands still, up to three cards lie side by side, and one of
    // them is taken: with left and right and the confirm key, or with a click. There is no way to
    // take none.
    public sealed class EchoScreen
    {
        sealed class Card
        {
            public Plate root;
            public EchoIcon icon;
            public Label name, text;
            public VisualElement chips;
        }

        // A key that was already being hammered when the cards came up must not pick one.
        const float Guard = 0.35f;

        readonly VisualElement overlay;
        readonly List<Card> cards = new List<Card>();
        List<EchoDef> offer = new List<EchoDef>();
        Action<EchoDef> taken;
        float openedAt;
        int focus;

        public bool IsOpen { get; private set; }
        // What is on the cards right now.
        public IReadOnlyList<EchoDef> Offered => offer;

        public EchoScreen(VisualElement root)
        {
            overlay = Ui.Add(root, new VisualElement(), "overlay");
            var box = Ui.Add(overlay, new VisualElement(), "echo-pick");
            Ui.Add(box, new Label("ECHO"), "display");
            var row = Ui.Add(box, new VisualElement(), "echo-pick__row");
            for (int i = 0; i < Echoes.Choices; i++)
            {
                int index = i;
                var card = new Card { root = Ui.Add(row, new Plate { Cut = 12f }, "echo-card") };
                card.icon = Ui.Add(card.root, new EchoIcon(), "echo-card__icon");
                card.name = Ui.Add(card.root, new Label(), "title");
                card.name.AddToClassList("echo-card__name");
                card.chips = Ui.Add(card.root, new VisualElement(), "echo-card__chips");
                card.text = Ui.Add(card.root, new Label(), "echo-card__text");
                card.root.RegisterCallback<MouseEnterEvent>(_ => Focus(index));
                card.root.AddManipulator(new Clickable(() => Pick(index)));
                cards.Add(card);
            }
        }

        // Shows the cards and stops the run. whenTaken is told which Echo was chosen.
        public void Open(List<EchoDef> offered, Action<EchoDef> whenTaken)
        {
            if (offered == null || offered.Count == 0) return;
            offer = offered;
            taken = whenTaken;
            IsOpen = true;
            openedAt = Time.unscaledTime;
            Time.timeScale = 0f;
            overlay.style.display = DisplayStyle.Flex;
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                bool used = i < offer.Count;
                card.root.style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
                if (!used) continue;
                var echo = offer[i];
                card.icon.Show(echo);
                card.name.text = echo.displayName.ToUpperInvariant();
                card.text.text = echo.text;
                card.chips.Clear();
                // A fragment of a strand says which.
                if (echo.strand != StateType.None) PartUi.Chip(card.chips, echo.strand.ToString(), Tokens.Of(echo.strand), echo.strand);
            }
            Focus(offer.Count / 2);
        }

        void Focus(int index)
        {
            focus = Mathf.Clamp(index, 0, Mathf.Max(0, offer.Count - 1));
            for (int i = 0; i < cards.Count; i++)
            {
                bool lit = i == focus;
                cards[i].root.Border = lit ? Tokens.PulseGreen : Tokens.Seam;
                cards[i].root.Fill = lit ? Tokens.Plate2 : Tokens.Plate;
                cards[i].root.Raised = lit;
            }
        }

        // Called every frame while the cards are up.
        public void Update()
        {
            if (!IsOpen) return;
            var nav = Nav.Read();
            if (nav.left) Focus(focus - 1);
            else if (nav.right) Focus(focus + 1);
            else if (nav.confirm && Time.unscaledTime - openedAt > Guard) Pick(focus);
        }

        // Takes one of the cards, as a key or a click would. Also what a test calls.
        public void Pick(int index)
        {
            if (!IsOpen || index < 0 || index >= offer.Count) return;
            var echo = offer[index];
            IsOpen = false;
            overlay.style.display = DisplayStyle.None;
            Time.timeScale = 1f;
            taken?.Invoke(echo);
        }
    }

    // The sign of an Echo. A fragment of a strand is that strand's shape in a ring, in its colour.
    // The others each have a sign of their own, drawn in chalk.
    public sealed class EchoIcon : VisualElement
    {
        EchoDef echo;
        bool known = true;

        public EchoIcon()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        // found: whether the player has carried it. One that was never carried is an empty ring.
        public void Show(EchoDef shown, bool found = true)
        {
            echo = shown;
            known = found;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            if (echo == null || layout.width <= 0f) return;
            Vector2 center = new Vector2(layout.width, layout.height) * 0.5f;
            Paint(context.painter2D, center, Mathf.Min(layout.width, layout.height) * 0.42f, echo, known);
        }

        public static void Paint(Painter2D painter, Vector2 c, float r, EchoDef echo, bool known)
        {
            Color ink = !known ? Tokens.Seam : echo.strand != StateType.None ? Tokens.Of(echo.strand) : Tokens.Chalk;
            painter.strokeColor = ink;
            painter.fillColor = ink;
            painter.lineWidth = Mathf.Max(2f, r * 0.14f);
            painter.lineCap = LineCap.Round;
            if (!known)
            {
                Arc(painter, c, r * 0.8f, 0f, 360f);
                return;
            }
            switch (echo.kind)
            {
                case EchoKind.SecondBeat:
                    // A beat, and the same beat again further out.
                    Vector2 source = c + new Vector2(-r * 0.6f, 0f);
                    Dot(painter, source, r * 0.18f);
                    Arc(painter, source, r * 0.65f, -55f, 55f);
                    Arc(painter, source, r * 1.25f, -45f, 45f);
                    break;

                case EchoKind.Undertow:
                    // A horseshoe magnet, open to the top.
                    Arc(painter, c + new Vector2(0f, r * 0.15f), r * 0.6f, 0f, 180f);
                    Line(painter, c + new Vector2(-r * 0.6f, r * 0.15f), c + new Vector2(-r * 0.6f, -r * 0.8f));
                    Line(painter, c + new Vector2(r * 0.6f, r * 0.15f), c + new Vector2(r * 0.6f, -r * 0.8f));
                    break;

                case EchoKind.Crescendo:
                    // The sign for growing louder.
                    Line(painter, c + new Vector2(-r, 0f), c + new Vector2(r, -r * 0.65f));
                    Line(painter, c + new Vector2(-r, 0f), c + new Vector2(r, r * 0.65f));
                    break;

                case EchoKind.Afterburn:
                    // Two jets.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector2 tip = c + new Vector2(r * 0.9f, side * r * 0.42f);
                        Line(painter, tip, tip + new Vector2(-r * 1.1f, -r * 0.3f));
                        Line(painter, tip, tip + new Vector2(-r * 1.1f, r * 0.3f));
                        Line(painter, tip + new Vector2(-r * 0.75f, 0f), tip + new Vector2(-r * 1.8f, 0f));
                    }
                    break;

                case EchoKind.DoubleTime:
                    // Two notes under one beam.
                    Dot(painter, c + new Vector2(-r * 0.6f, r * 0.62f), r * 0.24f);
                    Dot(painter, c + new Vector2(r * 0.4f, r * 0.42f), r * 0.24f);
                    Line(painter, c + new Vector2(-r * 0.38f, r * 0.62f), c + new Vector2(-r * 0.38f, -r * 0.7f));
                    Line(painter, c + new Vector2(r * 0.62f, r * 0.42f), c + new Vector2(r * 0.62f, -r * 0.9f));
                    Line(painter, c + new Vector2(-r * 0.38f, -r * 0.7f), c + new Vector2(r * 0.62f, -r * 0.9f));
                    break;

                case EchoKind.Encore:
                    // Once more: a ring that comes round on itself, with an arrow head.
                    Arc(painter, c, r * 0.78f, 50f, 330f);
                    Vector2 head = c + new Vector2(Mathf.Cos(-30f * Mathf.Deg2Rad), Mathf.Sin(-30f * Mathf.Deg2Rad)) * (r * 0.78f);
                    Line(painter, head, head + new Vector2(-r * 0.5f, -r * 0.05f));
                    Line(painter, head, head + new Vector2(r * 0.12f, -r * 0.5f));
                    break;

                case EchoKind.HeavyPurse:
                    // Two Beats, one in front of the other.
                    Fill(painter, c + new Vector2(r * 0.42f, -r * 0.22f), r * 0.58f, SchematicView.Diamond);
                    // The one in front is cut out of the one behind it, so they stay two.
                    painter.fillColor = Tokens.Plate;
                    Fill(painter, c + new Vector2(-r * 0.3f, r * 0.2f), r * 0.86f, SchematicView.Diamond);
                    painter.fillColor = ink;
                    Fill(painter, c + new Vector2(-r * 0.3f, r * 0.2f), r * 0.7f, SchematicView.Diamond);
                    break;

                case EchoKind.LongWire:
                    // Two nodes far apart, and the wire between them.
                    Dot(painter, c + new Vector2(-r * 0.8f, r * 0.55f), r * 0.22f);
                    Dot(painter, c + new Vector2(r * 0.8f, -r * 0.55f), r * 0.22f);
                    Line(painter, c + new Vector2(-r * 0.8f, r * 0.55f), c + new Vector2(r * 0.8f, -r * 0.55f));
                    break;

                default:
                    Fill(painter, c, r * 0.5f, SchematicView.Shape(echo.strand));
                    Arc(painter, c, r * 0.95f, 0f, 360f);
                    break;
            }
        }

        static void Arc(Painter2D painter, Vector2 center, float radius, float from, float to)
        {
            painter.BeginPath();
            painter.Arc(center, radius, from, to);
            painter.Stroke();
        }

        static void Line(Painter2D painter, Vector2 from, Vector2 to)
        {
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);
            painter.Stroke();
        }

        static void Dot(Painter2D painter, Vector2 center, float radius)
        {
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.ClosePath();
            painter.Fill();
        }

        static void Fill(Painter2D painter, Vector2 center, float size, Vector2[] points)
        {
            painter.BeginPath();
            SchematicView.Polygon(painter, center, size, points);
            painter.ClosePath();
            painter.Fill();
        }
    }

    // The picture of an entry of the Codex: the two strands of a reaction side by side, the head of
    // a Stray with eyes in the colour of where it lives, or the sign of an Echo. What is not known
    // yet is an outline.
    public sealed class CodexIcon : VisualElement
    {
        static readonly Vector2[] Head =
        {
            new Vector2(-0.7f, -0.8f), new Vector2(0.7f, -0.8f), new Vector2(1f, -0.3f), new Vector2(1f, 0.45f),
            new Vector2(0.55f, 0.8f), new Vector2(-0.55f, 0.8f), new Vector2(-1f, 0.45f), new Vector2(-1f, -0.3f),
        };

        Codex.Entry entry;
        Codex.Section section;
        bool known, emblem;

        public CodexIcon()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Show(Codex.Entry shown, bool found)
        {
            entry = shown;
            section = shown != null ? shown.section : section;
            known = found;
            emblem = false;
            MarkDirtyRepaint();
        }

        // The sign of a whole section, for its card.
        public void ShowSection(Codex.Section which)
        {
            section = which;
            emblem = true;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            if (layout.width <= 0f || (!emblem && entry == null)) return;
            var painter = context.painter2D;
            Vector2 c = new Vector2(layout.width, layout.height) * 0.5f;
            float r = Mathf.Min(layout.width, layout.height) * 0.42f;
            painter.lineWidth = Mathf.Max(2f, r * 0.12f);
            bool lit = emblem || known;

            switch (section)
            {
                case Codex.Section.Reactions:
                    var first = emblem ? StateType.Heat : entry.first;
                    var second = emblem ? StateType.Oil : entry.second;
                    Strand(painter, c + new Vector2(-r * 0.42f, -r * 0.12f), r * 0.6f, first, lit, emblem);
                    Strand(painter, c + new Vector2(r * 0.42f, r * 0.12f), r * 0.6f, second, lit, emblem);
                    break;

                case Codex.Section.Strays:
                    Color ink = lit ? Tokens.Chalk : Tokens.Seam;
                    painter.strokeColor = ink;
                    painter.fillColor = Tokens.Plate;
                    painter.BeginPath();
                    SchematicView.Polygon(painter, c + new Vector2(0f, -r * 0.12f), r * 0.82f, Head);
                    painter.ClosePath();
                    painter.Fill();
                    painter.Stroke();
                    // Two legs, and two eyes that glow in the colour of its home.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        painter.BeginPath();
                        painter.MoveTo(c + new Vector2(side * r * 0.4f, r * 0.55f));
                        painter.LineTo(c + new Vector2(side * r * 0.55f, r));
                        painter.Stroke();
                        if (!lit) continue;
                        painter.fillColor = emblem || entry.first == StateType.None ? Tokens.Alert : Tokens.Of(entry.first);
                        painter.BeginPath();
                        painter.Arc(c + new Vector2(side * r * 0.36f, -r * 0.18f), r * 0.16f, 0f, 360f);
                        painter.ClosePath();
                        painter.Fill();
                    }
                    break;

                default:
                    if (emblem || entry.echo == null)
                    {
                        // A beat and its echo.
                        painter.strokeColor = painter.fillColor = Tokens.Chalk;
                        Vector2 source = c + new Vector2(-r * 0.6f, 0f);
                        painter.BeginPath();
                        painter.Arc(source, r * 0.18f, 0f, 360f);
                        painter.ClosePath();
                        painter.Fill();
                        foreach (float reach in new[] { 0.65f, 1.25f })
                        {
                            painter.BeginPath();
                            painter.Arc(source, r * reach, -50f, 50f);
                            painter.Stroke();
                        }
                    }
                    else EchoIcon.Paint(painter, c, r, entry.echo, known);
                    break;
            }
        }

        // One strand of a reaction: its shape, filled when the reaction is known.
        static void Strand(Painter2D painter, Vector2 center, float size, StateType state, bool lit, bool outline)
        {
            painter.BeginPath();
            if (state == StateType.None) painter.Arc(center, size * 0.7f, 0f, 360f);
            else SchematicView.Polygon(painter, center, size, SchematicView.Shape(state));
            painter.ClosePath();
            painter.fillColor = lit && !outline ? Tokens.Of(state) : Tokens.Plate;
            painter.strokeColor = lit ? (outline ? Tokens.Chalk : Plate.Shade(Tokens.Of(state), 0.45f)) : Tokens.Seam;
            painter.Fill();
            painter.Stroke();
        }
    }
}
