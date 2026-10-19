using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pulse
{
    // A room behind one of the bay doors of the Roundhouse. Every machine of the lobby has one, and
    // each is furnished for the work that is done in it. This class is what they share: the door,
    // the shell of the room, how the character walks in, how the camera looks in, and the scene in
    // which a door that had to be bought opens.
    //
    // A room is looked into over one of its long sides, which is only a kerb. Which side that is
    // follows from where the camera stands while the character is inside: rooms to the left and
    // right of the hall are seen with the hall's own camera, rooms behind its far wall turn the
    // camera a little (Yaw), because from the hall's side they lie behind their own door wall.
    //
    // Places in a room are given as (across, out, up): across is along the door, out is the distance
    // behind it. Furniture uses Side instead of At, which counts across toward the walled side, so
    // that a room is laid out once and comes out right whichever way round it stands.
    public abstract class LobbyRoom : MonoBehaviour
    {
        // The room: how wide it is along the door and how deep, and how far behind the wall it starts.
        protected const float Width = 10f, Depth = 8.6f, Gap = 0.6f;
        // How far behind the door the room begins: nothing of it stands nearer to the hall than this.
        const float FrontEdge = 0.36f;
        // The door: how high the shutter is, how much of it is left when it is rolled up, and how
        // long the pieces of the scene take.
        const float ShutterTop = 4.2f, Rolled = 0.05f, LampTime = 0.45f, BarTime = 0.5f, RattleTime = 0.35f, RollTime = 1.5f;
        // The camera of the scene in which the door opens: how far round toward the front of the
        // door it comes from the hall camera's side, how far away it stands and how high.
        const float FrontShare = 0.72f, ViewDistance = 10.5f, ViewHeight = 4.6f;
        protected static readonly Color DustColor = new Color(0.34f, 0.33f, 0.32f);

        readonly List<Light> lights = new List<Light>();
        readonly List<float> full = new List<float>();
        protected GameConfig config;
        protected LobbyDef def;
        protected LobbyWalk walk;
        // Everything behind the door hangs on this.
        protected Transform room;
        Transform shutter, barLeft, barRight;
        MeshRenderer frame;
        // The dark that lies over the room until its light is up.
        Mesh darkBox;
        Material dark;
        protected Vector3 origin, outward, across;
        // Facing the hall, facing into the room, and facing into the room from its wall and from its kerb.
        protected Quaternion toHall, toRoom, fromWall, fromKerb;
        // +1 or -1: the side along the door on which the room has a wall.
        protected float wallSide;
        // Runs while the room is there. lit: how far its lamps are up, from 0 to 1.
        protected float clock, lit = 1f;
        int roomArea, doorArea;

        public bool IsOpen { get; private set; }
        // Where the machine that works here stands, and where the character stands to buy the door open.
        public Vector3 StationPos { get; protected set; }
        public Vector3 DoorPos { get; private set; }
        // The middle of the room, for the camera.
        public Vector3 Middle => At(0f, Gap + Depth * 0.5f);
        // Which way the door leads, seen from the hall.
        public Vector3 Outward => outward;
        // How far the camera is turned from its usual direction while the character is in this room.
        public float Yaw { get; private set; }
        // What the camera looks at while the door opens, and where it stands to do so, seen from
        // there: in front of the door, on the side the hall's own camera is on, and low enough to
        // look at the shutter and not down on it.
        public Vector3 DoorLook => origin + Vector3.up * 2f;
        public Vector3 DoorView { get; private set; }

        // How far back the back wall stands, and how far out the side walls.
        protected static float Back => Gap + Depth + 0.25f;
        protected static float Edge => Width * 0.5f + 0.3f;

        protected Vector3 At(float side, float depth, float height = 0f) => origin + across * side + outward * depth + Vector3.up * height;

        // The same, counted toward the walled side: a positive number is on the side of the wall,
        // a negative one on the side of the kerb, where the camera looks in.
        protected Vector3 Side(float toWall, float depth, float height = 0f) => At(wallSide * toWall, depth, height);

        // What stands in the room, and what moves in it. Solid things are given to the walk with Solid.
        protected abstract void Furnish();

        protected virtual void Run(float dt) { }

        // The floor of the room, and the two materials of its plates, its walls and their trim.
        protected virtual string FloorModel => "RM_Floor";
        protected virtual Material FloorBase => def.wallMaterial;
        protected virtual Material FloorAccent => def.trim;
        protected virtual Material WallBase => def.wallMaterial;
        protected virtual Material WallAccent => def.trim;

        // direction: where the door lies, seen from the middle of the hall. radius: how far out.
        // yaw: how far the camera turns while the character is in the room.
        public void Build(LobbyDef lobby, GameConfig game, LobbyWalk floor, Vector3 direction, float radius, bool open, float yaw)
        {
            def = lobby;
            config = game;
            walk = floor;
            outward = direction;
            origin = direction * radius;
            Yaw = yaw;
            toHall = Quaternion.LookRotation(-direction);
            toRoom = Quaternion.LookRotation(direction);
            across = toHall * Vector3.right;
            // The side along the door that faces the camera gets the kerb, the other the wall.
            Vector3 view = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            wallSide = Vector3.Dot(across, view) < 0f ? -1f : 1f;
            fromWall = Quaternion.LookRotation(-across * wallSide);
            fromKerb = Quaternion.LookRotation(across * wallSide);
            DoorPos = origin - outward * 1.6f;
            DoorView = Vector3.Slerp(Vector3.back, -outward, FrontShare).normalized * ViewDistance + Vector3.up * ViewHeight;

            // The door, which is always there.
            frame = Piece(transform, "Door frame", "RH_DoorFrame", origin, toHall).GetComponent<MeshRenderer>();
            shutter = Piece(frame.transform, "Shutter", "RH_Shutter", origin + Vector3.up * ShutterTop, toHall);
            barLeft = Piece(frame.transform, "Lock bar", "RH_LockBar", origin + toHall * new Vector3(-2.3f, 2.1f, 0.32f), toHall);
            barRight = Piece(frame.transform, "Lock bar", "RH_LockBar", origin + toHall * new Vector3(2.3f, 2.1f, 0.32f), toHall);
            barRight.localScale = new Vector3(-1f, 1f, 1f);

            // The room, which is not there until the door is open.
            room = new GameObject(GetType().Name).transform;
            room.SetParent(transform, false);
            Shell();
            Furnish();
            roomArea = walk.AddRoom(Middle, toHall, new Vector2(Width * 0.5f - 0.25f, Depth * 0.5f - 0.15f), open);
            // The doorway reaches well into the hall and well into the room. A character has to fit
            // into one area with its whole width at every step, so areas that only touch cannot be
            // walked between: the hall ends where the wall stands, and the room begins behind it.
            doorArea = walk.AddRoom(origin + outward * 0.2f, toHall, new Vector2(2.1f, 2f), open);
            Show(open);
        }

        // The floor, the back wall, one side wall, a kerb on the camera's side, and a pillar in
        // every corner. The pillars stand higher than the walls and the walls end inside them, so
        // nothing of two pieces lies in the same plane.
        void Shell()
        {
            float middle = Gap + Depth * 0.5f;
            Piece(room, "Floor", FloorModel, At(0f, middle), toRoom, def.lampGlow, FloorBase, FloorAccent);
            foreach (float side in new[] { -2.65f, 2.65f })
                Piece(room, "Wall", "TR_Wall", At(side, Back), toHall, def.lampGlow, WallBase, WallAccent);
            foreach (float depth in new[] { Gap + 2.2f, Gap + 6.6f })
            {
                Piece(room, "Wall", "TR_Wall", Side(Edge, depth), fromWall, def.lampGlow, WallBase, WallAccent).localScale = new Vector3(0.87f, 1f, 1f);
                Piece(room, "Kerb", "TR_Kerb", Side(-Edge, depth), fromKerb, null, WallBase, WallAccent).localScale = new Vector3(0.87f, 1f, 1f);
            }
            // Next to the door, where the hall's own wall turns away, a short piece closes the room.
            // Like everything of the room it stands behind FrontEdge: see DrawDark.
            float front = Gap + 0.25f;
            Piece(room, "Wall", "TR_Wall", Side(4.1f, front), toRoom, def.lampGlow, WallBase, WallAccent).localScale = new Vector3(0.44f, 1f, 1f);
            Piece(room, "Kerb", "TR_Kerb", Side(-4.1f, front), toRoom, null, WallBase, WallAccent).localScale = new Vector3(0.44f, 1f, 1f);
            foreach (float side in new[] { -4.1f, 4.1f }) walk.Block(At(side, front), toRoom, new Vector2(1.25f, 0.28f));
            Piece(room, "Pillar", "TR_Corner", Side(Edge, Back), toHall, null, WallBase, WallAccent);
            Piece(room, "Pillar", "TR_Corner", Side(-Edge, Back), toHall, null, WallBase, WallAccent);
            Piece(room, "Pillar", "TR_Corner", Side(Edge, front), toHall, null, WallBase, WallAccent);
        }

        // Puts the door and the room in the state they have when the door is shut or open.
        void Show(bool open)
        {
            IsOpen = open;
            room.gameObject.SetActive(open);
            shutter.localScale = new Vector3(1f, open ? Rolled : 1f, 1f);
            barLeft.localRotation = Quaternion.Euler(0f, 0f, open ? 90f : 0f);
            barRight.localRotation = Quaternion.Euler(0f, 0f, open ? -90f : 0f);
            Lamp(open);
            walk.Open(roomArea, open);
            walk.Open(doorArea, open);
        }

        // The lamps of the door and its lock: grey while it is locked, Pulse green once it is not.
        void Lamp(bool on)
        {
            var colour = on ? config.pulse : config.part;
            foreach (var renderer in frame.GetComponentsInChildren<MeshRenderer>())
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                renderer.sharedMaterials = Slots.For(mesh, def.wallMaterial, def.trim, config.dark, colour);
            }
        }

        // Whether someone standing there is in this room or in its doorway: then the camera is the room's.
        public bool Holds(Vector3 pos)
        {
            if (!IsOpen) return false;
            Vector3 from = pos - origin;
            return Vector3.Dot(from, outward) > -0.4f && Mathf.Abs(Vector3.Dot(from, across)) < Width * 0.5f + 0.5f;
        }

        // ---- Furnishing ----

        // A model of the lobby by its name. Materials that are left out are the walls' own.
        protected Transform Piece(Transform parent, string label, string model, Vector3 pos, Quaternion rotation, Material glowing = null,
            Material baseMaterial = null, Material accent = null) =>
            Piece(parent, label, def.Model(model), pos, rotation, glowing, baseMaterial, accent);

        protected Transform Piece(Transform parent, string label, Mesh mesh, Vector3 pos, Quaternion rotation, Material glowing = null,
            Material baseMaterial = null, Material accent = null)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, rotation);
            if (mesh == null) return go.transform;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = Slots.For(mesh, Slots.Or(baseMaterial, def.wallMaterial), Slots.Or(accent, def.trim),
                config.dark, Slots.Or(glowing, config.pulse));
            return go.transform;
        }

        // Something that stands in the room and is solid: the model, and a box of its own size on
        // the floor that the character walks around. half: half its width and half its depth.
        protected Transform Solid(string label, string model, Vector3 pos, Quaternion rotation, Vector2 half, Material glowing = null,
            Material baseMaterial = null, Material accent = null, Vector2 offset = default)
        {
            walk.Block(pos, rotation, half, offset);
            return Piece(room, label, model, pos, rotation, glowing, baseMaterial, accent);
        }

        // The same for something round.
        protected Transform Round(string label, string model, Vector3 pos, Quaternion rotation, float radius, Material glowing = null,
            Material baseMaterial = null, Material accent = null)
        {
            walk.Block(pos, radius);
            return Piece(room, label, model, pos, rotation, glowing, baseMaterial, accent);
        }

        // A material like another in a colour of its own, for a room that is not the colour of the hall.
        protected static Material Tinted(Material like, Color colour) => new Material(like) { color = colour };

        protected Light AddLight(string label, Vector3 pos, Color colour, float range, float intensity)
        {
            var light = new GameObject(label).AddComponent<Light>();
            light.transform.SetParent(room, true);
            light.transform.position = pos;
            light.type = LightType.Point;
            light.color = colour;
            light.range = range;
            light.intensity = intensity;
            lights.Add(light);
            full.Add(intensity);
            return light;
        }

        // ---- The door opens ----

        // The door is bought open. The lamps turn green, the two arms of the lock swing up against
        // the posts, the shutter rattles and rolls up, dust comes down from the lintel, and the light
        // of the room comes up behind it.
        public IEnumerator Unlock(CameraRig camera)
        {
            Lamp(true);
            Vector3 seam = origin + Vector3.up * 2.1f - outward * 0.4f;
            Combat.Vfx.Sparks(seam, Tokens.PulseGreen, 18, 7f, 0.5f, 0.3f);
            yield return new WaitForSeconds(LampTime);

            for (float t = 0f; t < BarTime; t += Time.deltaTime)
            {
                float swung = Mathf.SmoothStep(0f, 1f, t / BarTime) * 90f;
                barLeft.localRotation = Quaternion.Euler(0f, 0f, swung);
                barRight.localRotation = Quaternion.Euler(0f, 0f, -swung);
                yield return null;
            }
            barLeft.localRotation = Quaternion.Euler(0f, 0f, 90f);
            barRight.localRotation = Quaternion.Euler(0f, 0f, -90f);
            foreach (float side in new[] { -2.3f, 2.3f })
                Combat.Vfx.Sparks(origin + across * side + Vector3.up * 4.1f - outward * 0.4f, Tokens.Chalk, 12, 8f, 0.4f, 0.25f);
            if (camera != null) camera.Shake(0.2f);

            // From here on the room is there, in the dark: nothing of it shows until its light comes up.
            room.gameObject.SetActive(true);
            lit = 0f;
            Dim();
            for (float t = 0f; t < RattleTime; t += Time.deltaTime)
            {
                shutter.localPosition += Vector3.up * (Mathf.Sin(t * 70f) * 0.006f);
                yield return null;
            }
            float owed = 0f;
            for (float t = 0f; t < RollTime; t += Time.deltaTime)
            {
                float rolled = Mathf.SmoothStep(0f, 1f, t / RollTime);
                shutter.localScale = new Vector3(1f, Mathf.Lerp(1f, Rolled, rolled), 1f);
                lit = Mathf.Clamp01(rolled * 1.4f - 0.2f);
                Dim();
                // Dust off the shutter as it goes up, and off the floor where it stood.
                owed += Time.deltaTime * 46f;
                int puffs = (int)owed;
                owed -= puffs;
                for (int k = 0; k < puffs; k++)
                {
                    float side = Random.Range(-2.2f, 2.2f);
                    Combat.Vfx.Burst(origin + across * side + Vector3.up * (ShutterTop * (1f - rolled) + 0.1f), DustColor, 1, 1.4f, 0.14f, 0.7f, falls: true);
                }
                if (camera != null) camera.Shake(0.03f);
                yield return null;
            }
            shutter.localScale = new Vector3(1f, Rolled, 1f);
            if (camera != null) camera.Shake(0.18f);
            lit = 1f;
            Dim();
            Show(true);
        }

        // The lamps of the room at a share of their strength.
        void Dim()
        {
            for (int i = 0; i < lights.Count; i++) lights[i].intensity = full[i] * lit;
        }

        // While its light is not up, the room lies in the dark: a box around it in the colour of
        // the dark around the building, which thins out as the lamps come on. So the room is never
        // there all at once, from wherever the camera looks; it comes out of the dark. The box
        // starts behind the door, so the shutter itself stays in the light of the hall.
        void DrawDark()
        {
            if (lit >= 1f) return;
            if (darkBox == null)
            {
                const float side = Width * 0.5f + 0.8f, near = FrontEdge, far = Gap + Depth + 0.9f, low = -0.05f, high = 6.6f;
                var corners = new Vector3[8];
                for (int i = 0; i < 8; i++)
                    corners[i] = At((i & 1) == 0 ? -side : side, (i & 2) == 0 ? near : far, (i & 4) == 0 ? low : high);
                darkBox = new Mesh
                {
                    name = "Dark over the room", vertices = corners,
                    // The four sides and the top. There is nothing to hide from below.
                    triangles = new[] { 0, 4, 5, 0, 5, 1, 2, 3, 7, 2, 7, 6, 0, 2, 6, 0, 6, 4, 1, 5, 7, 1, 7, 3, 4, 6, 7, 4, 7, 5 },
                };
                darkBox.RecalculateBounds();
                // The same plain, see-through shader the storm and the line effects use. It draws both sides.
                dark = new Material(Shader.Find("Sprites/Default"));
            }
            Color colour = Tokens.Soot;
            colour.a = 1f - lit;
            dark.color = colour;
            Graphics.RenderMesh(new RenderParams(dark) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false }, darkBox, 0, Matrix4x4.identity);
        }

        void Update()
        {
            if (!IsOpen && !room.gameObject.activeSelf) return;
            DrawDark();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            clock += dt;
            Run(dt);
        }
    }
}
