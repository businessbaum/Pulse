using System.Collections.Generic;
using UnityEngine;

namespace Pulse
{
    // A dungeon as it was drawn: a grid of cells read from the rows of text in its DungeonDef.
    //
    //   #  wall                 o  pillar               .  floor            (space)  nothing, outside the map
    //   @  where the machine lands                      s  where enemies appear
    //   v  a vent: ground that keeps burning, freezing or oozing, depending on the biome
    //   c  a stand with spectators (boss arenas)        B  where the boss stands        G  the boss's gate
    //
    // Walls, pillars, stands, the gate and everything outside the map block movement and shots.
    // The top row of the text is the north side of the map.
    public sealed class DungeonMap
    {
        // The side of one cell in metres. Passages should be three cells wide: the machine is nearly one and a half.
        public const float Cell = 2f;
        const int Unreached = int.MaxValue;
        const float PillarRadius = 0.95f;

        readonly char[,] cells;
        // How many cells each cell is from the machine, going around walls. Enemies walk down this.
        readonly int[,] steps;
        readonly Queue<int> queue = new Queue<int>();
        readonly Vector3 corner;

        public int Width { get; }
        public int Height { get; }
        public Vector3 Center { get; }
        // Half the diagonal: nothing of the map is further from its centre.
        public float Radius { get; }
        public Vector3 Start { get; }
        public Vector3 BossSpawn { get; }
        public readonly List<Vector3> Spawns = new List<Vector3>(), Vents = new List<Vector3>(), Stands = new List<Vector3>();
        public readonly List<Vector3> Walls = new List<Vector3>(), Pillars = new List<Vector3>(), Gate = new List<Vector3>();

        public DungeonMap(string text, Vector3 center)
        {
            var rows = new List<string>();
            foreach (string raw in (text ?? "").Split('\n'))
            {
                string row = raw.TrimEnd('\r', ' ');
                // Empty rows before the first and after the last drawn row are not part of the map.
                if (row.Length > 0 || rows.Count > 0) rows.Add(row);
            }
            while (rows.Count > 0 && rows[rows.Count - 1].Length == 0) rows.RemoveAt(rows.Count - 1);
            if (rows.Count == 0) rows.Add("@");

            Height = rows.Count;
            foreach (string row in rows) Width = Mathf.Max(Width, row.Length);
            cells = new char[Width, Height];
            steps = new int[Width, Height];
            Center = center;
            Radius = 0.5f * Mathf.Sqrt(Width * Width + Height * Height) * Cell;
            corner = center - new Vector3(Width * Cell * 0.5f, 0f, Height * Cell * 0.5f);
            Start = BossSpawn = center;

            for (int z = 0; z < Height; z++)
            for (int x = 0; x < Width; x++)
            {
                // Row 0 of the text is the north side, which is the highest z.
                string row = rows[Height - 1 - z];
                char cell = x < row.Length ? row[x] : ' ';
                cells[x, z] = cell;
                Vector3 pos = CellCenter(x, z);
                switch (cell)
                {
                    case '#': Walls.Add(pos); break;
                    case 'o': Pillars.Add(pos); break;
                    case 'c': Stands.Add(pos); break;
                    case 'G': Gate.Add(pos); break;
                    case 's': Spawns.Add(pos); break;
                    case 'v': Vents.Add(pos); break;
                    case '@': Start = pos; break;
                    case 'B': BossSpawn = pos; break;
                }
            }
            if (Spawns.Count == 0) Spawns.Add(Start);
        }

