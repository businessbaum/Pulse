using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Pulse
{
    // The lobby: a round engine shed with no enemies. The machine stands on the turntable in the
    // middle, where four arms work on it, and a display next to it says which of the player's
    // machines that is. A wall runs all the way round the hall; behind four of its bay doors lie
    // the rooms of the machines that work here. The player walks a small robot around: builds the
    // machine in the Mechanic's shop, buys and upgrades frames in the Smelter's foundry, looks
    // through what has been found in the Archivist's archive, raises parts in the Tuner's room,
    // whose door has to be bought open first, and starts a run from the green pad in front of the
    // gate. See lobby.md.
    [RequireComponent(typeof(UIDocument))]
    public sealed class LobbyRoot : MonoBehaviour
    {
        const int Segments = 14;
        const float Radius = 13.4f, StationReach = 3f, GateSeconds = 1.7f;
        // The departure pad: a half circle on the floor with its straight side at the gate. Standing in
        // it starts the run. PadInset: how far inside the wall its centre lies.
        const float PadInset = 0.9f, PadRadius = 4.2f;
        const int PadSteps = 40;
        // Leaving: the arms fold away (ClearTime at most), the deck swings the machine round to face
        // the character (TurnTime for a half turn), it rolls up and stops StopShort before the character
        // (RollTime), the character jumps onto it (JumpTime), and it pulls away through the gate (ExitTime).
        const float ClearTime = 2.6f, TurnTime = 1.1f, RollTime = 0.95f, StopShort = 2.5f, JumpTime = 0.55f, JumpHeight = 2.2f, DeckHeight = 1.05f;
        // ExitPush is chosen so that the machine leaves at the speed it arrives with in the open world.
        const float ExitTime = 1.6f, ExitPush = GameRoot.ArriveSpeed / ExitTime, ExitTurn = 200f;
        // The camera during that scene pulls back to SceneZoom to show machine and character together.
        // While the machine pulls away it moves to where the open world's camera will be, and from
        // CloseFrom on (a share of ExitTime) the dark closes in around the machine.
        const float SceneZoom = 1.3f, CloseFrom = 0.6f;
        // Coming back through the teleporter: the picture opens on the empty deck while light pours
        // down (BeamTime), the machine snaps into being (PopTime), stands for a moment (SettleTime),
        // and the character jumps down from it and lands JumpOut from the middle (LandTime).
        const float BeamTime = 0.8f, PopTime = 0.26f, SettleTime = 0.45f, LandTime = 0.6f, JumpOut = 4.1f, BackTime = 0.6f;
        // How far from the middle the character is kept: off the deck, which turns.
        const float DeckBlock = 2.75f;
        // How far inside the line of the wall pieces the character stops: at their foot.
        const float WallFoot = 0.45f;
        // Buying the door of a room open: the camera swings round in front of the door (SwingTime),
        // stays while it opens and a moment longer (GazeTime), rises over the room behind it and
        // back into its usual angle (PeekTime), and comes back to the character (LookTime).
        const float SwingTime = 1f, GazeTime = 0.55f, PeekTime = 1.6f, LookTime = 0.6f;
        // The wall pieces on the camera's side are cut off at the height of a railing, so the
        // building is closed all the way round and the view into it stays clear.
        const float CutawayBelow = -0.35f;
        // Which wall pieces hold the doors to the rooms. The Tuner's has to be bought open.
        const int MechanicBay = 12, ArchivistBay = 2, SmelterBay = 4, TunerBay = 10;
        // The display at the turntable: where it stands, in degrees round the middle and metres out.
        // It keeps clear of the arms and of where the character jumps down from the machine.
        const float DisplayAngle = 246f, DisplayRadius = 3.42f;
        // How quickly the camera turns when the character walks into a room that is looked into from the side.
        const float YawTime = 0.4f;
        static readonly Color DustColor = new Color(0.34f, 0.33f, 0.32f);

        public GameConfig config;

        // Something the player can walk up to and use: a machine, or a door that can be bought.
        sealed class Station
        {
            public string name;
            public Vector3 pos;
            public System.Action open;
            // What using it costs, for a door. Zero: nothing.
            public int price;
            public int sign;
            public bool gone;
        }

        // What gets a floating sign: only things the player can use.
        readonly List<(string title, Vector3 pos)> signs = new List<(string, Vector3)>();
        readonly List<Station> stations = new List<Station>();
        readonly List<LobbyRoom> rooms = new List<LobbyRoom>();
        LobbyCharacter character;
        LobbyHud hud;
        MechanicScreen mechanic;
        ArchiveScreen archive;
        SmelterScreen smelter;
        TunerScreen tuner;
        PresetScreen presets;
        Workshop workshop;
        TuningRoom tuningRoom;
        MechanicRoom mechanicRoom;
        ArchiveRoom archiveRoom;
        SmelterRoom smelterRoom;
        LobbyWalk walk;
        Station tunerDoor, tunerStation;
        CameraRig cameraRig;
        Camera view;
        Rig display;
        Transform padFill;
        Vector3 padPos;
        Quaternion padFacing;
        float gateProgress, viewYaw, yawSpeed;
        int archiveSign;
        // busy: a scene is playing, and the player has no say until it is over.
        bool busy, archiveWasOpen, usingPad;

        void Awake()
        {
            Time.timeScale = 1f;
            Rig.MoveOverride = null;
            var def = config.lobby;
            Profile.Load(config);
            RunSetup.Arriving = false;
            // The lobby has no fights, but its scenes use the same particles as a run.
            Combat.Config = config;
            Combat.Vfx = gameObject.AddComponent<Vfx>();

            // The floor plan: the hall inside its fourteen wall pieces, with the deck in its middle.
            walk = new LobbyWalk();
            var around = new List<Vector3>();
            for (int i = 0; i < Segments; i++) around.Add(Direction(i));
            walk.AddHall(around, Radius - WallFoot);
            walk.Block(Vector3.zero, DeckBlock);

            character = new GameObject("Character").AddComponent<LobbyCharacter>();
            character.Build(def, config);
            character.Walk = walk;
            character.transform.position = new Vector3(0f, 0f, -6.5f);

            BuildRoom(def);
            BuildPad();
            workshop = gameObject.AddComponent<Workshop>();
            workshop.Build(def, config);
            foreach (var foot in workshop.Feet()) walk.Block(foot, 0.48f);

            // The display at the turntable, where the machine that stands on it is chosen.
            float displayAngle = DisplayAngle * Mathf.Deg2Rad;
            Vector3 displayDir = new Vector3(Mathf.Cos(displayAngle), 0f, Mathf.Sin(displayAngle)), displayPos = displayDir * DisplayRadius;
            Place("Display", def.Model("RH_Display"), displayPos, Quaternion.LookRotation(displayDir), def.trim, def.wallMaterial, config.pulse);
            walk.Block(displayPos, 0.5f);
            var screenLight = new GameObject("Display light").AddComponent<Light>();
            screenLight.transform.SetParent(transform, false);
            screenLight.transform.position = displayPos + displayDir * 0.7f + Vector3.up * 1.6f;
            screenLight.type = LightType.Point;
            screenLight.color = new Color(0.45f, 1f, 0.7f);
            screenLight.range = 3.5f;
            screenLight.intensity = 2.2f;
            Use("Machines", () => OpenPresets(), displayPos + displayDir * 0.6f);

            // The rooms behind the bay doors. Three are open from the start; the Tuner's is bought.
            mechanicRoom = Room<MechanicRoom>(MechanicBay, true);
            archiveRoom = Room<ArchiveRoom>(ArchivistBay, true);
            smelterRoom = Room<SmelterRoom>(SmelterBay, true);
            tuningRoom = Room<TuningRoom>(TunerBay, Profile.IsUnlocked(TuningRoom.Id));
            Use("Mechanic", () => OpenMechanic(), mechanicRoom.StationPos);
            archiveSign = signs.Count;
            Use("Archivist", () => OpenArchive(), archiveRoom.StationPos);
            Use("Smelter", () => OpenSmelter(), smelterRoom.StationPos);
            // Until the Tuner's door is bought, the door is what can be used.
            tunerDoor = Use("Tuning room", () => BuyRoom(), tuningRoom.DoorPos, def.tunerRoomPrice);
            tunerDoor.gone = tuningRoom.IsOpen;
            tunerStation = Use("Tuner", () => OpenTuner(), tuningRoom.TunerPos);
            tunerStation.gone = !tuningRoom.IsOpen;
            ShowMachine();

            view = Camera.main;
            if (view == null) return;
            Look.Apply(view, indoors: true);
            if (view.TryGetComponent(out cameraRig))
            {
                cameraRig.target = character.transform;
                cameraRig.offset = new Vector3(0f, 12.5f, -10f);
                cameraRig.Snap();
            }
        }

        void Start()
        {
            hud = new LobbyHud(GetComponent<UIDocument>().rootVisualElement);
            hud.ShowMachine(Profile.Machine);
            foreach (var (title, pos) in signs) hud.AddSign(title, pos);
            mechanic = new MechanicScreen(hud.Screen, config, MachineChanged);
            presets = new PresetScreen(hud.Screen, MachineChanged);
            archive = new ArchiveScreen(hud.Screen, config);
            smelter = new SmelterScreen(hud.Screen, config, MachineChanged);
            tuner = new TunerScreen(hud.Screen, config, MachineChanged);
            NameArchive();
            NameRoom();
            // Back from a run through the teleporter: the machine arrives before the player gets to
            // walk, and the Beats it brought are counted onto the pile.
            if (RunSetup.Returning)
            {
                hud.CountFrom(Profile.Beats - RunSetup.Brought);
                StartCoroutine(Arrive());
            }
            RunSetup.Returning = false;
            RunSetup.Brought = 0;
        }

        // Makes something usable: it gets a sign, and a prompt when the character stands next to it.
        Station Use(string name, System.Action open, Vector3 pos, int price = 0)
        {
            var station = new Station { name = name, open = open, pos = pos, price = price, sign = signs.Count };
            stations.Add(station);
            signs.Add((name, pos + Vector3.up * (price > 0 ? 3.6f : 2.8f)));
            return station;
        }

        // Builds the room behind a bay door. A room to the left or right of the hall is looked into
        // with the hall's own camera. One behind the far wall lies behind its own door from there,
        // so the camera turns to look in over its long side while the character is in it.
        T Room<T>(int bay, bool open) where T : LobbyRoom
        {
            Vector3 dir = Direction(bay), side = new Vector3(dir.z, 0f, -dir.x);
            if (side.z < 0f) side = -side;
            float yaw = Mathf.Atan2(side.x, side.z) * Mathf.Rad2Deg;
            var room = gameObject.AddComponent<T>();
            room.Build(config.lobby, config, walk, dir, Radius, open, Mathf.Abs(yaw) < 20f ? 0f : yaw);
            rooms.Add(room);
            return room;
        }

        static bool HasRoom(int bay) => bay == MechanicBay || bay == ArchivistBay || bay == SmelterBay || bay == TunerBay;

        // The door says what it costs while it is locked. Once it is open, the Tuner has the sign.
        void NameRoom()
        {
            bool open = tuningRoom.IsOpen;
            tunerDoor.gone = open;
            tunerStation.gone = !open;
            hud.NameSign(tunerDoor.sign, $"Tuning room    {tunerDoor.price}", Profile.Beats >= tunerDoor.price, !open);
            hud.NameSign(tunerStation.sign, "Tuner", false, open);
        }

        // The Archivist's sign says so when it has something new to show.
        void NameArchive()
        {
            int fresh = 0;
            for (int kind = 0; kind < Parts.Kinds; kind++)
            {
                Parts.Found(config, (PartKind)kind, out _, out int some);
                fresh += some;
            }
            hud.NameSign(archiveSign, fresh > 0 ? $"Archivist    {fresh} new" : "Archivist", fresh > 0);
        }

        // ---- Building the room ----

        void BuildRoom(LobbyDef def)
        {
            var floor = new GameObject("Floor");
            floor.transform.SetParent(transform, false);
            floor.AddComponent<MeshFilter>().sharedMesh = GroundBuilder.Round(Radius + 1.5f, 9, 40, 3);
            floor.AddComponent<MeshRenderer>().sharedMaterial = new Material(config.ground)
            {
                mainTexture = GroundBuilder.Palette(def.floor, def.floorAlt, def.floorBand), color = Color.white,
            };
            // The ring of the turntable. Its deck, which turns, belongs to the workshop.
            Place("Turntable", def.turntable, Vector3.zero, Quaternion.identity, def.trim, def.wallMaterial, config.pulse);

            // Outside the gate lies bare, dark ground: the same ground the machine arrives on in the
            // open world, so that driving out of here and arriving there is one unbroken drive.
            var road = GameObject.CreatePrimitive(PrimitiveType.Plane).transform;
            road.name = "Ground outside";
            Destroy(road.GetComponent<Collider>());
            road.SetParent(transform, false);
            road.position = new Vector3(0f, -0.04f, Radius + 100.6f);
            road.localScale = new Vector3(40f, 1f, 20f);
            var scenery = config.scenery;
            var outside = scenery != null && scenery.biomes.Length > 0
                ? scenery.biomes[Biomes.At(Vector3.back * 30f, scenery.biomes.Length)].ground : Tokens.Soot;
            road.GetComponent<MeshRenderer>().sharedMaterial = new Material(config.ground) { color = outside };

            // Fourteen wall pieces in a ring: the gate, the bay doors and plain wall between them.
            // The ring is closed. On the side the camera looks in from, the pieces are cut off low.
            var random = new System.Random(5);
            for (int i = 0; i < Segments; i++)
            {
                Vector3 dir = Direction(i), pos = dir * Radius;
                var facing = Quaternion.LookRotation(-dir);
                if (dir.z < CutawayBelow)
                {
                    Place("Low wall", def.Model("RH_WallLow"), pos, facing, def.wallMaterial, def.trim, def.lampGlow);
                    continue;
                }
                if (i == 0)
                {
                    Place("Gate", def.gate, pos, facing, def.wallMaterial, def.trim, config.pulse);
                    padPos = dir * (Radius - PadInset);
                    padFacing = Quaternion.LookRotation(-dir);
                    signs.Add(("Gate", dir * (Radius - 1.5f) + Vector3.up * 3.4f));
                }
                else if (i % 2 == 0)
                {
                    // A door that leads to a room is built by that room. The rest stay dark until the story opens them.
                    if (HasRoom(i)) continue;
                    Place("Bay " + i / 2, def.bayDoor, pos, facing, def.wallMaterial, def.trim, config.dark);
                }
                else
                {
                    Place("Wall", def.wall, pos, facing, def.wallMaterial, def.trim, def.lampGlow);
                    Vector3 side = Vector3.Cross(Vector3.up, dir);
                    float along = random.Next(2) == 0 ? -1.4f : 1.4f;
                    Vector3 spot = dir * (Radius - 1.7f) + side * along, other = dir * (Radius - 1.7f) - side * along;
                    // Next to the gate, clutter keeps to the far end of its wall piece, clear of the pad.
                    if ((i == 1 || i == Segments - 1) && (other - padPos).sqrMagnitude > (spot - padPos).sqrMagnitude) spot = other;
                    var turned = Quaternion.Euler(0f, random.Next(360), 0f);
                    bool crate = random.Next(2) == 0;
                    Place("Clutter", crate ? def.crate : def.barrel, spot, turned, def.trim, def.wallMaterial, null);
                    // A crate is a box and a barrel is round.
                    if (crate) walk.Block(spot, turned, new Vector2(0.42f, 0.42f));
                    else walk.Block(spot, 0.33f);
                }
            }

            // Where two pieces meet stands a pillar, thicker and higher than both, so that their ends,
            // which cannot fit each other at an angle, disappear inside it. The gate has posts of its own.
            float corner = Radius / Mathf.Cos(Mathf.PI / Segments);
            for (int i = 1; i < Segments - 1; i++)
            {
                float angle = (90f - (i + 0.5f) * 360f / Segments) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                bool low = Direction(i).z < CutawayBelow && Direction(i + 1).z < CutawayBelow;
                Place("Pillar", def.Model(low ? "RH_PillarLow" : "RH_Pillar"), dir * corner, Quaternion.LookRotation(-dir), def.wallMaterial, def.trim, def.lampGlow);
                walk.Block(dir * corner, 0.8f);
            }

            // Four lamps light the floor around the turntable.
            for (int i = 0; i < 4; i++)
            {
                float angle = (45f + i * 90f) * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 7.2f;
                var lamp = Place("Lamp", def.lamp, pos, Quaternion.identity, def.trim, def.wallMaterial, def.lampGlow);
                walk.Block(pos, 0.24f);
                var bulb = new GameObject("Light").AddComponent<Light>();
                bulb.transform.SetParent(lamp, false);
                bulb.transform.localPosition = Vector3.up * 2.5f;
                bulb.type = LightType.Point;
                bulb.color = new Color(1f, 0.8f, 0.55f);
                bulb.range = 11f;
                bulb.intensity = 9f;
            }
        }

        // The departure pad: a dark half circle with a Pulse-green rim along its arc, lying against the
        // gate, and three bars that point at the gate. A green half circle grows out from the gate
        // while the character stands in it, in step with the bar on screen.
        void BuildPad()
        {
            Flat("Pad", HalfDisc(0f), padPos, 0.02f, PadRadius, config.floor);
            Flat("Pad rim", HalfDisc(0.95f), padPos, 0.05f, PadRadius, config.pulse);
            padFill = Flat("Pad fill", HalfDisc(0f), padPos, 0.035f, 0f,
                new Material(config.floor) { color = Color.Lerp(Tokens.Plate2, Tokens.PulseGreen, 0.4f) });
            for (int i = 0; i < 3; i++)
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                bar.name = "Pad mark";
                Destroy(bar.GetComponent<Collider>());
                bar.SetParent(transform, false);
                // The shortest bar is the one nearest the gate, so the three read as an arrow toward it.
                bar.SetPositionAndRotation(padPos + padFacing * new Vector3(0f, 0.05f, PadRadius * (0.3f + i * 0.17f)), padFacing);
                bar.localScale = new Vector3(0.8f + i * 0.5f, 0.02f, 0.16f);
                bar.GetComponent<MeshRenderer>().sharedMaterial = config.part;
            }
        }

        // Half a flat disc of radius 1: the half in front of its own position. innerRatio > 0 leaves
        // only a band along the arc.
        static Mesh HalfDisc(float innerRatio)
        {
            var vertices = new Vector3[(PadSteps + 1) * 2];
            var triangles = new int[PadSteps * 6];
            for (int s = 0; s <= PadSteps; s++)
            {
                float angle = s * Mathf.PI / PadSteps;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[s * 2] = dir * innerRatio;
                vertices[s * 2 + 1] = dir;
                if (s == PadSteps) break;
                int i0 = s * 2, o0 = i0 + 1, i1 = i0 + 2, o1 = i0 + 3, t = s * 6;
                triangles[t] = i0; triangles[t + 1] = o1; triangles[t + 2] = o0;
                triangles[t + 3] = i0; triangles[t + 4] = i1; triangles[t + 5] = o1;
            }
            var mesh = new Mesh { name = "Half disc", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        Transform Flat(string label, Mesh mesh, Vector3 pos, float height, float radius, Material material)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(pos + Vector3.up * height, padFacing);
            go.transform.localScale = new Vector3(radius, 1f, radius);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        Transform Place(string label, Mesh mesh, Vector3 pos, Quaternion rotation, Material baseMaterial, Material accent, Material glow)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(pos, rotation);
            if (mesh == null) return go.transform;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = Slots.For(mesh, baseMaterial, accent, config.dark, glow);
            return go.transform;
        }

        // Piece 0 is the gate at the far side; the others follow clockwise.
        static Vector3 Direction(int segment)
        {
            float angle = (90f - segment * 360f / Segments) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        // ---- The machine on the turntable ----

        // A machine was saved, taken, added or removed, or its frame was upgraded: show the one that
        // is on the turntable now, as it is now.
        void MachineChanged()
        {
            ShowMachine();
            hud.ShowMachine(Profile.Machine);
        }

        void ShowMachine()
        {
            if (display == null)
            {
                display = new GameObject("Machine on the turntable").AddComponent<Rig>();
                // It only stands there. A disabled Rig neither drives nor fires.
                display.enabled = false;
                workshop.Hold(display.transform, 180f);
            }
            display.Build(Profile.Machine, config, refill: true);
        }

        // ---- Walking around ----

        void Update()
        {
            // While a machine's screen is open, it takes all input and the character stands still.
            bool reading = mechanic.IsOpen || presets.IsOpen || archive.IsOpen || smelter.IsOpen || tuner.IsOpen;
            // The pointer is only there while there is something to click.
            Pointer.Show(!busy && (reading || hud.Pause.IsOpen));
            if (busy) return;
            // In a room that is looked into from the side, the camera turns to do so.
            float wantedYaw = 0f;
            foreach (var room in rooms)
                if (room.Holds(character.transform.position)) wantedYaw = room.Yaw;
            viewYaw = Mathf.SmoothDampAngle(viewYaw, wantedYaw, ref yawSpeed, YawTime, Mathf.Infinity, Time.unscaledDeltaTime);
            if (cameraRig != null) cameraRig.yaw = viewYaw;
            character.ViewYaw = viewYaw;
            if (view != null) hud.MoveSigns(view);
            if (hud.Pause.IsOpen) { hud.Pause.Update(); return; }

            character.Frozen = reading;
            hud.Tick(Profile.Beats);
            if (!tuningRoom.IsOpen) NameRoom();
            // What was new has been looked at: the Archivist's sign goes back to its name.
            if (archiveWasOpen && !archive.IsOpen) NameArchive();
            archiveWasOpen = archive.IsOpen;
            if (reading)
            {
                hud.ShowPrompt(null, null);
                if (mechanic.IsOpen) mechanic.Update();
                else if (presets.IsOpen) presets.Update();
                else if (archive.IsOpen) archive.Update();
                else if (smelter.IsOpen) smelter.Update();
                else tuner.Update();
                return;
            }
            if (PauseMenu.Asked()) { hud.Pause.Open("The Roundhouse"); return; }

            Vector3 pos = character.transform.position;
            bool onPad = (padPos - pos).sqrMagnitude < PadRadius * PadRadius;
            // The nearest machine within reach is the one that can be used.
            Station near = null;
            float nearest = StationReach * StationReach;
            foreach (var station in stations)
            {
                float away = (station.pos - pos).sqrMagnitude;
                if (station.gone || away >= nearest) continue;
                nearest = away;
                near = station;
            }
            if (onPad) near = null;

            var keys = Keyboard.current;
            var pad = Gamepad.current;
            // The prompt names the key of whatever the player touched last.
            if (pad != null && pad.wasUpdatedThisFrame && (pad.leftStick.ReadValue().sqrMagnitude > 0.1f || pad.buttonSouth.isPressed)) usingPad = true;
            else if (keys != null && keys.anyKey.isPressed) usingPad = false;
            bool use = (keys != null && (keys.eKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame))
                || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            // A door that costs more than the player has shows its price and cannot be used.
            bool afford = near == null || near.price <= Profile.Beats;
            if (near != null && use && afford) near.open();
            hud.ShowPrompt(near?.name, usingPad ? "A" : "E", near != null ? near.price : 0, afford);

            // Standing on the pad for a moment starts the run.
            gateProgress = onPad ? gateProgress + Time.deltaTime / GateSeconds : 0f;
            hud.ShowGate(onPad ? "Leaving" : null, gateProgress);
            float fill = PadRadius * 0.95f * Mathf.Clamp01(gateProgress);
            padFill.localScale = new Vector3(fill, 1f, fill);
            if (gateProgress >= 1f) StartCoroutine(Leave());
        }

        // The start of a run: the arms fold away, the deck swings the machine round, it comes for the
        // character, the character jumps in, and the machine tears out through the gate into the
        // dark. The open world picks it up from there.
        IEnumerator Leave()
        {
            busy = true;
            character.enabled = false;
            hud.HideForScene();
            var machine = display.transform;
            Vector3 rider = character.transform.position;
            Vector3 toRider = rider - machine.position;
            toRider.y = 0f;
            toRider.Normalize();

            // The camera looks at a point on the floor between the two, so both stay in the picture.
            var focus = new GameObject("Scene focus").transform;
            void Frame(float between, float zoom)
            {
                Vector3 point = Vector3.Lerp(machine.position, rider, between);
                focus.position = new Vector3(point.x, 0f, point.z);
                if (cameraRig != null) cameraRig.zoom = zoom;
            }
            Frame(1f, 1f);
            if (cameraRig != null) cameraRig.target = focus;

            // The arms stop what they are doing and fold away.
            workshop.Running = false;
            for (float t = 0f; t < ClearTime && !workshop.Clear; t += Time.deltaTime)
            {
                Frame(1f - 0.25f * Mathf.Clamp01(t / 0.5f), Mathf.Lerp(1f, SceneZoom, Mathf.Clamp01(t / 0.5f)));
                yield return null;
            }

            // The deck swings the machine round to face the character, who turns to meet it.
            Quaternion facing = Quaternion.LookRotation(toRider);
            Quaternion riderFrom = character.transform.rotation, riderFacing = Quaternion.LookRotation(-toRider);
            float swing = TurnTime * Mathf.Lerp(0.45f, 1f, Quaternion.Angle(machine.rotation, facing) / 180f);
            var turn = StartCoroutine(workshop.Face(facing, swing));
            for (float t = 0f; t < swing; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / swing);
                character.transform.rotation = Quaternion.Slerp(riderFrom, riderFacing, u);
                Frame(0.75f - 0.25f * u, SceneZoom);
                yield return null;
            }
            yield return turn;
            // From here on it drives by itself.
            machine.SetParent(transform, true);
            machine.rotation = facing;

            // It rolls off the turntable and stops just short of the character.
            // It comes down off the deck as it goes.
            Vector3 start = machine.position, stop = rider - toRider * StopShort;
            stop.y = 0f;
            float rolled = 0f, length = (stop - start).magnitude;
            for (float t = 0f; t < RollTime; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / RollTime);
                machine.position = Vector3.Lerp(start, stop, u);
                Frame(0.5f, SceneZoom);
                display.SpinWheels(u * length - rolled);
                rolled = u * length;
                if (Time.frameCount % 3 == 0) Combat.Vfx.Burst(machine.position - toRider * 1.4f + Vector3.up * 0.2f, DustColor, 1, 1.2f, 0.2f, 0.45f);
                yield return null;
            }
            machine.position = stop;

            // The character jumps onto the deck and is gone inside.
            Vector3 deck = stop + Vector3.up * DeckHeight;
            for (float t = 0f; t < JumpTime; t += Time.deltaTime)
            {
                float u = t / JumpTime;
                character.transform.position = Vector3.Lerp(rider, deck, u) + Vector3.up * (JumpHeight * 4f * u * (1f - u));
                character.transform.localScale = Vector3.one * Mathf.Clamp01((1f - u) / 0.3f);
                yield return null;
            }
            character.gameObject.SetActive(false);
            Combat.Vfx.Sparks(deck, Tokens.PulseGreen, 14, 6f, 0.4f, 0.3f);
            if (cameraRig != null) cameraRig.Shake(0.12f);
            // The machine dips under the weight.
            for (float t = 0f; t < 0.22f; t += Time.deltaTime)
            {
                display.Visuals.localPosition = Vector3.down * (0.09f * Mathf.Sin(t / 0.22f * Mathf.PI));
                yield return null;
            }
            display.Visuals.localPosition = Vector3.zero;

            // It pulls away: round toward the gate, through it, and on into the dark. While it does,
            // the camera moves to where the open world's camera will be, the light changes to the
            // light outside, and at the end the dark closes in around the machine. The open world
            // starts from exactly that picture, so the two join without a cut.
            Vector3 offsetFrom = cameraRig != null ? cameraRig.offset : CameraRig.RunOffset;
            var sun = RenderSettings.sun != null ? RenderSettings.sun : FindAnyObjectByType<Light>();
            float sunFrom = sun != null ? sun.intensity : Look.OutdoorSun;
            Color ambientFrom = RenderSettings.ambientLight;
            float speed = 0f;
            for (float t = 0f; t < ExitTime; t += Time.deltaTime)
            {
                float dt = Time.deltaTime, u = t / ExitTime;
                float late = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.95f, u));
                speed += ExitPush * dt;
                bool outside = machine.position.z > Radius;
                Vector3 aim = outside ? Vector3.forward : new Vector3(0f, 0f, Radius + 2f) - machine.position;
                aim.y = 0f;
                machine.rotation = Quaternion.RotateTowards(machine.rotation, Quaternion.LookRotation(aim), ExitTurn * dt);
                machine.position += machine.forward * (speed * dt);
                Frame(0f, Mathf.Lerp(SceneZoom, 1f, late));
                if (cameraRig != null)
                {
                    cameraRig.offset = Vector3.Lerp(offsetFrom, CameraRig.RunOffset, late);
                    cameraRig.pin = late;
                    cameraRig.iris = u < CloseFrom ? float.PositiveInfinity
                        : Mathf.Lerp(CameraRig.IrisWide, CameraRig.IrisTight, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CloseFrom, 1f, u)));
                }
                if (sun != null) sun.intensity = Mathf.Lerp(sunFrom, Look.OutdoorSun, late);
                RenderSettings.ambientLight = Color.Lerp(ambientFrom, Look.OutdoorAmbient, late);
                display.SpinWheels(speed * dt);
                if (dt > 0f)
                {
                    Combat.Vfx.Burst(machine.position - machine.forward * 1.4f + Vector3.up * 0.2f, DustColor, 2, 3f, 0.3f, 0.5f);
                    // Outside there is nothing but the dark, and the air it tears through.
                    if (outside) Combat.Vfx.Rush(machine.position + machine.forward * 10f + Vector3.up * 0.9f, new Vector3(8f, 0.7f, 10f),
                        machine.forward * -3f, Tokens.Dust, 3, 3f, 0.4f);
                }
                yield return null;
            }
            if (cameraRig != null) { cameraRig.iris = CameraRig.IrisTight; cameraRig.pin = 1f; }
            yield return null;
            RunSetup.Arriving = true;
            SceneManager.LoadScene("Arena");
        }

        // Buys the door of the Tuner's room open and plays the scene. The door stands side-on to
        // the hall's camera, which would see nothing of it opening. So for the length of the scene
        // the camera is the scene's own: it swings round in front of the door, watches it open,
        // rises over the room behind it into its usual angle again, and returns to the character.
        void BuyRoom()
        {
            if (tuningRoom.IsOpen || !Profile.Unlock(TuningRoom.Id, tunerDoor.price)) return;
            StartCoroutine(OpenRoom());
        }

        IEnumerator OpenRoom()
        {
            busy = true;
            character.Frozen = true;
            hud.HideForScene();
            workshop.Running = true;
            var focus = new GameObject("Scene focus").transform;
            Vector3 from = character.transform.position, door = tuningRoom.DoorLook, inside = tuningRoom.Middle;
            Vector3 home = cameraRig != null ? cameraRig.offset : Vector3.zero, front = tuningRoom.DoorView;
            Quaternion stood = character.transform.rotation, toDoor = Quaternion.LookRotation(tuningRoom.Outward);
            focus.position = from;
            if (cameraRig != null) cameraRig.target = focus;
            for (float t = 0f; t < SwingTime; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / SwingTime);
                focus.position = Vector3.Lerp(from, door, u);
                // The character turns to watch.
                character.transform.rotation = Quaternion.Slerp(stood, toDoor, Mathf.Clamp01(t / 0.3f));
                if (cameraRig != null)
                {
                    cameraRig.offset = Vector3.Slerp(home, front, u);
                    // Held exactly on what it looks at, so that turning does not swing the picture about.
                    cameraRig.pin = Mathf.Clamp01(t / 0.25f);
                }
                yield return null;
            }
            focus.position = door;
            if (cameraRig != null) { cameraRig.offset = front; cameraRig.pin = 1f; }
            yield return tuningRoom.Unlock(cameraRig);
            yield return new WaitForSeconds(GazeTime);
            for (float t = 0f; t < PeekTime; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / PeekTime);
                focus.position = Vector3.Lerp(door, inside, u);
                if (cameraRig != null) cameraRig.offset = Vector3.Slerp(front, home, u);
                yield return null;
            }
            if (cameraRig != null) cameraRig.offset = home;
            yield return new WaitForSeconds(0.9f);
            for (float t = 0f; t < LookTime; t += Time.deltaTime)
            {
                focus.position = Vector3.Lerp(inside, from, Mathf.SmoothStep(0f, 1f, t / LookTime));
                yield return null;
            }
            if (cameraRig != null) { cameraRig.target = character.transform; cameraRig.pin = 0f; }
            Destroy(focus.gameObject);
            hud.ShowAfterScene();
            NameRoom();
            character.Frozen = false;
            busy = false;
        }

        // Back from a run through the teleporter. The run ended with the dark closing over the place
        // the machine left. Here the dark opens on the empty deck, light pours down onto it, the
        // machine snaps into being, and the character climbs out and jumps down.
        IEnumerator Arrive()
        {
            busy = true;
            workshop.Running = false;
            character.enabled = false;
            character.gameObject.SetActive(false);
            hud.HideForScene();
            display.Visuals.gameObject.SetActive(false);
            Vector3 middle = workshop.Deck.position;
            var focus = new GameObject("Arrival focus").transform;
            focus.position = new Vector3(middle.x, 0f, middle.z);
            if (cameraRig != null) { cameraRig.target = focus; cameraRig.pin = 1f; cameraRig.iris = 0f; cameraRig.Snap(); }
            // Loading leaves one very long frame behind. The scene starts after it.
            yield return null;
            yield return null;

            float owed = 0f;
            for (float t = 0f; t < BeamTime; t += Time.deltaTime)
            {
                float u = t / BeamTime;
                if (cameraRig != null) cameraRig.iris = Mathf.Lerp(0f, CameraRig.IrisWide, u * u);
                // Streaks of light coming down the beam, thicker toward the end, and a little of it gathering on the deck.
                owed += Time.deltaTime * (50f + 90f * u);
                int streaks = (int)owed;
                owed -= streaks;
                if (streaks > 0)
                {
                    Combat.Vfx.Rush(middle + Vector3.up * 5f, new Vector3(0.9f, 5f, 0.9f), Vector3.down * 22f, Tokens.PulseGreen, streaks, 2.4f, 0.25f);
                    Combat.Vfx.Gather(middle + Vector3.up * 1f, 2.6f, Tokens.PulseGreen, Vfx.Share(streaks * 0.3f), 0.3f);
                }
                yield return null;
            }
            if (cameraRig != null) cameraRig.iris = float.PositiveInfinity;

            // It is there: drawn out thin at first, then at its own size, then a short squash as it lands.
            display.Visuals.gameObject.SetActive(true);
            Combat.Vfx.Sparks(middle + Vector3.up, Tokens.PulseGreen, 46, 13f, 0.9f, 0.4f);
            Combat.Vfx.Shockwave(middle, 5f, Tokens.PulseGreen, 44, 0.4f);
            Combat.Vfx.Burst(middle + Vector3.up * 0.4f, DustColor, 22, 5f, 0.25f, 0.7f, falls: true);
            if (cameraRig != null) cameraRig.Shake(0.45f);
            var thin = new Vector3(0.2f, 4.5f, 0.2f);
            for (float t = 0f; t < PopTime; t += Time.deltaTime)
            {
                display.Visuals.localScale = Vector3.Lerp(thin, Vector3.one, Ui.EaseOut(t / PopTime));
                yield return null;
            }
            for (float t = 0f; t < 0.2f; t += Time.deltaTime)
            {
                float squash = Mathf.Sin(t / 0.2f * Mathf.PI);
                display.Visuals.localScale = new Vector3(1f + 0.12f * squash, 1f - 0.24f * squash, 1f + 0.12f * squash);
                yield return null;
            }
            display.Visuals.localScale = Vector3.one;
            yield return new WaitForSeconds(SettleTime);

            // The character climbs out of the top and jumps down toward the camera.
            Vector3 top = middle + Vector3.up * 1.15f, landing = focus.position + Vector3.back * JumpOut;
            character.gameObject.SetActive(true);
            character.transform.rotation = Quaternion.LookRotation(Vector3.back);
            for (float t = 0f; t < LandTime; t += Time.deltaTime)
            {
                float u = t / LandTime;
                character.transform.position = Vector3.Lerp(top, landing, u) + Vector3.up * (JumpHeight * 4f * u * (1f - u));
                character.transform.localScale = Vector3.one * Mathf.Clamp01(u / 0.25f);
                yield return null;
            }
            character.transform.position = landing;
            character.transform.localScale = Vector3.one;
            Combat.Vfx.Burst(landing + Vector3.up * 0.1f, DustColor, 10, 2.5f, 0.16f, 0.5f);
            if (cameraRig != null) cameraRig.Shake(0.1f);

            // The camera goes back to the character, and the place goes back to work.
            for (float t = 0f; t < BackTime; t += Time.deltaTime)
            {
                focus.position = Vector3.Lerp(new Vector3(middle.x, 0f, middle.z), landing, Mathf.SmoothStep(0f, 1f, t / BackTime));
                yield return null;
            }
            if (cameraRig != null) { cameraRig.target = character.transform; cameraRig.pin = 0f; }
            Destroy(focus.gameObject);
            character.enabled = true;
            hud.ShowAfterScene();
            workshop.Running = true;
            busy = false;
        }

        // Public so a test can open and close the screens without a key press.
        public void OpenMechanic() => mechanic.Open();

        public void CloseMechanic() => mechanic.Close();

        public void OpenPresets() => presets.Open();

        public void ClosePresets() => presets.Close();

        public void OpenArchive() => archive.Open();

        public void CloseArchive()
        {
            archive.Close();
            NameArchive();
        }

        public void OpenSmelter() => smelter.Open();

        public void CloseSmelter() => smelter.Close();

        public void OpenTuner() => tuner.Open();

        public void CloseTuner() => tuner.Close();

        // Buys the door of the Tuner's room, if the Beats are there. Also what a test calls.
        public void BuyTuningRoom() => BuyRoom();

        public MechanicScreen Mechanic => mechanic;
        public PresetScreen Presets => presets;
        public ArchiveScreen Archive => archive;
        public SmelterScreen Smelter => smelter;
        public TunerScreen Tuner => tuner;
        public TuningRoom TuningRoom => tuningRoom;
        public IReadOnlyList<LobbyRoom> Rooms => rooms;
        public LobbyWalk Walk => walk;
        public Workshop Workshop => workshop;
        public PauseMenu Pause => hud.Pause;
        // A scene is playing: the machine is leaving or arriving.
        public bool Busy => busy;
    }
}
