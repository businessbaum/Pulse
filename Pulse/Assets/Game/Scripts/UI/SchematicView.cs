using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pulse
{
    // The wiring diagram of a machine. Slot shapes and wire colours follow gui-style.md, section 5.
    public sealed class SchematicView : VisualElement
    {
        // How large a slot is drawn, in pixels. Bigger on full screens than in the HUD.
        public float SlotSize = 22f;

        static readonly Vector2[] Triangle = { new Vector2(0f, -1f), new Vector2(0.9f, 0.8f), new Vector2(-0.9f, 0.8f) };
        static readonly Vector2[] Bolt =
        {
            new Vector2(0.2f, -1f), new Vector2(-0.6f, 0.15f), new Vector2(-0.05f, 0.15f),
            new Vector2(-0.2f, 1f), new Vector2(0.6f, -0.2f), new Vector2(0.05f, -0.2f),
        };
        static readonly Vector2[] Hexagon =
        {
            new Vector2(0f, -1f), new Vector2(0.87f, -0.5f), new Vector2(0.87f, 0.5f),
            new Vector2(0f, 1f), new Vector2(-0.87f, 0.5f), new Vector2(-0.87f, -0.5f),
        };
        static readonly Vector2[] Drop =
        {
            new Vector2(0f, -1f), new Vector2(0.6f, 0.1f), new Vector2(0.65f, 0.45f), new Vector2(0.4f, 0.85f),
            new Vector2(0f, 1f), new Vector2(-0.4f, 0.85f), new Vector2(-0.65f, 0.45f), new Vector2(-0.6f, 0.1f),
        };
        static readonly Vector2[] Horseshoe =
        {
            new Vector2(-0.8f, -0.9f), new Vector2(-0.3f, -0.9f), new Vector2(-0.3f, 0.2f), new Vector2(0f, 0.5f),
            new Vector2(0.3f, 0.2f), new Vector2(0.3f, -0.9f), new Vector2(0.8f, -0.9f), new Vector2(0.8f, 0.3f),
            new Vector2(0.45f, 0.85f), new Vector2(0f, 1f), new Vector2(-0.45f, 0.85f), new Vector2(-0.8f, 0.3f),
        };

        Loadout loadout;
        string selected;

        public Loadout Loadout
        {
            set { loadout = value; MarkDirtyRepaint(); }
        }

        // The slot that is being worked on, by its id. It is outlined in Pulse green.
        public string Selected
        {
            set { selected = value; MarkDirtyRepaint(); }
        }

        // Set this to let the player click slots. It is called with the id of the slot that was clicked.
        public Action<string> Picked;

        public SchematicView()
        {
            generateVisualContent += Draw;
            RegisterCallback<PointerDownEvent>(Pick);
        }

        // Where the diagram lies inside this element: how much it is scaled, and where its corner is.
        void Fit(out float scale, out Vector2 origin)
        {
            var frame = loadout.frame;
            scale = Mathf.Min(layout.width / frame.schematicSize.x, layout.height / frame.schematicSize.y);
            origin = (new Vector2(layout.width, layout.height) - frame.schematicSize * scale) * 0.5f;
        }

        void Pick(PointerDownEvent click)
        {
            if (Picked == null || loadout == null || layout.width <= 0f) return;
            Fit(out float scale, out Vector2 origin);
            Vector2 at = click.localPosition;
            string best = null;
            float nearest = SlotSize * SlotSize;
            foreach (var slot in loadout.frame.slots)
            {
                Vector2 away = at - (origin + slot.schematicPos * scale);
                // The drive is drawn wide, so it is also wide to click.
                if (slot.type == SlotType.Drive) away.x *= 0.55f;
                if (away.sqrMagnitude >= nearest) continue;
                nearest = away.sqrMagnitude;
                best = slot.id;
            }
            if (best != null) Picked(best);
        }

        void Draw(MeshGenerationContext context)
        {
            if (loadout == null || layout.width <= 0f) return;
            var frame = loadout.frame;
            Fit(out float scale, out Vector2 origin);
            var painter = context.painter2D;

            foreach (var wire in frame.wires)
            {
                painter.strokeColor = Wiring.IsLive(loadout, wire) ? Tokens.PulseGreen : Tokens.Seam;
                painter.lineWidth = 3f;
                painter.BeginPath();
                painter.MoveTo(origin + Find(frame, wire.system).schematicPos * scale);
                painter.LineTo(origin + Find(frame, wire.slot).schematicPos * scale);
                painter.Stroke();
            }

            float half = SlotSize * 0.5f;
            foreach (var slot in frame.slots)
            {
                Vector2 center = origin + slot.schematicPos * scale;
                var module = slot.type == SlotType.Drive ? null : loadout.In(slot.id);
                bool filled = module != null || slot.type == SlotType.Drive;

                bool chosen = slot.id == selected;
                painter.fillColor = chosen ? Tokens.Plate2 : Tokens.Plate;
                painter.strokeColor = chosen ? Tokens.PulseGreen : filled ? Tokens.Chalk : Tokens.Seam;
                painter.lineWidth = chosen ? 3f : 2f;
                painter.BeginPath();
                switch (slot.type)
                {
                    case SlotType.System:
                        painter.Arc(center, half, 0f, 360f);
                        break;
                    case SlotType.Armor:
                        Polygon(painter, center, half * 1.25f, Diamond);
                        break;
                    case SlotType.Drive:
                        Box(painter, center, half * 1.8f, half * 0.55f);
                        break;
                    case SlotType.Socket:
                        Polygon(painter, center, half * 1.15f, Octagon);
                        break;
                    default:
                        Box(painter, center, half, half);
                        break;
                }
                painter.ClosePath();
                painter.Fill();
                painter.Stroke();

                if (module != null) Glyph(painter, module, Wiring.Resolve(loadout, slot.id), center, half * 0.52f);
            }
        }

        // A socket takes what a bay takes and what a support slot takes: its shape lies between a circle and a diamond.
        internal static readonly Vector2[] Octagon =
        {
            new Vector2(-0.42f, -1f), new Vector2(0.42f, -1f), new Vector2(1f, -0.42f), new Vector2(1f, 0.42f),
            new Vector2(0.42f, 1f), new Vector2(-0.42f, 1f), new Vector2(-1f, 0.42f), new Vector2(-1f, -0.42f),
        };

        internal static readonly Vector2[] Diamond = { new Vector2(0f, -1f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(-1f, 0f) };

        // Weapons and triggers show their state as a filled glyph, feeds as an outline.
        static void Glyph(Painter2D painter, ModuleDef module, SlotMods mods, Vector2 center, float size)
        {
            if (module is SystemDef system)
            {
                painter.strokeColor = system.kind == SystemKind.Feed ? Tokens.Of(system.feedState) : Tokens.Chalk;
                painter.lineWidth = 2f;
                painter.BeginPath();
                if (system.kind == SystemKind.Feed) Polygon(painter, center, size, Shape(system.feedState));
                else painter.Arc(center, size * 0.8f, 0f, 360f);
                painter.ClosePath();
                painter.Stroke();
                return;
            }

            var state = Wiring.EffectiveState(module, mods);
            painter.fillColor = Tokens.Of(state);
            painter.BeginPath();
            if (state == StateType.None) painter.Arc(center, size * 0.6f, 0f, 360f);
            else Polygon(painter, center, size, Shape(state));
            painter.ClosePath();
            painter.Fill();
        }

        internal static Vector2[] Shape(StateType state) => state switch
        {
            StateType.Heat => Triangle,
            StateType.Shock => Bolt,
            StateType.Cryo => Hexagon,
            StateType.Oil => Drop,
            StateType.Magnet => Horseshoe,
            _ => Diamond,
        };

        internal static void Polygon(Painter2D painter, Vector2 center, float size, Vector2[] points)
        {
            painter.MoveTo(center + points[0] * size);
            for (int i = 1; i < points.Length; i++) painter.LineTo(center + points[i] * size);
        }

        static void Box(Painter2D painter, Vector2 center, float halfWidth, float halfHeight)
        {
            painter.MoveTo(center + new Vector2(-halfWidth, -halfHeight));
            painter.LineTo(center + new Vector2(halfWidth, -halfHeight));
            painter.LineTo(center + new Vector2(halfWidth, halfHeight));
            painter.LineTo(center + new Vector2(-halfWidth, halfHeight));
        }

        static SlotDef Find(FrameDef frame, string id)
        {
            foreach (var slot in frame.slots)
                if (slot.id == id) return slot;
            return default;
        }
    }
}
