using UnityEngine;

namespace Pulse
{
    // Builds floors from many small flat triangles, each in one shade from a small palette texture.
    // Every triangle faces straight up and neighbouring shades are close, so the floor reads as level.
    public static class GroundBuilder
    {
        public const int Shades = 8;
        // The entry of a biome's shades that is its ground colour exactly.
        const int MainShade = 2;

        // The open world floor. Each triangle takes its colours from the biome it lies in.
        public static Mesh World(float halfSize, int cells, int biomeCount, int seed)
        {
            var random = new System.Random(seed);
            float size = halfSize * 2f, step = size / cells;
            var corners = new Vector3[(cells + 1) * (cells + 1)];
            for (int z = 0; z <= cells; z++)
            for (int x = 0; x <= cells; x++)
                corners[z * (cells + 1) + x] = new Vector3(
                    -halfSize + (x + Jitter(random)) * step, 0f, -halfSize + (z + Jitter(random)) * step);

            var builder = new Builder(cells * cells * 2, biomeCount * Shades);
            for (int z = 0; z < cells; z++)
            for (int x = 0; x < cells; x++)
            {
                Vector3 a = corners[z * (cells + 1) + x], b = corners[z * (cells + 1) + x + 1];
                Vector3 c = corners[(z + 1) * (cells + 1) + x], d = corners[(z + 1) * (cells + 1) + x + 1];
                bool flip = (x + z) % 2 == 0;
                AddWorld(builder, random, a, flip ? d : c, flip ? b : d, biomeCount);
                AddWorld(builder, random, flip ? a : b, flip ? c : a, d, biomeCount);
            }
            return builder.ToMesh("World Floor");
        }

        // A round floor made of rings of tiles, for the lobby.
        public static Mesh Round(float radius, int rings, int segments, int seed)
        {
            var random = new System.Random(seed);
            var builder = new Builder(rings * segments * 2, Shades);
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < segments; s++)
            {
                float inner = radius * r / rings, outer = radius * (r + 1) / rings;
                float from = (s + r * 0.5f) * Mathf.PI * 2f / segments, to = (s + 1 + r * 0.5f) * Mathf.PI * 2f / segments;
                Vector3 a = Polar(inner, from), b = Polar(inner, to), c = Polar(outer, from), d = Polar(outer, to);
                // One shade per tile, darker toward the wall; every few rings a band of contrasting tiles.
                int shade = r % 4 == 3 ? 6 + random.Next(2) : random.Next(4);
                builder.Add(a, c, d, shade);
                builder.Add(a, d, b, shade);
            }
            return builder.ToMesh("Round Floor");
        }

        // The floor of a dungeon: one square slab for every cell of its map, laid like paving. Slabs
        // vary a little in shade; vents are marked in the accent colour of the biome.
        public static Mesh Dungeon(DungeonMap map)
        {
            int slabs = 0;
            for (int z = 0; z < map.Height; z++)
            for (int x = 0; x < map.Width; x++)
                if (map.At(x, z) != ' ') slabs++;

            var builder = new Builder(slabs * 2, Shades);
            for (int z = 0; z < map.Height; z++)
            for (int x = 0; x < map.Width; x++)
            {
                char cell = map.At(x, z);
                if (cell == ' ') continue;
                Vector3 a = map.CellCorner(x, z), b = a + Vector3.right * DungeonMap.Cell;
                Vector3 c = a + Vector3.forward * DungeonMap.Cell, d = b + Vector3.forward * DungeonMap.Cell;
                int shade = cell == 'v' ? 7 : cell == '@' || cell == 'B' ? 6 : (x * 7 + z * 13 + x * z) % 4;
                builder.Add(a, c, d, shade);
                builder.Add(a, d, b, shade);
            }
            return builder.ToMesh("Dungeon Floor");
        }

