using System.Collections.Generic;
using UnityEngine;

namespace Pulse
{
    // Where the character can walk in the Roundhouse, and what stands in its way.
    //
    // The floor is a set of areas: the hall, which has as many straight walls as the building has
    // wall pieces, and every room behind a door, with the doorway that leads to it. The character
    // may be wherever at least one open area has room for it. Things standing on the floor are
    // circles or boxes that match their footprints, and the character slides around them.
    public sealed class LobbyWalk
    {
        struct Circle
        {
            public Vector2 pos;
            public float radius;
        }

        // A box on the floor, turned any way. right and forward are its own axes.
        struct Box
        {
            public Vector2 pos, right, forward, half;
        }

        sealed class Area
        {
            // A hall: the character is inside when it is behind every wall. Each wall is a direction
            // and how far out along it the wall stands.
            public Vector2[] walls;
            public float[] limits;
            // A room or a doorway.
            public Box box;
            public bool isBox, open = true;
        }

        readonly List<Area> areas = new List<Area>();
        readonly List<Circle> circles = new List<Circle>();
        readonly List<Box> boxes = new List<Box>();

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        // ---- Describing the place ----

        // An area with straight walls all round. Each wall is given as the direction it lies in,
        // seen from the middle, and its distance.
        public void AddHall(IList<Vector3> directions, float distance)
        {
            var area = new Area { walls = new Vector2[directions.Count], limits = new float[directions.Count] };
            for (int i = 0; i < directions.Count; i++)
            {
                area.walls[i] = Flat(directions[i]).normalized;
                area.limits[i] = distance;
            }
            areas.Add(area);
        }

        // A rectangular area, such as a room or the doorway into it. half: half its width and half
        // its depth. Returns its number, for Open.
        public int AddRoom(Vector3 center, Quaternion rotation, Vector2 half, bool open)
        {
            areas.Add(new Area { isBox = true, open = open, box = Make(center, rotation, half, Vector2.zero) });
            return areas.Count - 1;
        }

        public void Open(int area, bool open) => areas[area].open = open;

        public void Block(Vector3 pos, float radius) => circles.Add(new Circle { pos = Flat(pos), radius = radius });

        // A box that stands where an object stands and turns with it. offset moves it in the
        // object's own directions: x to its right, y to its front.
        public void Block(Vector3 pos, Quaternion rotation, Vector2 half, Vector2 offset = default) =>
            boxes.Add(Make(pos, rotation, half, offset));

        static Box Make(Vector3 pos, Quaternion rotation, Vector2 half, Vector2 offset)
        {
            Vector2 right = Flat(rotation * Vector3.right).normalized, forward = Flat(rotation * Vector3.forward).normalized;
            return new Box { pos = Flat(pos) + right * offset.x + forward * offset.y, right = right, forward = forward, half = half };
        }

        // ---- Walking ----

        // Where a character of that size ends up when it wants to stand at a point.
        public Vector3 Resolve(Vector3 wanted, float size)
        {
            Vector2 p = Flat(wanted);
            // Twice: being pushed out of one thing can push into another.
            for (int pass = 0; pass < 2; pass++)
            {
                p = Inside(p, size);
                foreach (var circle in circles)
                {
                    Vector2 away = p - circle.pos;
                    float reach = circle.radius + size, dist = away.magnitude;
                    if (dist < reach && dist > 0.0001f) p += away / dist * (reach - dist);
                }
                foreach (var box in boxes) p = PushOut(p, box, size);
            }
            p = Inside(p, size);
            return new Vector3(p.x, 0f, p.y);
        }

        // The point itself if an open area has room for the character there. Otherwise the nearest
        // point at which one has.
        Vector2 Inside(Vector2 p, float size)
        {
            Vector2 best = p;
            float nearest = float.MaxValue;
            foreach (var area in areas)
            {
                if (!area.open) continue;
                Vector2 kept = area.isBox ? KeepIn(p, area.box, size) : KeepBehind(p, area, size);
                float moved = (kept - p).sqrMagnitude;
                if (moved < 0.000001f) return p;
                if (moved >= nearest) continue;
                nearest = moved;
                best = kept;
            }
            return best;
        }

        static Vector2 KeepBehind(Vector2 p, Area hall, float size)
        {
            // Corners need a second go: stepping back from one wall can step through its neighbour.
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < hall.walls.Length; i++)
                {
                    float past = Vector2.Dot(p, hall.walls[i]) - (hall.limits[i] - size);
                    if (past > 0f) p -= hall.walls[i] * past;
                }
            return p;
        }

        static Vector2 KeepIn(Vector2 p, Box box, float size)
        {
            Vector2 from = p - box.pos;
            float x = Mathf.Clamp(Vector2.Dot(from, box.right), -Mathf.Max(0f, box.half.x - size), Mathf.Max(0f, box.half.x - size));
            float y = Mathf.Clamp(Vector2.Dot(from, box.forward), -Mathf.Max(0f, box.half.y - size), Mathf.Max(0f, box.half.y - size));
            return box.pos + box.right * x + box.forward * y;
        }

        // Out of a box by the shortest way: through whichever side is nearest.
        static Vector2 PushOut(Vector2 p, Box box, float size)
        {
            Vector2 from = p - box.pos;
            float x = Vector2.Dot(from, box.right), y = Vector2.Dot(from, box.forward);
            float overX = box.half.x + size - Mathf.Abs(x), overY = box.half.y + size - Mathf.Abs(y);
            if (overX <= 0f || overY <= 0f) return p;
            if (overX < overY) x += (x >= 0f ? overX : -overX);
            else y += (y >= 0f ? overY : -overY);
            return box.pos + box.right * x + box.forward * y;
        }
    }
}
