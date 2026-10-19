using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pulse
{
    // A panel with the top-left and bottom-right corners cut, as set in gui-style.md.
    //
    // A plate is a piece of steel with a thickness, lit from the top left. Its outline is lighter
    // along the top and the left and darker along the bottom and the right, and under it its side
    // shows: one plain dark band of the same shape, a few pixels lower. The band has no outline of
    // its own and the plate throws no shadow, so a plate has one edge and not two. How thick a plate
    // is comes from the style sheet (--plate-depth). The side is drawn below the element, in the gap
    // to whatever comes next, so giving a plate a thickness moves nothing. For the same reason a
    // plate must not clip (overflow: hidden): that would cut its own side off. What has to stay
    // inside a plate clips itself.
    public class Plate : VisualElement
    {
        static readonly CustomStyleProperty<float> DepthStyle = new CustomStyleProperty<float>("--plate-depth");
        // How far a raised plate stands above the others.
        const float Lift = 2f;
        static readonly Color Dark = new Color(0.03f, 0.035f, 0.045f);

        Color fill = Tokens.Plate, border = Tokens.Seam;
        float cut = 12f, depth;
        bool raised, sunk;

        public Color Fill { get => fill; set { fill = value; MarkDirtyRepaint(); } }
        public Color Border { get => border; set { border = value; MarkDirtyRepaint(); } }
        public float Cut { get => cut; set { cut = value; MarkDirtyRepaint(); } }

        // The one in use among several stands a little higher than the rest.
        public bool Raised
        {
            get => raised;
            set
            {
                if (raised == value) return;
                raised = value;
                if (value) style.translate = new Translate(0f, -Lift);
                else style.translate = StyleKeyword.Null;
                MarkDirtyRepaint();
            }
        }

        // Let into the surface instead of standing on it: for what is not there to be used.
        public bool Sunk
        {
            get => sunk;
            set
            {
                if (sunk == value) return;
                sunk = value;
                MarkDirtyRepaint();
            }
        }

        public Plate()
        {
            AddToClassList("plate");
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(Styled);
        }

        void Styled(CustomStyleResolvedEvent resolved)
        {
            resolved.customStyle.TryGetValue(DepthStyle, out float newDepth);
            if (Mathf.Approximately(newDepth, depth)) return;
            depth = newDepth;
            MarkDirtyRepaint();
        }

        // A colour in the shade, and one that catches the light.
        public static Color Shade(Color color, float by) => Keep(Color.Lerp(color, Dark, by), color.a);

        public static Color Lit(Color color, float by) => Keep(Color.Lerp(color, Color.white, by), color.a);

        static Color Keep(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        void Draw(MeshGenerationContext context)
        {
            // Inset by half the outline so the 2 px stroke stays inside the element.
            const float inset = 1f;
            float w = layout.width - inset, h = layout.height - inset;
            if (w <= cut || h <= cut) return;

            var painter = context.painter2D;
            painter.lineJoin = LineJoin.Miter;
            painter.lineCap = LineCap.Butt;
            painter.lineWidth = 2f;

            // A raised plate shows more of its side: the face went up, and the foot stayed.
            float under = sunk ? 0f : depth + (raised && depth > 0f ? Lift : 0f);
            if (under > 0f)
            {
                // As large as the face with its outline, and a little of the outline's colour in it,
                // so the side of the highlighted plate is its own.
                Outline(painter, 0f, under, layout.width, layout.height + under, cut + 0.4f);
                painter.fillColor = Shade(Color.Lerp(fill, border, 0.35f), 0.6f);
                painter.Fill();
            }

            Outline(painter, inset, inset, w, h, cut);
            painter.fillColor = fill;
            painter.Fill();
            // The whole outline in the tone of the side away from the light, and over it the side
            // that faces the light. On a sunk plate it is the other way round.
            Color toLight = Lit(border, 0.22f), away = Shade(border, 0.32f);
            painter.strokeColor = sunk ? toLight : away;
            painter.Stroke();
            // It runs one pixel past both ends, so the corners there are closed.
            painter.strokeColor = sunk ? away : toLight;
            painter.BeginPath();
            painter.MoveTo(new Vector2(inset, h + 1f));
            painter.LineTo(new Vector2(inset, inset + cut));
            painter.LineTo(new Vector2(inset + cut, inset));
            painter.LineTo(new Vector2(w + 1f, inset));
            painter.Stroke();
        }

        // The cut-corner shape between two corners, as a closed path.
        public static void Outline(Painter2D painter, float left, float top, float right, float bottom, float cut)
        {
            painter.BeginPath();
            painter.MoveTo(new Vector2(left + cut, top));
            painter.LineTo(new Vector2(right, top));
            painter.LineTo(new Vector2(right, bottom - cut));
            painter.LineTo(new Vector2(right - cut, bottom));
            painter.LineTo(new Vector2(left, bottom));
            painter.LineTo(new Vector2(left, top + cut));
            painter.ClosePath();
        }
    }

    // A plate that can be pressed. It stands on a flat, darker slab: pressing it pushes the face
    // down onto the slab, which is what makes it look like a button. Primary buttons are filled with
    // Pulse green; one per screen.
    public sealed class PlateButton : VisualElement
    {
        // How far the face stands above its slab, and how long a press from the keyboard is shown.
        const float Depth = 6f;
        const int PressMs = 110;

        readonly Plate face;
        readonly Hatch hatch;
        readonly Action clicked;
        readonly bool primary;
        readonly Color slab;
        bool focused, hovered, usable = true;
        float glow;

        public Label Label { get; }

        public PlateButton(string text, Action clicked, bool primary = false)
        {
            this.clicked = clicked;
            this.primary = primary;
            slab = primary ? Color.Lerp(Tokens.PulseGreen, Tokens.Soot, 0.6f) : Tokens.Soot;
            focusable = true;
            AddToClassList("button");
            if (primary) AddToClassList("button--primary");

            face = new Plate { Cut = 6f };
            face.AddToClassList("button__face");
            Add(face);
            hatch = new Hatch();
            hatch.AddToClassList("button__hatch");
            hatch.style.display = DisplayStyle.None;
            face.Add(hatch);
            Label = new Label(text.ToUpperInvariant());
            Label.AddToClassList("button__label");
            face.Add(Label);
            Paint();

            generateVisualContent += DrawSlab;
            face.RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
            RegisterCallback<MouseEnterEvent>(_ => { hovered = true; Paint(); });
            RegisterCallback<MouseLeaveEvent>(_ => { hovered = false; Paint(); });
            this.AddManipulator(new Clickable(() => this.clicked?.Invoke()));
        }

        // Set by screens that are steered with the keyboard or a controller. Looks the same as hovering.
        public bool Focused
        {
            get => focused;
            set { focused = value; Paint(); }
        }

        // A button that cannot be used right now is hatched and grey, and does not react.
        public bool Usable
        {
            get => usable;
            set
            {
                if (usable == value) return;
                usable = value;
                SetEnabled(value);
                hatch.style.display = value ? DisplayStyle.None : DisplayStyle.Flex;
                EnableInClassList("button--off", !value);
                face.Sunk = !value;
                Paint();
                MarkDirtyRepaint();
            }
        }

        // From 0 to 1: lets a screen make its primary button pulse, to say "this is the way on".
        public float Glow
        {
            set { glow = value; Paint(); }
        }

        // A press without the mouse: the face goes down, comes back up, and then the button acts.
        public void Press()
        {
            if (!usable || ClassListContains("button--down")) return;
            AddToClassList("button--down");
            schedule.Execute(() =>
            {
                RemoveFromClassList("button--down");
                clicked?.Invoke();
            }).StartingIn(PressMs);
        }

        void Paint()
        {
            bool lit = focused || hovered;
            if (!usable)
            {
                face.Fill = Tokens.Plate;
                face.Border = Tokens.Seam;
            }
            else if (primary)
            {
                face.Fill = Color.Lerp(Tokens.PulseGreen, Color.white, lit ? 0.25f : 0.3f * glow);
                face.Border = lit ? Tokens.Chalk : Tokens.PulseGreen;
            }
            else
            {
                face.Fill = lit ? Tokens.Plate2 : Tokens.Plate;
                face.Border = lit ? Tokens.PulseGreen : Tokens.Seam;
            }
        }

        // The slab has the shape of the face and sits a few pixels lower. It is one flat shape: no
        // blur, no gradient and no outline of its own.
        void DrawSlab(MeshGenerationContext context)
        {
            var rect = face.layout;
            if (rect.width <= 12f || rect.height <= 12f) return;
            var painter = context.painter2D;
            Plate.Outline(painter, rect.xMin, rect.yMin + Depth, rect.xMax, rect.yMax + Depth, 6.4f);
            painter.fillColor = usable ? slab : Plate.Shade(Tokens.Plate, 0.6f);
            painter.Fill();
        }
    }

    // Diagonal stripes of the two plate greys: the pattern gui-style.md reserves for warnings.
    // The stripes run past the element's own edges; its style cuts them off there.
    public sealed class Hatch : VisualElement
    {
        const float Stripe = 22f, Gap = 22f;

        public Hatch()
        {
            AddToClassList("hatch");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            float w = layout.width, h = layout.height;
            if (w <= 0f || h <= 0f) return;
            var painter = context.painter2D;
            painter.fillColor = Tokens.Plate2;
            for (float x = -h; x < w; x += Stripe + Gap)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, h));
                painter.LineTo(new Vector2(x + h, 0f));
                painter.LineTo(new Vector2(x + h + Stripe, 0f));
                painter.LineTo(new Vector2(x + Stripe, h));
                painter.ClosePath();
                painter.Fill();
            }
        }
    }
}