        // One row of texels: Shades entries per biome.
        public static Texture2D Palette(SceneryDef.Biome[] biomes)
        {
            var texture = new Texture2D(biomes.Length * Shades, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            for (int b = 0; b < biomes.Length; b++)
                for (int s = 0; s < Shades; s++)
                    texture.SetPixel(b * Shades + s, 0, Shade(biomes[b].ground, biomes[b].groundAlt, biomes[b].groundSpot, s));
            texture.Apply();
            return texture;
        }

        public static Texture2D Palette(Color ground, Color alt, Color spot) =>
            Palette(new[] { new SceneryDef.Biome { ground = ground, groundAlt = alt, groundSpot = spot } });

        static Color Shade(Color ground, Color alt, Color spot, int index) => index switch
        {
            0 => ground * 0.93f, 1 => ground * 0.97f, 2 => ground, 3 => ground * 1.04f,
            4 => alt * 0.97f, 5 => alt * 1.03f, 6 => Color.Lerp(ground, alt, 0.5f), _ => Color.Lerp(ground, spot, 0.7f),
        };

        static void AddWorld(Builder builder, System.Random random, Vector3 a, Vector3 b, Vector3 c, int biomeCount)
        {
            Vector3 middle = (a + b + c) / 3f;
            // The biome is read a little off-centre, which dithers the border between two biomes.
            Vector3 probe = middle + new Vector3(Jitter(random) * 7f, 0f, Jitter(random) * 7f);
            // The track the machine arrives on is plain ground of its biome, in the main shade.
            if (Scenery.OnArrivalTrack(middle)) builder.Add(a, b, c, Biomes.At(middle, biomeCount) * Shades + MainShade);
            else builder.Add(a, b, c, Biomes.At(probe, biomeCount) * Shades + NaturalShade(middle));
        }

        // Shades come from smooth noise, so they form patches with natural outlines, like soil, moss
        // or ash, and not a random mosaic.
        static int NaturalShade(Vector3 pos)
        {
            float broad = Mathf.PerlinNoise(pos.x * 0.035f + 40f, pos.z * 0.035f + 90f);
            float fine = Mathf.PerlinNoise(pos.x * 0.11f + 7f, pos.z * 0.11f + 3f);
            float spots = Mathf.PerlinNoise(pos.x * 0.06f + 200f, pos.z * 0.06f + 150f);
            if (spots > 0.78f) return 7;                       // a few patches of the accent colour
            if (broad > 0.6f) return fine > 0.5f ? 5 : 4;      // large areas of the second colour
            if (broad > 0.54f) return 6;                       // a soft edge between the two
            return Mathf.Clamp((int)(fine * 4f), 0, 3);        // gentle variation of the main colour
        }

        static Vector3 Polar(float radius, float angle) => new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

        static float Jitter(System.Random random) => (float)random.NextDouble() * 0.7f - 0.35f;

        sealed class Builder
        {
            readonly Vector3[] vertices, normals;
            readonly Vector2[] uvs;
            readonly int[] triangles;
            readonly int texels;
            int count;

            public Builder(int triangleCount, int texels)
            {
                vertices = new Vector3[triangleCount * 3];
                normals = new Vector3[triangleCount * 3];
                uvs = new Vector2[triangleCount * 3];
                triangles = new int[triangleCount * 3];
                this.texels = texels;
            }

            public void Add(Vector3 a, Vector3 b, Vector3 c, int texel)
            {
                // Keep the face pointing up whichever way the corners were given.
                if (Vector3.Cross(b - a, c - a).y < 0f) (b, c) = (c, b);
                var normal = Vector3.up;
                var uv = new Vector2((texel + 0.5f) / texels, 0.5f);
                foreach (var corner in new[] { a, b, c })
                {
                    vertices[count] = corner;
                    normals[count] = normal;
                    uvs[count] = uv;
                    triangles[count] = count;
                    count++;
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.uv = uvs;
                mesh.triangles = triangles;
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