        // The text of a plain round arena, for a dungeon that has no map of its own.
        public static string Round(float radius)
        {
            int half = Mathf.CeilToInt(radius / Cell) + 1, size = half * 2 + 1;
            var text = new System.Text.StringBuilder();
            for (int row = 0; row < size; row++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = x - half, dz = row - half;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz) * Cell;
                    bool spawn = (dx == 0 || dz == 0) && Mathf.Abs(dist - (radius - Cell * 2f)) < Cell * 0.5f;
                    text.Append(dx == 0 && dz == 0 ? '@' : spawn ? 's' : dist < radius ? '.' : dist < radius + Cell * 1.5f ? '#' : ' ');
                }
                text.Append('\n');
            }
            return text.ToString();
        }

        public char At(int x, int z) => x < 0 || z < 0 || x >= Width || z >= Height ? ' ' : cells[x, z];

        public Vector3 CellCenter(int x, int z) => corner + new Vector3((x + 0.5f) * Cell, 0f, (z + 0.5f) * Cell);

        public Vector3 CellCorner(int x, int z) => corner + new Vector3(x * Cell, 0f, z * Cell);

        int CellX(float worldX) => Mathf.FloorToInt((worldX - corner.x) / Cell);

        int CellZ(float worldZ) => Mathf.FloorToInt((worldZ - corner.z) / Cell);

        static bool Solid(char cell) => cell == '#' || cell == 'o' || cell == 'c' || cell == 'G' || cell == ' ';

        public bool Blocked(Vector3 pos) => Solid(At(CellX(pos.x), CellZ(pos.z)));

        // Moves a circle out of every wall and pillar it overlaps. hit says whether it touched any;
        // normal points away from what it touched.
        public Vector3 Resolve(Vector3 pos, float radius, out bool hit, out Vector3 normal)
        {
            hit = false;
            normal = Vector3.zero;
            // Twice, so that being pushed out of one wall into another is settled as well.
            for (int pass = 0; pass < 2; pass++)
            {
                int x0 = CellX(pos.x - radius), x1 = CellX(pos.x + radius), z0 = CellZ(pos.z - radius), z1 = CellZ(pos.z + radius);
                for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    char cell = At(x, z);
                    if (!Solid(cell)) continue;
                    Vector3 low = CellCorner(x, z);
                    float nearX, nearZ, reach = radius;
                    if (cell == 'o')
                    {
                        // A pillar is round and stands in the middle of its cell.
                        nearX = low.x + Cell * 0.5f;
                        nearZ = low.z + Cell * 0.5f;
                        reach += PillarRadius;
                    }
                    else
                    {
                        nearX = Mathf.Clamp(pos.x, low.x, low.x + Cell);
                        nearZ = Mathf.Clamp(pos.z, low.z, low.z + Cell);
                    }
                    float dx = pos.x - nearX, dz = pos.z - nearZ, sq = dx * dx + dz * dz;
                    if (sq >= reach * reach) continue;
                    hit = true;
                    if (sq > 0.000001f)
                    {
                        float dist = Mathf.Sqrt(sq);
                        pos.x += dx / dist * (reach - dist);
                        pos.z += dz / dist * (reach - dist);
                        normal += new Vector3(dx / dist, 0f, dz / dist);
                        continue;
                    }
                    // The centre is inside the cell: out through its nearest side.
                    float left = pos.x - low.x, right = low.x + Cell - pos.x, down = pos.z - low.z, up = low.z + Cell - pos.z;
                    float least = Mathf.Min(Mathf.Min(left, right), Mathf.Min(down, up));
                    if (least == left) { pos.x = low.x - radius; normal += Vector3.left; }
                    else if (least == right) { pos.x = low.x + Cell + radius; normal += Vector3.right; }
                    else if (least == down) { pos.z = low.z - radius; normal += Vector3.back; }
                    else { pos.z = low.z + Cell + radius; normal += Vector3.forward; }
                }
            }
            normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.zero;
            return pos;
        }

        // Whether nothing solid lies on the straight line between two points.
        public bool Sight(Vector3 from, Vector3 to)
        {
            Vector3 line = to - from;
            line.y = 0f;
            int samples = Mathf.CeilToInt(line.magnitude / (Cell * 0.45f));
            for (int i = 1; i < samples; i++)
                if (Blocked(from + line * ((float)i / samples))) return false;
            return true;
        }

        // Works out, for every cell, how far it is from the target going around walls.
        public void Flow(Vector3 target)
        {
            for (int z = 0; z < Height; z++)
            for (int x = 0; x < Width; x++)
                steps[x, z] = Unreached;
            int tx = CellX(target.x), tz = CellZ(target.z);
            if (Solid(At(tx, tz))) return;
            steps[tx, tz] = 0;
            queue.Clear();
            queue.Enqueue(tz * Width + tx);
            while (queue.Count > 0)
            {
                int index = queue.Dequeue(), x = index % Width, z = index / Width, next = steps[x, z] + 1;
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = x + dx, nz = z + dz;
                    if (Solid(At(nx, nz)) || steps[nx, nz] <= next) continue;
                    // No squeezing diagonally past the corner of a wall.
                    if (dx != 0 && dz != 0 && (Solid(At(nx, z)) || Solid(At(x, nz)))) continue;
                    steps[nx, nz] = next;
                    queue.Enqueue(nz * Width + nx);
                }
            }
        }

        // The way to walk from a point to get to the target of the last Flow. Zero when there is no way.
        public Vector3 Direction(Vector3 from)
        {
            int x = CellX(from.x), z = CellZ(from.z);
            if (Solid(At(x, z)) || steps[x, z] == Unreached) return Vector3.zero;
            int best = steps[x, z], bestX = x, bestZ = z;
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, nz = z + dz;
                if (Solid(At(nx, nz)) || steps[nx, nz] >= best) continue;
                if (dx != 0 && dz != 0 && (Solid(At(nx, z)) || Solid(At(x, nz)))) continue;
                best = steps[nx, nz];
                bestX = nx;
                bestZ = nz;
            }
            Vector3 way = CellCenter(bestX, bestZ) - from;
            way.y = 0f;
            return way.sqrMagnitude > 0.0001f ? way.normalized : Vector3.zero;
        }
    }
}
