using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Pulse
{
    // What the player asked for this frame on a full screen, from the keyboard or a controller.
    public struct Nav
    {
        public bool up, down, left, right, confirm, back, save, take, remove;

        static bool heldX, heldY;

        public static Nav Read()
        {
            var keys = Keyboard.current;
            var pad = Gamepad.current;
            Vector2 stick = pad != null ? pad.leftStick.ReadValue() : Vector2.zero;
            // A stick counts once each time it is pushed over, not for as long as it is held.
            bool stickUp = stick.y > 0.6f && !heldY, stickDown = stick.y < -0.6f && !heldY;
            bool stickLeft = stick.x < -0.6f && !heldX, stickRight = stick.x > 0.6f && !heldX;
            heldY = Mathf.Abs(stick.y) > 0.4f;
            heldX = Mathf.Abs(stick.x) > 0.4f;

            return new Nav
            {
                up = stickUp || (keys != null && (keys.wKey.wasPressedThisFrame || keys.upArrowKey.wasPressedThisFrame))
                    || (pad != null && pad.dpad.up.wasPressedThisFrame),
                down = stickDown || (keys != null && (keys.sKey.wasPressedThisFrame || keys.downArrowKey.wasPressedThisFrame))
                    || (pad != null && pad.dpad.down.wasPressedThisFrame),
                left = stickLeft || (keys != null && (keys.aKey.wasPressedThisFrame || keys.leftArrowKey.wasPressedThisFrame))
                    || (pad != null && pad.dpad.left.wasPressedThisFrame),
                right = stickRight || (keys != null && (keys.dKey.wasPressedThisFrame || keys.rightArrowKey.wasPressedThisFrame))
                    || (pad != null && pad.dpad.right.wasPressedThisFrame),
                confirm = (keys != null && (keys.eKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame))
                    || (pad != null && pad.buttonSouth.wasPressedThisFrame),
                back = (keys != null && (keys.escapeKey.wasPressedThisFrame || keys.backspaceKey.wasPressedThisFrame))
                    || (pad != null && pad.buttonEast.wasPressedThisFrame),
                save = (keys != null && keys.qKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame),
                take = (keys != null && keys.tKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame),
                remove = (keys != null && keys.deleteKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame),
            };
        }
    }

    // The small pieces every screen about parts is built from.
    public static class PartUi
    {
        public static void Chip(VisualElement row, string text, Color color, StateType? glyph = null)
        {
            var chip = Ui.Add(row, new Plate { Cut = 5f, Border = color }, "chip");
            if (glyph.HasValue) Ui.Add(chip, new StateGlyph(glyph.Value), "glyph");
            Ui.Add(chip, new Label(text.ToUpperInvariant()), "chip__label");
        }

        // The tags of a part: how a weapon hits and with what, what a system does, what a plate adds.
        // state is what the part's hits carry once the machine's feeds are applied; leave it out to
        // show the part by itself.
        public static void Chips(VisualElement row, ScriptableObject part, StateType? state = null)
        {
            row.Clear();
            switch (part)
            {
                case WeaponDef weapon:
                    Chip(row, weapon.delivery.ToString(), Tokens.Seam);
                    var carries = state ?? weapon.state;
                    Chip(row, carries == StateType.None ? "Kinetic" : carries.ToString(), carries == StateType.None ? Tokens.Dust : Tokens.Of(carries), carries);
                    break;
                case SystemDef system when system.kind == SystemKind.Feed:
                    Chip(row, system.feedState + " feed", Tokens.Of(system.feedState), system.feedState);
                    break;
                case SystemDef system:
                    Chip(row, system.kind.ToString(), Tokens.Seam);
                    break;
                case SupportDef support:
                    if (support.armor > 0f) Chip(row, "Armor", Tokens.Seam);
                    if (support.hull > 0f) Chip(row, "Hull", Tokens.Seam);
                    if (support.trigger != TriggerType.None) Chip(row, "Trigger", Tokens.Dust, state ?? StateType.None);
                    break;
                case DriveDef drive:
                    Chip(row, drive.turnsOnSpot ? "Turns on the spot" : "Rolls to turn", Tokens.Seam);
                    break;
                case ReactorDef reactor:
                    Chip(row, "Power " + reactor.powerCap, Tokens.PulseGreen);
                    break;
                case FrameDef frame:
                    Chip(row, "Hull " + Mathf.RoundToInt(frame.HullAt(Profile.MarkOf(frame))), Tokens.Seam);
                    break;
            }
        }
    }

    // The small shape that stands for a state, so a chip never relies on colour alone.
    public sealed class StateGlyph : VisualElement
    {
        readonly StateType state;

        public StateGlyph(StateType state)
        {
            this.state = state;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            Vector2 center = new Vector2(layout.width, layout.height) * 0.5f;
            float size = Mathf.Min(layout.width, layout.height) * 0.5f;
            painter.fillColor = Tokens.Of(state);
            painter.BeginPath();
            if (state == StateType.None) painter.Arc(center, size * 0.6f, 0f, 360f);
            else SchematicView.Polygon(painter, center, size, SchematicView.Shape(state));
            painter.ClosePath();
            painter.Fill();
        }
    }

    // How good the ability of a part is, as a row of five notches of which so many are filled.
    // Rarity is never shown by colour: the state colours and Pulse green all mean something else.
    public sealed class Notches : VisualElement
    {
        const int Shown = 5;

        int filled;

        public Notches()
        {
            AddToClassList("notches");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public Rarity Rarity
        {
            set
            {
                filled = Mathf.Min((int)value, Shown);
                style.visibility = value == Rarity.None ? Visibility.Hidden : Visibility.Visible;
                MarkDirtyRepaint();
            }
        }

        void Draw(MeshGenerationContext context)
        {
            float w = layout.width, h = layout.height;
            if (w <= 0f || h <= 0f) return;
            var painter = context.painter2D;
            float step = w / Shown, wide = step * 0.62f, lean = h * 0.35f;
            for (int i = 0; i < Shown; i++)
            {
                float x = i * step;
                painter.fillColor = i < filled ? Tokens.Chalk : Tokens.Seam;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x + lean, 0f));
                painter.LineTo(new Vector2(x + lean + wide, 0f));
                painter.LineTo(new Vector2(x + wide, h));
                painter.LineTo(new Vector2(x, h));
                painter.ClosePath();
                painter.Fill();
            }
        }
    }

    // The symbol of a kind of part: a crosshair for weapons, a wired node for systems, a shield for
    // armor, a wheel for drives, a cell for reactors and a chassis for frames.
    public sealed class KindIcon : VisualElement
    {
        static readonly Vector2[] Shield =
        {
            new Vector2(-0.8f, -0.85f), new Vector2(0.8f, -0.85f), new Vector2(0.8f, 0.1f), new Vector2(0f, 0.95f), new Vector2(-0.8f, 0.1f),
        };
        static readonly Vector2[] Chassis =
        {
            new Vector2(-0.55f, -0.5f), new Vector2(0.95f, -0.5f), new Vector2(0.95f, 0.1f),
            new Vector2(0.55f, 0.4f), new Vector2(-0.95f, 0.4f), new Vector2(-0.95f, -0.2f),
        };

        PartKind kind;

        // For a symbol in a row that is refilled.
        public PartKind Kind
        {
            set
            {
                if (kind == value) return;
                kind = value;
                MarkDirtyRepaint();
            }
        }

        public KindIcon(PartKind kind)
        {
            this.kind = kind;
            AddToClassList("kind-icon");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            if (layout.width <= 0f) return;
            var painter = context.painter2D;
            Vector2 c = new Vector2(layout.width, layout.height) * 0.5f;
            float r = Mathf.Min(layout.width, layout.height) * 0.46f;
            painter.lineWidth = 2f;
            // Stamped out of the plate: the same lines once more, dark and a little lower, under the light ones.
            Color under = Plate.Shade(Tokens.Plate, 0.75f);
            Paint(painter, c + new Vector2(0f, 2f), r, under, under, false);
            Paint(painter, c, r, Tokens.Chalk, Tokens.PulseGreen, true);
        }

        // ink: the colour of the lines. core: of the Pulse in a reactor. hollow: whether the middle of
        // a node is cleared, which only the upper drawing needs.
        void Paint(Painter2D painter, Vector2 c, float r, Color ink, Color core, bool hollow)
        {
            painter.strokeColor = ink;
            painter.fillColor = ink;
            switch (kind)
            {
                case PartKind.Weapon:
                    // A crosshair: a ring, four ticks that cross it, and a dot.
                    Ring(painter, c, r * 0.62f);
                    for (int i = 0; i < 4; i++)
                    {
                        var dir = new Vector2(i == 0 ? 1f : i == 1 ? -1f : 0f, i == 2 ? 1f : i == 3 ? -1f : 0f);
                        Stroke(painter, c + dir * (r * 0.38f), c + dir * r);
                    }
                    Dot(painter, c, r * 0.12f);
                    break;

                case PartKind.System:
                    // A node with three wires that end in smaller nodes.
                    for (int i = 0; i < 3; i++)
                    {
                        float angle = (-90f + i * 120f) * Mathf.Deg2Rad;
                        var end = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (r * 0.82f);
                        Stroke(painter, c, end);
                        Dot(painter, end, r * 0.17f);
                    }
                    if (hollow)
                    {
                        painter.fillColor = Tokens.Plate;
                        painter.BeginPath();
                        painter.Arc(c, r * 0.34f, 0f, 360f);
                        painter.ClosePath();
                        painter.Fill();
                    }
                    Ring(painter, c, r * 0.34f);
                    break;

                case PartKind.Armor:
                    Outline(painter, c, r, Shield);
                    Stroke(painter, c + new Vector2(0f, -r * 0.5f), c + new Vector2(0f, r * 0.45f));
                    break;

                case PartKind.Drive:
                    // A wheel: tyre, hub and five spokes.
                    Ring(painter, c, r * 0.92f);
                    Ring(painter, c, r * 0.3f);
                    for (int i = 0; i < 5; i++)
                    {
                        float angle = (i * 72f - 90f) * Mathf.Deg2Rad;
                        var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        Stroke(painter, c + dir * (r * 0.3f), c + dir * (r * 0.92f));
                    }
                    break;

                case PartKind.Reactor:
                    // A six-sided cell with the Pulse in it.
                    Outline(painter, c, r, SchematicView.Shape(StateType.Cryo));
                    painter.fillColor = core;
                    Dot(painter, c, r * 0.3f);
                    break;

                default:
                    // A chassis seen from the side, on two wheels.
                    Outline(painter, c + new Vector2(0f, -r * 0.2f), r, Chassis);
                    Ring(painter, c + new Vector2(-r * 0.5f, r * 0.55f), r * 0.26f);
                    Ring(painter, c + new Vector2(r * 0.5f, r * 0.55f), r * 0.26f);
                    break;
            }
        }

        static void Ring(Painter2D painter, Vector2 center, float radius)
        {
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.ClosePath();
            painter.Stroke();
        }

        static void Dot(Painter2D painter, Vector2 center, float radius)
        {
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.ClosePath();
            painter.Fill();
        }

        static void Stroke(Painter2D painter, Vector2 from, Vector2 to)
        {
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);
            painter.Stroke();
        }

        static void Outline(Painter2D painter, Vector2 center, float size, Vector2[] points)
        {
            painter.BeginPath();
            SchematicView.Polygon(painter, center, size, points);
            painter.ClosePath();
            painter.Stroke();
        }
    }

    // A small mark in the corner of the screen that lights for a moment whenever the game has
    // saved. It needs no words, and it is the only sign of saving there is, because there is
    // nothing for the player to do about it.
    public sealed class SaveMark : VisualElement
    {
        const float Shown = 1.1f;

        public SaveMark()
        {
            AddToClassList("save-mark");
            pickingMode = PickingMode.Ignore;
            style.opacity = 0f;
        }

        // Called every frame.
        public void Tick()
        {
            float since = Time.realtimeSinceStartup - Profile.SavedAt;
            style.opacity = since < 0f || since > Shown ? 0f : Mathf.Sin(since / Shown * Mathf.PI);
        }
    }

    // The picture of a part on its tile: the shape of the slot it goes in, as in the wiring diagram,
    // with the state it carries inside. Drives, reactors and frames have shapes of their own.
    public sealed class PartIcon : VisualElement
    {
        static readonly Vector2[] Square = { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f) };
        static readonly Vector2[] Wide = { new Vector2(-1.25f, -0.5f), new Vector2(1.25f, -0.5f), new Vector2(1.25f, 0.5f), new Vector2(-1.25f, 0.5f) };
        static readonly Vector2[] Cut =
        {
            new Vector2(-0.6f, -1f), new Vector2(1.2f, -1f), new Vector2(1.2f, 0.4f),
            new Vector2(0.6f, 1f), new Vector2(-1.2f, 1f), new Vector2(-1.2f, -0.4f),
        };

        ScriptableObject part;
        bool known;

        public PartIcon()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        // known: whether the player has found it. An unknown part shows only an empty outline.
        public void Show(ScriptableObject shown, bool found)
        {
            part = shown;
            known = found;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            if (part == null || layout.width <= 0f) return;
            var painter = context.painter2D;
            float half = Mathf.Min(layout.width, layout.height) * 0.4f;
            // How far the shape stands up from the tile, in pixels. Its side shows below it.
            float rise = Mathf.Max(2f, half * 0.12f);
            Vector2 center = new Vector2(layout.width, layout.height) * 0.5f - new Vector2(0f, rise * 0.5f);

            void Trace(Vector2 at)
            {
                painter.BeginPath();
                switch (part)
                {
                    case SystemDef _: painter.Arc(at, half, 0f, 360f); break;
                    case SupportDef _: SchematicView.Polygon(painter, at, half * 1.2f, SchematicView.Diamond); break;
                    case DriveDef _: SchematicView.Polygon(painter, at, half * 0.9f, Wide); break;
                    case ReactorDef _: SchematicView.Polygon(painter, at, half * 1.1f, SchematicView.Shape(StateType.Cryo)); break;
                    case FrameDef _: SchematicView.Polygon(painter, at, half * 0.85f, Cut); break;
                    default: SchematicView.Polygon(painter, at, half, Square); break;
                }
                painter.ClosePath();
            }

            painter.lineWidth = 2f;
            // A part that is found stands on its tile like a key on a board. One that is not is only an outline.
            if (known)
            {
                Trace(center + new Vector2(0f, rise));
                painter.fillColor = Plate.Shade(Tokens.Plate, 0.6f);
                painter.strokeColor = Plate.Shade(Tokens.Chalk, 0.62f);
                painter.Fill();
                painter.Stroke();
            }
            Trace(center);
            painter.fillColor = Tokens.Plate;
            painter.strokeColor = known ? Tokens.Chalk : Tokens.Seam;
            painter.Fill();
            painter.Stroke();
            if (!known) return;

            float size = half * 0.5f;
            switch (part)
            {
                case SystemDef system:
                    painter.strokeColor = system.kind == SystemKind.Feed ? Tokens.Of(system.feedState) : Tokens.Chalk;
                    painter.BeginPath();
                    if (system.kind == SystemKind.Feed) SchematicView.Polygon(painter, center, size, SchematicView.Shape(system.feedState));
                    else painter.Arc(center, size * 0.8f, 0f, 360f);
                    painter.ClosePath();
                    painter.Stroke();
                    break;
                case ModuleDef module:
                    var state = module.OutputState;
                    painter.fillColor = module.HasOutput ? Tokens.Of(state) : Tokens.Dust;
                    painter.BeginPath();
                    if (state == StateType.None) painter.Arc(center, size * 0.6f, 0f, 360f);
                    else SchematicView.Polygon(painter, center, size, SchematicView.Shape(state));
                    painter.ClosePath();
                    painter.Fill();
                    break;
                case ReactorDef _:
                    // The Pulse itself.
                    painter.fillColor = Tokens.PulseGreen;
                    painter.BeginPath();
                    painter.Arc(center, size * 0.7f, 0f, 360f);
                    painter.ClosePath();
                    painter.Fill();
                    break;
            }
        }
    }
}
