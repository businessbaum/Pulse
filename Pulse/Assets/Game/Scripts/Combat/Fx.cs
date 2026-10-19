using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pulse
{
    // Short-lived line effects: straight lines that announce an attack, arcs of electricity and
    // forks of lightning. Lines are pooled. Circles and lanes on the ground are drawn by Marks.
    public sealed class Fx : MonoBehaviour
    {
        sealed class Item
        {
            public LineRenderer line;
            public float left, total;
            public Color color;
            // hold: stays at full strength until it is gone, for lines that announce an attack.
            // flicker: stutters between bright and dim as it fades, for lightning.
            public bool hold, flicker;
        }

        const int BoltPoints = 13, BranchPoints = 6;

        readonly List<Item> live = new List<Item>(64);
        readonly Stack<Item> pool = new Stack<Item>(64);
        readonly Vector3[] path = new Vector3[BoltPoints];
        Material material;

        void Awake() => material = new Material(Shader.Find("Sprites/Default"));

        public void Line(Vector3 from, Vector3 to, Color color, float time = 0.12f, float width = 0.14f, bool hold = false)
        {
            var item = Get(2, width, color, time);
            item.hold = hold;
            item.line.SetPosition(0, from);
            item.line.SetPosition(1, to);
        }

        // A jagged line between two points that looks like a lightning arc.
        public void Bolt(Vector3 from, Vector3 to, Color color, float time = 0.14f, float jitter = 0.45f)
        {
            const int points = 8;
            var item = Get(points, 0.11f, color, time);
            Vector3 side = Vector3.Cross(to - from, Vector3.up).normalized;
            for (int p = 0; p < points; p++)
            {
                float t = p / (points - 1f);
                float bend = p == 0 || p == points - 1 ? 0f : Random.Range(-jitter, jitter);
                item.line.SetPosition(p, Vector3.Lerp(from, to, t) + side * bend + Vector3.up * Random.Range(0f, jitter * 0.5f));
            }
        }

        // A fork of lightning from the sky down to a point on the ground: a thick crooked bolt in the
        // given colour with a white core inside it, and thinner branches that split off on the way
        // down. It stutters as it fades.
        public void Lightning(Vector3 ground, float height, Color color, float time = 0.26f)
        {
            Vector3 top = ground + new Vector3(Random.Range(-7f, 7f), height, Random.Range(-7f, 7f));
            for (int p = 0; p < BoltPoints; p++)
            {
                float t = p / (BoltPoints - 1f);
                // Wild high up, exact where it lands.
                float bend = p == BoltPoints - 1 ? 0f : 2.4f * (1f - t * 0.6f);
                path[p] = Vector3.Lerp(top, ground, t) + new Vector3(Random.Range(-bend, bend), 0f, Random.Range(-bend, bend));
            }
            Trace(path, BoltPoints, 0.55f, color, time);
            Trace(path, BoltPoints, 0.18f, Color.white, time);

            int branches = Random.Range(2, 5);
            for (int b = 0; b < branches; b++)
            {
                // A branch leaves the bolt somewhere in its upper two thirds and dies out in the air.
                Vector3 from = path[Random.Range(1, BoltPoints * 2 / 3)];
                Vector3 to = from + new Vector3(Random.Range(-9f, 9f), -Random.Range(4f, 10f), Random.Range(-9f, 9f));
                to.y = Mathf.Max(to.y, 1f);
                var item = Get(BranchPoints, 0.2f, color, time * 0.8f);
                item.flicker = true;
                for (int p = 0; p < BranchPoints; p++)
                {
                    float t = p / (BranchPoints - 1f), bend = p == 0 ? 0f : 1.3f;
                    item.line.SetPosition(p, Vector3.Lerp(from, to, t) + new Vector3(Random.Range(-bend, bend), 0f, Random.Range(-bend, bend)));
                }
            }
        }

        void Trace(Vector3[] points, int count, float width, Color color, float time)
        {
            var item = Get(count, width, color, time);
            item.flicker = true;
            for (int p = 0; p < count; p++) item.line.SetPosition(p, points[p]);
        }

        Item Get(int points, float width, Color color, float time)
        {
            Item item;
            if (pool.Count > 0) item = pool.Pop();
            else
            {
                var go = new GameObject("FxLine");
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = true;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                item = new Item { line = line };
            }
            item.line.gameObject.SetActive(true);
            item.line.positionCount = points;
            item.line.loop = false;
            item.line.widthMultiplier = width;
            item.line.startColor = item.line.endColor = color;
            item.color = color;
            item.hold = item.flicker = false;
            item.left = item.total = time;
            live.Add(item);
            return item;
        }

        void Update()
        {
            // Unscaled, so effects still fade while the game is paused.
            float dt = Time.unscaledDeltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var item = live[i];
                item.left -= dt;
                if (item.left <= 0f)
                {
                    item.line.gameObject.SetActive(false);
                    pool.Push(item);
                    live.RemoveAt(i);
                    continue;
                }
                var color = item.color;
                float fade = item.left / item.total;
                color.a = item.hold ? 1f : item.flicker ? Mathf.Sqrt(fade) * (Random.value < 0.25f ? 0.2f : 1f) : fade;
                item.line.startColor = item.line.endColor = color;
            }
        }
    }
}
