using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pulse
{
    // The edge of the open world. There is no wall: the map ends in fog. The fog stands still and
    // grows denser the further in the machine goes. Inside it a storm rages: wind, sheet lightning
    // that lights the fog from within, and forked lightning that strikes the machine harder and more
    // often the deeper it is. WorldDef sets how deep the storm is and how much it hurts.
    public sealed class Storm : MonoBehaviour
    {
        // Light in the fog: a soft patch that flares up and dies away.
        struct Glow
        {
            public Vector3 pos;
            public float size, left, total, strength;
        }

        // The fog is a stack of flat sheets, each with the same cloud pattern laid over it at its own
        // scale and offset. Looking down through them as the camera moves gives the fog depth.
        // The first GroundLayers lie low and are thick: they swallow the ground, so the fog is plain to
        // see from inside the map, while the top of the machine still shows above them. The sheets
        // above the machine are only a veil, Veil times as thick. The fog starts a little inside the
        // map and is dense at its edge.
        const int Points = 220, Layers = 6, GroundLayers = 4, TextureSize = 256, GlowPoints = 28;
        const float Veil = 0.2f;
        const float ViewRange = 60f;
        // Lightning on the machine comes every SlowStrike seconds at the edge and every FastStrike at the far end.
        const float SlowStrike = 1.1f, FastStrike = 0.28f, SkyHeight = 30f, FlashFade = 9f;
        // How far beyond the edge each row of a sheet lies, and how thick one sheet is there.
        static readonly float[] Reach = { -14f, -7f, 0f, 6f, 14f, 24f, 45f, 130f };
        static readonly float[] Thick = { 0f, 0.11f, 0.32f, 0.58f, 0.75f, 0.85f, 0.9f, 0.95f };
        static readonly float[] Heights = { 0.25f, 0.55f, 0.9f, 1.25f, 3.2f, 5.6f };
        static readonly Color FogColor = new Color(0.56f, 0.6f, 0.67f);
        static readonly Color Bright = new Color(1f, 0.97f, 0.82f);
        static readonly Vector3 Wind = new Vector3(0.87f, 0f, -0.5f);
        static readonly int ColorId = Shader.PropertyToID("_Color");

        readonly List<Glow> glows = new List<Glow>(16);
        WorldDef world;
        Mesh[] sheets;
        Mesh glowMesh;
        Material fogMaterial, glowMaterial;
        MaterialPropertyBlock tint;
        float strikeIn, strayIn, sheetIn, windOwed, flash;

        // How deep the machine is in the storm, from 0 at its edge to 1 at the far end. 0 outside it.
        public float Depth { get; private set; }

        public void Build(WorldDef worldDef)
        {
            world = worldDef;
            // The same plain, see-through shader the line effects use.
            var shader = Shader.Find("Sprites/Default");
            fogMaterial = new Material(shader) { mainTexture = Clouds() };
            glowMaterial = new Material(shader);
            tint = new MaterialPropertyBlock();
            sheets = new Mesh[Layers];
            for (int layer = 0; layer < Layers; layer++) sheets[layer] = BuildSheet(world.halfSize, layer);
            glowMesh = BuildGlow();
        }

        void Update()
        {
            if (world == null || world.stormDepth <= 0f) return;
            var rig = Combat.Rig;
            bool openWorld = Combat.Director != null && Combat.Director.Mode == RunMode.OpenWorld;
            Vector3 pos = rig.transform.position;
            Depth = openWorld ? WorldRules.StormDepth(world, pos) : 0f;
            float dt = Time.deltaTime;

            var plain = new RenderParams(fogMaterial) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            for (int layer = 0; layer < Layers; layer++)
                Graphics.RenderMesh(plain, sheets[layer], 0, Matrix4x4.Translate(Vector3.up * Heights[layer]));

            // The distant ground sinks into the fog as well, and the flash of a strike washes the picture out.
            RenderSettings.fogColor = Color.Lerp(Tokens.Soot, FogColor * 0.7f, Depth);
            RenderSettings.fogStartDistance = Mathf.Lerp(Look.FogStart, 26f, Depth);
            RenderSettings.fogEndDistance = Mathf.Lerp(Look.FogEnd, 62f, Depth);
            flash = Mathf.Lerp(flash, 0f, 1f - Mathf.Exp(-FlashFade * dt));
            // Only in the open world: in a dungeon other things flash the picture.
            if (openWorld) Look.Exposure = flash;
            DrawGlows(dt);

            if (dt <= 0f || !openWorld) return;
            float edge = world.halfSize, nearEdge = Mathf.Max(Mathf.Abs(pos.x), Mathf.Abs(pos.z)) - edge;

            // Sheet lightning: the fog lights up from within, somewhere in view. It can be seen from
            // inside the map, long before the machine is in the storm.
            if (nearEdge > -ViewRange && (sheetIn -= dt) <= 0f)
            {
                sheetIn = Random.Range(0.5f, 2.2f) * Mathf.Lerp(1f, 0.45f, Depth);
                var at = pos + new Vector3(Random.Range(-ViewRange, ViewRange), 0f, Random.Range(-ViewRange, ViewRange));
                float beyond = Mathf.Max(Mathf.Abs(at.x), Mathf.Abs(at.z)) - edge;
                if (beyond > 4f) Light(at + Vector3.up * 6f, Random.Range(14f, 26f), Random.Range(0.25f, 0.5f), 0.55f);
            }
            if (Depth <= 0f || !rig.Exposed) { strikeIn = SlowStrike * 0.6f; return; }

            // Wind: streaks tearing across the picture, more of them the deeper the machine is.
            windOwed += dt * Mathf.Lerp(25f, 130f, Depth);
            int gusts = (int)windOwed;
            windOwed -= gusts;
            if (gusts > 0)
                Combat.Vfx.Rush(pos + Vector3.up * 1.5f - Wind * 6f, new Vector3(24f, 1.5f, 18f), Wind * 34f, Tokens.Dust, gusts, 3.4f, 0.45f);
            if (Combat.Camera != null) Combat.Camera.Shake(0.03f + 0.07f * Depth);

            // Lightning that misses, all around.
            strayIn -= dt;
            if (strayIn <= 0f)
            {
                strayIn = Random.Range(0.2f, 0.8f) * Mathf.Lerp(1.4f, 0.5f, Depth);
                float angle = Random.value * Mathf.PI * 2f;
                Strike(pos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(6f, 22f), 0.25f);
            }

            // Lightning that hits. Each strike takes what the storm deals in the time between two strikes.
            strikeIn -= dt;
            if (strikeIn > 0f) return;
            float wait = Mathf.Lerp(SlowStrike, FastStrike, Depth);
            strikeIn = wait;
            Strike(pos, 0.8f);
            rig.Scorch(WorldRules.StormDamage(world, Depth) * wait, StateType.Shock, "The storm");
            Combat.Vfx.Sparks(pos + Vector3.up * 0.8f, Tokens.Shock, 18, 12f, 0.7f, 0.35f);
            if (Combat.Camera != null) Combat.Camera.Shake(0.25f + 0.3f * Depth);
        }

        // A fork of lightning from the sky. Where it lands the ground is scorched and throws sparks and
        // splinters, the fog around the bolt lights up, and the whole picture flashes.
        void Strike(Vector3 ground, float power)
        {
            ground.y = 0f;
            Combat.Fx.Lightning(ground, SkyHeight, Tokens.Shock);
            Combat.Vfx.Sparks(ground + Vector3.up * 0.3f, Bright, 14, 13f, 0.7f, 0.3f);
            Combat.Vfx.Burst(ground + Vector3.up * 0.3f, Tokens.Dust, 8, 7f, 0.2f, 0.6f, falls: true);
            Combat.Vfx.Ground(ground, 1.6f, 0f, new Color(0.08f, 0.07f, 0.07f), 7, 0.7f, 2.2f);
            Light(ground + Vector3.up * 5f, 16f + 14f * power, 0.3f, 0.5f + 0.4f * power);
            // Kept modest: deep in the storm the strikes come several times a second.
            flash = Mathf.Max(flash, power * 0.55f);
        }

        void Light(Vector3 pos, float size, float time, float strength) =>
            glows.Add(new Glow { pos = pos, size = size, left = time, total = time, strength = strength });

        // Each glow flickers once or twice as it fades, the way lightning does.
        void DrawGlows(float dt)
        {
            for (int g = glows.Count - 1; g >= 0; g--)
            {
                var glow = glows[g];
                glow.left -= dt;
                if (glow.left <= 0f) { glows.RemoveAt(g); continue; }
                glows[g] = glow;
                float life = glow.left / glow.total, flicker = 0.6f + 0.4f * Mathf.Sin(glow.left * 70f);
                var color = Bright;
                color.a = glow.strength * life * flicker;
                tint.SetColor(ColorId, color);
                Graphics.RenderMesh(new RenderParams(glowMaterial) { matProps = tint, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false },
                    glowMesh, 0, Matrix4x4.TRS(glow.pos, Quaternion.identity, new Vector3(glow.size, 1f, glow.size)));
            }
        }

        // One sheet of fog: a square frame around the map. The colour of each corner carries how thick
        // the fog is there; the cloud pattern is laid over it at a scale and offset of the sheet's own.
        static Mesh BuildSheet(float edge, int layer)
        {
            int rows = Reach.Length;
            var vertices = new Vector3[Points * rows];
            var colors = new Color[Points * rows];
            var uvs = new Vector2[Points * rows];
            var triangles = new int[Points * (rows - 1) * 6];
            float scale = 1f / (38f + layer * 9f);
            var shift = new Vector2(layer * 0.37f, layer * 0.61f);
            for (int p = 0; p < Points; p++)
            {
                float angle = p * Mathf.PI * 2f / Points;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                // Stretching a circle by this turns it into a square with the same half width.
                float square = 1f / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.z));
                for (int r = 0; r < rows; r++)
                {
                    Vector3 at = dir * ((edge + Reach[r]) * square);
                    vertices[p * rows + r] = at;
                    uvs[p * rows + r] = new Vector2(at.x, at.z) * scale + shift;
                    var color = FogColor;
                    color.a = Thick[r] * (layer < GroundLayers ? 1f : Veil);
                    colors[p * rows + r] = color;
                }
                int next = (p + 1) % Points;
                for (int r = 0; r < rows - 1; r++)
                {
                    int a = p * rows + r, b = p * rows + r + 1, c = next * rows + r, d = next * rows + r + 1;
                    int t = (p * (rows - 1) + r) * 6;
                    triangles[t] = a; triangles[t + 1] = b; triangles[t + 2] = c;
                    triangles[t + 3] = c; triangles[t + 4] = b; triangles[t + 5] = d;
                }
            }
            var mesh = new Mesh { name = "Storm fog", vertices = vertices, colors = colors, uv = uvs, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        // A flat patch of radius 1 that is brightest in the middle and fades to nothing at its rim.
        static Mesh BuildGlow()
        {
            var vertices = new Vector3[GlowPoints * 2 + 1];
            var colors = new Color[GlowPoints * 2 + 1];
            var triangles = new int[GlowPoints * 9];
            colors[0] = Color.white;
            for (int p = 0; p < GlowPoints; p++)
            {
                float angle = p * Mathf.PI * 2f / GlowPoints;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                // An uneven outline, so the light does not read as a disc.
                float reach = 0.8f + 0.2f * Mathf.Sin(angle * 3f + 1f) * Mathf.Cos(angle * 2f);
                int mid = 1 + p * 2, rim = mid + 1, nextMid = 1 + (p + 1) % GlowPoints * 2, nextRim = nextMid + 1, t = p * 9;
                vertices[mid] = dir * (reach * 0.45f);
                colors[mid] = new Color(1f, 1f, 1f, 0.45f);
                vertices[rim] = dir * reach;
                colors[rim] = new Color(1f, 1f, 1f, 0f);
                triangles[t] = 0; triangles[t + 1] = nextMid; triangles[t + 2] = mid;
                triangles[t + 3] = mid; triangles[t + 4] = nextMid; triangles[t + 5] = nextRim;
                triangles[t + 6] = mid; triangles[t + 7] = nextRim; triangles[t + 8] = rim;
            }
            var mesh = new Mesh { name = "Storm glow", vertices = vertices, colors = colors, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        // The cloud pattern: soft noise in four sizes laid over each other, made so that it repeats
        // without a seam. White, with the pattern in how see-through it is.
        static Texture2D Clouds()
        {
            var random = new System.Random(11);
            int[] cells = { 4, 8, 16, 32 };
            float[] weight = { 0.5f, 0.27f, 0.15f, 0.08f };
            var lattice = new float[cells.Length][];
            for (int o = 0; o < cells.Length; o++)
            {
                lattice[o] = new float[cells[o] * cells[o]];
                for (int i = 0; i < lattice[o].Length; i++) lattice[o][i] = (float)random.NextDouble();
            }

            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                float sum = 0f;
                for (int o = 0; o < cells.Length; o++)
                {
                    int n = cells[o];
                    float fx = (float)x / TextureSize * n, fy = (float)y / TextureSize * n;
                    int x0 = (int)fx, y0 = (int)fy, x1 = (x0 + 1) % n, y1 = (y0 + 1) % n;
                    float tx = Mathf.SmoothStep(0f, 1f, fx - x0), ty = Mathf.SmoothStep(0f, 1f, fy - y0);
                    float top = Mathf.Lerp(lattice[o][y0 * n + x0], lattice[o][y0 * n + x1], tx);
                    float bottom = Mathf.Lerp(lattice[o][y1 * n + x0], lattice[o][y1 * n + x1], tx);
                    sum += Mathf.Lerp(top, bottom, ty) * weight[o];
                }
                // Thin in places, thick in others, never quite clear.
                float cloud = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.72f, sum));
                byte alpha = (byte)(255f * Mathf.Lerp(0.3f, 1f, cloud));
                pixels[y * TextureSize + x] = new Color32(255, 255, 255, alpha);
            }
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, true)
            {
                name = "Storm clouds", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
