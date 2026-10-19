using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pulse
{
    // Flat marks on the ground: where a blow will land, and what one left behind.
    //
    //   Ring      a circle with a second one growing inside it. The blow lands when the two meet.
    //   Lane      the same for a charge: the strip it will run down, filling from the near end.
    //   Fissure   cracks that glow hotter and hotter: ground that is about to burst.
    //   Crack     a burst of cracks in the floor, left by a heavy blow.
    //   Scorch    a dark, ragged stain left by fire or a blast.
    //
    // Particles show what moves: fire, dust, sparks. A shape the player has to read at a glance,
    // such as how far a stomp reaches, is drawn as that shape. The first three are drawn for the
    // frame they are asked for and have to be asked for again every frame, so a warning can never
    // outlive the enemy that gives it. The last two stay for a while and fade.
    public sealed class Marks : MonoBehaviour
    {
        struct Stain
        {
            public Vector3 pos;
            public float size, turn, age, life;
            public Color color;
            public int shape;
            public bool crack;
        }

        const int Steps = 64, Shapes = 6, MaxStains = 90;
        // Edge: how wide the line of a ring is, as a share of its radius. Lift: how far the marks
        // lie above the ground, each kind a little higher than the one before, so none flickers
        // against another.
        const float Edge = 0.035f, Lift = 0.02f, CrackLife = 16f, ScorchLife = 13f, FadeOut = 5f, FadeIn = 0.12f;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly Color CrackDark = new Color(0.03f, 0.03f, 0.04f), CrackRim = new Color(0.62f, 0.6f, 0.56f);
        static readonly Color Soot = new Color(0.05f, 0.045f, 0.045f);

        readonly List<Stain> stains = new List<Stain>(MaxStains);
        readonly Mesh[] crackCore = new Mesh[Shapes], crackRim = new Mesh[Shapes], blots = new Mesh[Shapes];
        Mesh disc, ring, strip;
        Material material;
        MaterialPropertyBlock tint;

        void Awake()
        {
            // The same plain, see-through shader the line effects and the storm use.
            material = new Material(Shader.Find("Sprites/Default"));
            tint = new MaterialPropertyBlock();
            disc = Round("Disc", 0f);
            ring = Round("Ring", 1f - Edge);
            strip = Strip();
            var random = new System.Random(11);
            for (int i = 0; i < Shapes; i++)
            {
                Cracks(random, out crackCore[i], out crackRim[i]);
                blots[i] = Blot(random);
            }
        }

        public void Clear() => stains.Clear();

        public int Count => stains.Count;

        // ---- Warnings: drawn for this frame only ----

        // coming runs from 0, when the wind-up starts, to 1, when the blow lands.
        public void Ring(Vector3 pos, float radius, float coming, Color color)
        {
            coming = Mathf.Clamp01(coming);
            float shown = Mathf.Clamp01(coming / FadeIn);
            var at = Flat(pos);
            Draw(disc, Matrix4x4.TRS(at + Vector3.up * (Lift * 3f), Quaternion.identity, Vector3.one * (radius * coming)), color, 0.2f * shown);
            Draw(ring, Matrix4x4.TRS(at + Vector3.up * (Lift * 4f), Quaternion.identity, Vector3.one * (radius * coming)), color, 0.55f * shown);
            Draw(ring, Matrix4x4.TRS(at + Vector3.up * (Lift * 5f), Quaternion.identity, Vector3.one * radius), color, 0.95f * shown);
        }

        // A strip from a point along a direction. halfWidth: from its middle line to either edge.
        public void Lane(Vector3 from, Vector3 dir, float length, float halfWidth, float coming, Color color)
        {
            coming = Mathf.Clamp01(coming);
            float shown = Mathf.Clamp01(coming / FadeIn), line = Mathf.Max(0.08f, halfWidth * 0.07f);
            var at = Flat(from);
            var along = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z));
            Vector3 side = along * Vector3.right;
            // The part that has filled, the two edges, and the far end.
            Draw(strip, Matrix4x4.TRS(at + Vector3.up * (Lift * 3f), along, new Vector3(halfWidth, 1f, length * coming)), color, 0.2f * shown);
            Draw(strip, Matrix4x4.TRS(at + side * (halfWidth - line) + Vector3.up * (Lift * 5f), along, new Vector3(line, 1f, length)), color, 0.95f * shown);
            Draw(strip, Matrix4x4.TRS(at - side * (halfWidth - line) + Vector3.up * (Lift * 5f), along, new Vector3(line, 1f, length)), color, 0.95f * shown);
            Draw(strip, Matrix4x4.TRS(at + along * Vector3.forward * (length - line * 2f) + Vector3.up * (Lift * 5f), along, new Vector3(halfWidth, 1f, line * 2f)), color, 0.95f * shown);
            Draw(strip, Matrix4x4.TRS(at + along * Vector3.forward * (length * coming) + Vector3.up * (Lift * 4f), along, new Vector3(halfWidth, 1f, line * 2f)), color, 0.55f * shown);
        }

        // seed: any number that stays the same for one spot, so its cracks do not change shape.
        public void Fissure(Vector3 pos, float radius, float coming, Color color, int seed)
        {
            coming = Mathf.Clamp01(coming);
            int shape = Mathf.Abs(seed) % Shapes;
            var place = Matrix4x4.TRS(Flat(pos) + Vector3.up * (Lift * 3f), Quaternion.Euler(0f, Mathf.Abs(seed) % 360, 0f), Vector3.one * radius);
            Draw(crackRim[shape], place, color, 0.25f + 0.65f * coming);
            // Its heart goes white as the moment comes.
            Draw(crackCore[shape], place * Matrix4x4.Translate(Vector3.up * Lift), Color.Lerp(color, Color.white, coming), coming);
        }

        // ---- What a blow leaves behind ----

        public void Crack(Vector3 pos, float radius) => Leave(pos, radius, CrackDark, CrackLife, true);

        public void Scorch(Vector3 pos, float radius) => Leave(pos, radius, Soot, ScorchLife, false);

        public void Scorch(Vector3 pos, float radius, Color color) => Leave(pos, radius, Color.Lerp(color, Soot, 0.7f), ScorchLife, false);

        void Leave(Vector3 pos, float size, Color color, float life, bool crack)
        {
            // The oldest makes room.
            if (stains.Count >= MaxStains) stains.RemoveAt(0);
            stains.Add(new Stain
            {
                pos = Flat(pos), size = size, color = color, life = life, crack = crack,
                turn = Random.value * 360f, shape = Random.Range(0, Shapes),
            });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = stains.Count - 1; i >= 0; i--)
            {
                var stain = stains[i];
                stain.age += dt;
                if (stain.age >= stain.life) { stains.RemoveAt(i); continue; }
                stains[i] = stain;
                // It is there at once, as a blow is, and goes slowly.
                float strength = Mathf.Clamp01((stain.life - stain.age) / FadeOut);
                var place = Matrix4x4.TRS(stain.pos + Vector3.up * Lift, Quaternion.Euler(0f, stain.turn, 0f), Vector3.one * stain.size);
                if (stain.crack)
                {
                    // The broken edge catches the light; the crack itself is dark.
                    Draw(crackRim[stain.shape], place, CrackRim, 0.16f * strength);
                    Draw(crackCore[stain.shape], place * Matrix4x4.Translate(Vector3.up * Lift), stain.color, 0.9f * strength);
                }
                else Draw(blots[stain.shape], place, stain.color, 0.6f * strength);
            }
        }

        void Draw(Mesh mesh, Matrix4x4 place, Color color, float alpha)
        {
            if (alpha <= 0.004f) return;
            color.a = alpha;
            tint.SetColor(ColorId, color);
            Graphics.RenderMesh(new RenderParams(material) { matProps = tint, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false },
                mesh, 0, place);
        }

        static Vector3 Flat(Vector3 pos) => new Vector3(pos.x, 0f, pos.z);

        // ---- The shapes, all of size one ----

        // A disc, or with inner above zero a ring that leaves the middle open.
        static Mesh Round(string name, float inner)
        {
            var vertices = new Vector3[(Steps + 1) * 2];
            var triangles = new int[Steps * 6];
            for (int s = 0; s <= Steps; s++)
            {
                float angle = s * Mathf.PI * 2f / Steps;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[s * 2] = dir * inner;
                vertices[s * 2 + 1] = dir;
                if (s == Steps) break;
                int a = s * 2, t = s * 6;
                triangles[t] = a; triangles[t + 1] = a + 3; triangles[t + 2] = a + 1;
                triangles[t + 3] = a; triangles[t + 4] = a + 2; triangles[t + 5] = a + 3;
            }
            return Finish(new Mesh { name = name, vertices = vertices, triangles = triangles });
        }

        // One wide and one long, lying ahead of its own position.
        static Mesh Strip() => Finish(new Mesh
        {
            name = "Strip",
            vertices = new[] { new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(1f, 0f, 1f), new Vector3(-1f, 0f, 1f) },
            triangles = new[] { 0, 2, 1, 0, 3, 2 },
        });

        static Mesh Finish(Mesh mesh)
        {
            mesh.RecalculateBounds();
            return mesh;
        }

        // Cracks that run out from a point, fork, and are crossed by a few broken arcs: floor that
        // took a blow. core is the cracks themselves; rim is the same lines three times as wide,
        // for the chipped edge on either side of them.
        static void Cracks(System.Random random, out Mesh core, out Mesh rim)
        {
            var lines = new List<(Vector3 from, Vector3 to, float fromWidth, float toWidth)>();
            float Roll(float low, float high) => low + (float)random.NextDouble() * (high - low);

            void Branch(Vector3 from, float angle, float length, float width, int depth)
            {
                int pieces = depth == 0 ? 4 : 2;
                for (int p = 0; p < pieces; p++)
                {
                    angle += Roll(-0.42f, 0.42f);
                    var to = from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (length / pieces * Roll(0.75f, 1.2f));
                    float thinner = width * (1f - (p + 1f) / pieces * 0.8f), wider = width * (1f - (float)p / pieces * 0.8f);
                    lines.Add((from, to, wider, thinner));
                    if (depth < 2 && random.NextDouble() < 0.5)
                        Branch(to, angle + (random.Next(2) == 0 ? 1f : -1f) * Roll(0.55f, 1.05f), length * Roll(0.3f, 0.5f), thinner, depth + 1);
                    from = to;
                }
            }

            int arms = random.Next(7, 10);
            float turn = Roll(0f, 6.28f);
            for (int a = 0; a < arms; a++)
            {
                float angle = turn + (a + Roll(-0.3f, 0.3f)) * Mathf.PI * 2f / arms;
                var start = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Roll(0.02f, 0.1f);
                Branch(start, angle, Roll(0.6f, 0.95f), 0.05f, 0);
            }
            // Broken arcs around the middle, where the floor gave way.
            for (int a = 0; a < 5; a++)
            {
                float at = Roll(0f, 6.28f), reach = Roll(0.3f, 0.62f), span = Roll(0.35f, 0.9f);
                var from = new Vector3(Mathf.Cos(at), 0f, Mathf.Sin(at)) * reach;
                for (int p = 1; p <= 3; p++)
                {
                    float angle = at + span * p / 3f, far = reach * Roll(0.93f, 1.07f);
                    var to = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * far;
                    lines.Add((from, to, 0.03f, 0.024f));
                    from = to;
                }
            }
            core = Ribbons("Cracks", lines, 1f);
            rim = Ribbons("Crack edges", lines, 3.2f);
        }

        static Mesh Ribbons(string name, List<(Vector3 from, Vector3 to, float fromWidth, float toWidth)> lines, float widen)
        {
            var vertices = new Vector3[lines.Count * 4];
            var triangles = new int[lines.Count * 6];
            for (int i = 0; i < lines.Count; i++)
            {
                var (from, to, fromWidth, toWidth) = lines[i];
                Vector3 along = (to - from).normalized, side = new Vector3(-along.z, 0f, along.x);
                // A little longer than the piece, so that two pieces meeting at a bend leave no gap.
                from -= along * (fromWidth * widen * 0.4f);
                to += along * (toWidth * widen * 0.4f);
                int v = i * 4, t = i * 6;
                vertices[v] = from - side * (fromWidth * widen * 0.5f);
                vertices[v + 1] = from + side * (fromWidth * widen * 0.5f);
                vertices[v + 2] = to + side * (toWidth * widen * 0.5f);
                vertices[v + 3] = to - side * (toWidth * widen * 0.5f);
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            return Finish(new Mesh { name = name, vertices = vertices, triangles = triangles });
        }

        // A stain with a ragged edge: a fan of points at uneven distances, with a few tongues that
        // reach further out.
        static Mesh Blot(System.Random random)
        {
            const int points = 30;
            var vertices = new Vector3[points + 1];
            var triangles = new int[points * 3];
            for (int p = 0; p < points; p++)
            {
                float angle = p * Mathf.PI * 2f / points;
                float reach = 0.62f + 0.2f * (float)random.NextDouble() + (random.Next(5) == 0 ? 0.22f : 0f);
                vertices[p + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach;
                triangles[p * 3] = 0;
                triangles[p * 3 + 1] = 1 + (p + 1) % points;
                triangles[p * 3 + 2] = 1 + p;
            }
            return Finish(new Mesh { name = "Stain", vertices = vertices, triangles = triangles });
        }
    }
}
