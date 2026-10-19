using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Pulse
{
    // The middle of the Roundhouse: the deck of the turntable, which turns with the machine on it,
    // and four arms around it that never stop working on the machine. Two weld and two tighten.
    //
    // It runs in a loop. The arms work for a while, each picking one spot on the machine after
    // another. Then they lift clear, the deck turns the machine to a new side, and they start again.
    // When the machine is about to leave, the arms fold away and stay there.
    public sealed class Workshop : MonoBehaviour
    {
        sealed class Arm
        {
            public Transform turret, upper, fore, tool;
            public Light glow;
            public Vector3 shoulder, rest, tip, from, to, job;
            public float clock, span, burst, sparkOwed;
            public int state;
            public bool welder;
        }

        const int Resting = 0, Reaching = 1, Working = 2, Lifting = 3;
        // The arm: how high its shoulder is, and how long its three pieces are. The models are built to these.
        const float ShoulderHeight = 1f, UpperLength = 2f, ForeLength = 1.6f, ToolLength = 0.4f;
        const float ArmRadius = 3.3f, DeckHeight = 0.2f, DeckTop = 0.09f;
        // The loop: how long the arms work, how long the deck takes to turn, and how far it turns.
        const float WorkTime = 6.5f, TurnTime = 1.9f, MinTurn = 50f, MaxTurn = 110f;
        const float ReachTime = 0.65f, LiftTime = 0.3f, RestTime = 0.5f;
        // Where the arms stand, in degrees. The gate is at 90 and the camera at 270; both stay free,
        // so the machine can drive out and the character can jump down.
        static readonly float[] Angles = { 35f, 145f, 215f, 325f };
        static readonly Color WeldLight = new Color(0.7f, 0.86f, 1f), DustColor = new Color(0.4f, 0.4f, 0.42f);

        readonly List<Arm> arms = new List<Arm>();
        Transform machine;
        float clock, turnFrom, turnTo;
        // turning: the loop is turning the deck. facing: a scene is.
        bool turning, facing;

        // The part of the turntable that turns.
        public Transform Deck { get; private set; }
        // Off: the arms fold away and the deck stands still.
        public bool Running { get; set; } = true;
        // Every arm is folded away and nothing moves.
        public bool Clear
        {
            get
            {
                foreach (var arm in arms)
                    if (arm.state != Resting || arm.clock < arm.span) return false;
                return !turning && !facing;
            }
        }

        public void Build(LobbyDef def, GameConfig config)
        {
            Deck = Piece("Deck", def.deck, transform, new Vector3(0f, DeckHeight, 0f), def.trim, def.wallMaterial, config.dark, config.pulse);
            for (int i = 0; i < Angles.Length; i++)
            {
                float angle = Angles[i] * Mathf.Deg2Rad;
                Vector3 foot = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ArmRadius + Vector3.up * 0.2f;
                var arm = new Arm { welder = i % 2 == 0, shoulder = foot + Vector3.up * ShoulderHeight };
                Piece("Arm " + (i + 1), def.armBase, transform, foot, def.trim, def.wallMaterial, config.dark, config.pulse);
                arm.turret = Piece("Turret", def.armTurret, transform, arm.shoulder, config.body, def.trim, config.dark, config.pulse);
                arm.upper = Piece("Upper arm", def.armUpper, arm.turret, Vector3.zero, config.body, def.trim, config.dark, config.pulse);
                arm.fore = Piece("Forearm", def.armFore, arm.upper, Vector3.forward * UpperLength, config.body, def.trim, config.dark, config.pulse);
                arm.tool = Piece("Tool", arm.welder ? def.armWelder : def.armClaw, arm.fore, Vector3.forward * ForeLength,
                    def.trim, config.body, config.dark, arm.welder ? config.shock : config.pulse);
                if (arm.welder)
                {
                    arm.glow = new GameObject("Weld light").AddComponent<Light>();
                    arm.glow.transform.SetParent(arm.tool, false);
                    arm.glow.transform.localPosition = Vector3.forward * ToolLength;
                    arm.glow.type = LightType.Point;
                    arm.glow.color = WeldLight;
                    arm.glow.range = 3.2f;
                    arm.glow.intensity = 0f;
                }
                // Folded: the upper arm nearly upright, the tool held high over the edge of the deck.
                Vector3 inward = -new Vector3(foot.x, 0f, foot.z).normalized;
                arm.rest = arm.shoulder + inward * 1.6f + Vector3.up * 2.1f;
                arm.tip = arm.from = arm.to = arm.rest;
                arm.clock = arm.span = RestTime;
                Pose(arm, arm.rest);
                arms.Add(arm);
            }
            clock = Random.Range(0f, 1.5f);
        }

        static Transform Piece(string label, Mesh mesh, Transform parent, Vector3 localPos, Material baseMaterial, Material accent,
            Material dark, Material glow)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            if (mesh == null) return go.transform;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = Slots.For(mesh, baseMaterial, accent, dark, glow);
            return go.transform;
        }

        // Stands a machine on the deck, facing the way the deck faces.
        public void Hold(Transform held, float turned)
        {
            machine = held;
            held.SetParent(Deck, false);
            held.localPosition = Vector3.up * DeckTop;
            held.localRotation = Quaternion.Euler(0f, turned, 0f);
        }

        // Where the arms stand, so the character can be kept out of them.
        public IEnumerable<Vector3> Feet()
        {
            foreach (float degrees in Angles)
            {
                float angle = degrees * Mathf.Deg2Rad;
                yield return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ArmRadius;
            }
        }

        // Turns the deck, and the machine with it, until the machine faces a direction. For the
        // scene in which the machine leaves; the arms must be clear by then.
        public IEnumerator Face(Quaternion direction, float seconds)
        {
            facing = true;
            Quaternion from = Deck.rotation, to = direction * Quaternion.Inverse(machine != null ? machine.localRotation : Quaternion.identity);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                Deck.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            Deck.rotation = to;
            facing = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // The loop of the whole place: work, lift clear, turn, work again. A turn that has begun
            // is finished even when the place is told to stop.
            if (turning)
            {
                clock += dt;
                float u = Mathf.Clamp01(clock / TurnTime);
                Deck.localRotation = Quaternion.Euler(0f, Mathf.Lerp(turnFrom, turnTo, Mathf.SmoothStep(0f, 1f, u)), 0f);
                if (u >= 1f) { turning = false; clock = 0f; }
            }
            else if (Running && !facing)
            {
                clock += dt;
                if (clock >= WorkTime && AllResting())
                {
                    turning = true;
                    clock = 0f;
                    turnFrom = Deck.localEulerAngles.y;
                    turnTo = turnFrom + Random.Range(MinTurn, MaxTurn) * (Random.value < 0.5f ? -1f : 1f);
                }
            }

            bool mayWork = Running && !turning && !facing && clock < WorkTime && machine != null && machine.gameObject.activeInHierarchy;
            foreach (var arm in arms) Tick(arm, dt, mayWork);
        }

        bool AllResting()
        {
            foreach (var arm in arms)
                if (arm.state != Resting || arm.clock < arm.span) return false;
            return true;
        }

        // One arm: it reaches for a spot on the machine, works on it, lifts off, and picks the next.
        // The spot is fixed to the machine, so the arm follows it if the machine moves under it.
        void Tick(Arm arm, float dt, bool mayWork)
        {
            arm.clock += dt;
            float u = Mathf.Clamp01(arm.clock / Mathf.Max(0.01f, arm.span));
            bool lit = false;
            switch (arm.state)
            {
                case Resting:
                    arm.tip = Vector3.Lerp(arm.from, arm.rest, Mathf.SmoothStep(0f, 1f, u));
                    if (mayWork && u >= 1f && FindJob(arm)) Go(arm, Reaching, ReachTime * Random.Range(0.85f, 1.3f));
                    break;

                case Reaching:
                    arm.tip = Vector3.Lerp(arm.from, machine.TransformPoint(arm.job), Mathf.SmoothStep(0f, 1f, u));
                    if (!mayWork) Go(arm, Resting, RestTime);
                    else if (u >= 1f) Go(arm, Working, Random.Range(1.3f, 2.6f));
                    break;

                case Working:
                    Vector3 spot = machine.TransformPoint(arm.job);
                    arm.tip = spot;
                    lit = Work(arm, spot, dt);
                    if (!mayWork || u >= 1f || !Reaches(arm, spot)) Go(arm, Lifting, LiftTime);
                    break;

                default:
                    arm.tip = Vector3.Lerp(arm.from, arm.to, Mathf.SmoothStep(0f, 1f, u));
                    if (u < 1f) break;
                    if (mayWork && FindJob(arm)) Go(arm, Reaching, ReachTime * Random.Range(0.7f, 1.1f));
                    else Go(arm, Resting, RestTime);
                    break;
            }
            if (arm.glow != null) arm.glow.intensity = lit ? Random.Range(0.7f, 2f) : 0f;
            Pose(arm, arm.tip);
            // The claw turns back and forth on whatever it has hold of.
            if (!arm.welder)
                arm.tool.localRotation = Quaternion.Euler(0f, 0f, arm.state == Working ? Mathf.Sin(Time.time * 11f) * 50f : 0f);
        }

        void Go(Arm arm, int state, float seconds)
        {
            arm.state = state;
            arm.clock = 0f;
            arm.span = seconds;
            arm.from = arm.tip;
            // Lifting means straight up off the machine, and a little back toward the arm's own foot.
            if (state == Lifting) arm.to = arm.tip + Vector3.up * 0.55f + (arm.shoulder - arm.tip).normalized * 0.25f;
            arm.burst = 0f;
        }

        // A welder throws sparks in short bursts, with its lamp flickering. A claw only raises a little dust.
        // Returns whether the arm is giving off light right now.
        bool Work(Arm arm, Vector3 spot, float dt)
        {
            if (!arm.welder)
            {
                if (Random.value < dt * 2.5f) Combat.Vfx.Burst(spot, DustColor, 1, 0.9f, 0.07f, 0.35f, falls: true);
                return false;
            }
            // On for a moment, off for a moment.
            arm.burst -= dt;
            if (arm.burst < -0.25f) arm.burst = Random.Range(0.25f, 0.7f);
            if (arm.burst <= 0f) return false;
            arm.sparkOwed += dt * 34f;
            int sparks = (int)arm.sparkOwed;
            arm.sparkOwed -= sparks;
            if (sparks > 0) Combat.Vfx.Sparks(spot, Random.value < 0.6f ? Tokens.Shock : Tokens.Chalk, sparks, 4.5f, 0.22f, 0.2f);
            return true;
        }

        // Picks a spot on the top or the sides of the machine that this arm can get to. With the deck
        // turned differently, different spots are within its reach.
        bool FindJob(Arm arm)
        {
            for (int tries = 0; tries < 12; tries++)
            {
                var local = Random.value < 0.6f
                    ? new Vector3(Random.Range(-0.7f, 0.7f), Random.Range(0.95f, 1.2f), Random.Range(-1.1f, 1.1f))
                    : new Vector3((Random.value < 0.5f ? -1f : 1f) * Random.Range(0.8f, 0.95f), Random.Range(0.5f, 0.85f), Random.Range(-1f, 1f));
                Vector3 spot = machine.TransformPoint(local);
                // Only spots on its own side of the machine, so no arm reaches across.
                Vector3 toArm = arm.shoulder - machine.position, toSpot = spot - machine.position;
                toArm.y = toSpot.y = 0f;
                if (Vector3.Dot(toArm.normalized, toSpot) < 0.1f || !Reaches(arm, spot)) continue;
                arm.job = local;
                return true;
            }
            return false;
        }

        static bool Reaches(Arm arm, Vector3 spot)
        {
            float away = (spot - arm.shoulder).magnitude;
            return away > 1.2f && away < UpperLength + ForeLength + ToolLength - 0.12f;
        }

        // Bends the arm so that the end of its tool is at a point: the turret turns toward it, and
        // shoulder and elbow fold as far as the distance needs, with the elbow up.
        static void Pose(Arm arm, Vector3 point)
        {
            Vector3 to = point - arm.shoulder;
            float flat = Mathf.Sqrt(to.x * to.x + to.z * to.z), lower = ForeLength + ToolLength;
            float reach = Mathf.Clamp(to.magnitude, Mathf.Abs(UpperLength - lower) + 0.05f, UpperLength + lower - 0.02f);
            float toPoint = Mathf.Atan2(to.y, flat);
            float shoulder = Mathf.Acos(Mathf.Clamp((UpperLength * UpperLength + reach * reach - lower * lower) / (2f * UpperLength * reach), -1f, 1f));
            float elbow = Mathf.Acos(Mathf.Clamp((UpperLength * UpperLength + lower * lower - reach * reach) / (2f * UpperLength * lower), -1f, 1f));
            if (flat > 0.01f) arm.turret.rotation = Quaternion.Euler(0f, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 0f);
            arm.upper.localRotation = Quaternion.Euler(-(toPoint + shoulder) * Mathf.Rad2Deg, 0f, 0f);
            arm.fore.localRotation = Quaternion.Euler(180f - elbow * Mathf.Rad2Deg, 0f, 0f);
        }
    }
}
