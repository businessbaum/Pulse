using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pulse
{
    // The player's machine. Built from a loadout: body, wheels, and one block per filled slot.
    public sealed class Rig : MonoBehaviour
    {
        // FullTurnAt: the share of top speed at which wheels reach their full turn rate.
        const float WheelRadius = 0.35f, ReverseFactor = 0.55f, FullTurnAt = 0.4f;
        // TurnSlowdown: share of speed lost at full steering. CoastFactor: how gently the machine slows down.
        const float TurnSlowdown = 0.12f, CoastFactor = 0.4f;
        const float FlashTime = 0.09f, JoltTime = 0.28f, JoltDistance = 0.2f, JoltTilt = 6f, SquashTime = 0.3f;
        // Bouncing off something hard. HeadOn: how squarely it must be hit; a glancing touch slides along.
        // BounceBack: share of the impact speed it is thrown back with. BounceKeep: share of speed it keeps.
        const float HeadOn = 0.35f, BounceBack = 0.6f, MinBounce = 2.8f, BounceKeep = 0.15f, StaggerTime = 0.32f, ShoveFade = 5f;
        // Nitro. NitroStart: the share of a tank it takes to light it. NitroRest: seconds before it starts
        // to refill. BoostPush: how much harder the machine accelerates while it burns.
        const float NitroStart = 0.2f, NitroRest = 0.6f, BoostPush = 2.2f, BoostZoom = 1.08f;
        // Encore: how far its blast reaches, what it deals, and how long nothing can touch the machine after it.
        const float EncoreReach = 9f, EncoreDamage = 60f, EncoreGuard = 1.2f;
        // Afterburn: how often the burning Nitro leaves fire, in metres, and what that fire is.
        const float BurnEvery = 1.5f, BurnRadius = 1.2f, BurnLasts = 2.6f, BurnDps = 9f;
        static readonly Color DustColor = new Color(0.34f, 0.35f, 0.38f);

        readonly List<WeaponRunner> weapons = new List<WeaponRunner>();
        readonly List<SupportRunner> supports = new List<SupportRunner>();
        readonly List<Transform> wheels = new List<Transform>();
        // Every visible part with its normal materials and an all-white set for the hit flash.
        readonly List<(MeshRenderer renderer, Material[] normal, Material[] white)> skins =
            new List<(MeshRenderer, Material[], Material[])>();
        Transform visuals;
        GameConfig look;
        Vector3 boundsCenter, joltDirection, shove;
        float speed, dustOwed, flashLeft, joltLeft, squashLeft, stagger, launchBrake, boundsRadius = 100f;
        float slowFactor = 1f, slowLeft, nitroRest, mending, guard, burnTrail;
        readonly List<int> blasted = new List<int>(64);
        StateType slowState;
        bool boundsCircular;

        public Loadout Loadout { get; private set; }
        public float Hull { get; private set; }
        public float MaxHull { get; private set; }
        public float Armor { get; private set; }
        public float Radius => 1.5f;
        public bool Alive => Hull > 0f;
        // How full the Nitro tank is, from 0 to 1, and whether it is burning right now.
        public float Nitro { get; private set; } = 1f;
        public bool Boosting { get; private set; }
        // Whether there is enough in the tank to light it.
        public bool NitroReady => Boosting || Nitro >= NitroStart;
        // From how far the machine draws drops in. Zero: it has to drive onto them. Nothing raises
        // this yet; it is where a magnet power-up will plug in.
        public float Magnet { get; set; }
        public bool Moving => Mathf.Abs(speed) > 0.5f;
        public Vector3 Velocity => transform.forward * speed;
        // The part that is drawn. Scripted scenes tilt and shrink it without touching where the machine is.
        public Transform Visuals => visuals;
        // Set while a scripted scene moves the machine: it neither drives nor fires, and nothing can hurt it.
        public bool Scripted { get; set; }
        // Racing in at more than its own top speed, right after arriving.
        public bool Launching { get; private set; }
        // False while nothing can hurt it.
        public bool Exposed => Alive && !Scripted && !Launching;

        public void Build(Loadout loadout, GameConfig config, bool refill)
        {
            Loadout = loadout;
            look = config;
            weapons.Clear();
            supports.Clear();
            wheels.Clear();
            skins.Clear();
            if (visuals != null) Destroy(visuals.gameObject);
            visuals = new GameObject("Visuals").transform;
            visuals.SetParent(transform, false);

            // Models have their origin where they are mounted. The deck is the top of the body.
            var frame = loadout.frame;
            float deck = frame.bodySize.y + WheelRadius;
            Part("Body", frame.bodyMesh, frame.bodySize, new Vector3(0f, frame.bodyMesh != null ? 0f : WheelRadius, 0f),
                config.body, config.part, blockInBase: true);
            foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
            {
                var wheelPos = new Vector3(x * (frame.bodySize.x * 0.5f + 0.12f), 0f, z * frame.bodySize.z * 0.32f);
                if (frame.wheelMesh != null) wheelPos.y = WheelRadius;
                wheels.Add(Part("Wheel", frame.wheelMesh, new Vector3(0.35f, WheelRadius * 2f, 0.8f), wheelPos,
                    config.part, config.body, blockInBase: true));
            }

            // How tough the frame is depends on how far the player has upgraded it.
            int mark = Profile.MarkOf(frame);
            Armor = frame.ArmorAt(mark);
            MaxHull = frame.HullAt(mark);
            mending = 0f;
            foreach (var slot in frame.slots)
            {
                var module = loadout.In(slot.id);
                if (module == null) continue;
                var mods = Wiring.Resolve(loadout, slot.id, Echoes.Has(EchoKind.LongWire));
                // What the level and the ability of this very part make of it.
                var stats = Tuning.Of(module, loadout.ItemIn(slot.id));
                var pos = new Vector3(slot.localPos.x, deck, slot.localPos.z);

                if (module is WeaponDef weapon)
                {
                    var accent = config.StateMaterial(Wiring.EffectiveState(weapon, mods)) ?? config.body;
                    var muzzle = Part(weapon.displayName, weapon.mesh, new Vector3(0.55f, 0.45f, 0.75f), pos, config.part, accent);
                    if (slot.mount == Mount.Rear) muzzle.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    weapons.Add(new WeaponRunner(weapon, slot, mods, stats, this, muzzle));
                }
                else if (module is SupportDef support)
                {
                    var accent = config.StateMaterial(mods.feed) ?? config.body;
                    Part(support.displayName, support.mesh, new Vector3(0.4f, 0.3f, 0.4f), pos, config.part, accent);
                    supports.Add(new SupportRunner(support, mods, stats, this));
                    Armor += stats.armor;
                    MaxHull += stats.hull;
                    mending += stats.mending;
                }
            }
            if (refill || Hull > MaxHull) Hull = MaxHull;
            if (refill) Nitro = 1f;
        }

        // Adds one visible part at its mount point. With a mesh, "Base" faces get the base material and
        // "Accent" faces the accent. Without one, a block of the given size stands in, in the accent
        // colour unless blockInBase is set.
        Transform Part(string label, Mesh mesh, Vector3 blockSize, Vector3 mountPos, Material baseMaterial,
            Material accent, bool blockInBase = false)
        {
            GameObject go;
            if (mesh != null)
            {
                go = new GameObject(label);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = Slots.For(mesh, baseMaterial, accent, look.dark, look.pulse);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = label;
                Destroy(go.GetComponent<Collider>());
                go.GetComponent<MeshRenderer>().sharedMaterial = blockInBase ? baseMaterial : accent;
                go.transform.localScale = blockSize;
                mountPos += Vector3.up * (blockSize.y * 0.5f);
            }
            go.transform.SetParent(visuals, false);
            go.transform.localPosition = mountPos;
            var renderer = go.GetComponent<MeshRenderer>();
            var white = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < white.Length; i++) white[i] = Slots.Or(look.flash, look.body);
            skins.Add((renderer, renderer.sharedMaterials, white));
            return go.transform;
        }

        // The area the machine cannot leave: a square in the open world, a circle in a dungeon.
        public void SetBounds(Vector3 center, float radius, bool circular)
        {
            boundsCenter = center;
            boundsRadius = radius;
            boundsCircular = circular;
        }

        public void Teleport(Vector3 pos)
        {
            transform.position = pos;
            speed = 0f;
            shove = Vector3.zero;
        }

        public void Repair() => Hull = MaxHull;

        public void Heal(float amount)
        {
            if (Alive) Hull = Mathf.Min(MaxHull, Hull + amount);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!Alive || dt <= 0f) return;
            ShowImpacts(dt);
            guard -= dt;
            if (Scripted) return;
            // A plate that mends puts hull back all the time.
            if (mending > 0f && Hull < MaxHull) Hull = Mathf.Min(MaxHull, Hull + mending * dt);
            Drive(dt);
            foreach (var weapon in weapons) weapon.Tick(dt);
            foreach (var support in supports) support.Tick(dt);
        }

        // A hit shows on the machine: sparks and splinters where it landed, a white flash, a jolt away
        // from the attacker and a short camera shake. source names the attacker for the end-of-run screen.
        public void TakeHit(float damage, Vector3 from, string source = null)
        {
            if (!Exposed || guard > 0f) return;
            float taken = Mathf.Max(1f, damage - Armor);
            Hull -= taken;
            RunStats.DamageTaken += taken;
            Echoes.Hurt();
            Vector3 away = transform.position - from;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
            Vector3 contact = transform.position - away * (Radius * 0.8f) + Vector3.up * 0.8f;
            Combat.Vfx.Sparks(contact, Tokens.Chalk, 9, 8f);
            Combat.Vfx.Burst(contact, Tokens.Dust, 4, 4f, 0.14f, 0.45f, falls: true);
            joltDirection = away;
            joltLeft = JoltTime;
            Flash(FlashTime);
            if (Combat.Camera != null) Combat.Camera.Shake(0.22f);
            foreach (var support in supports) support.OnRigHit(from);
            if (Hull <= 0f && !Spared()) Die(source);
        }

        // Damage from standing in something: no jolt and no armour, only a flicker and a few sparks.
        public void Scorch(float damage, StateType state, string source)
        {
            if (!Exposed || guard > 0f) return;
            Hull -= damage;
            RunStats.DamageTaken += damage;
            Echoes.Hurt();
            Combat.Vfx.Sparks(transform.position + Vector3.up * 0.5f, Tokens.Of(state), 5, 5f);
            Flash(FlashTime * 0.6f);
            if (Hull <= 0f && !Spared()) Die(source);
        }

        // Encore: once in a run, the blow that would have destroyed the machine does not. It is left
        // with part of its hull, nothing can touch it for a moment, and everything around it is thrown back.
        bool Spared()
        {
            if (!Echoes.Encore()) return false;
            Hull = MaxHull * Echoes.EncoreHull;
            guard = EncoreGuard;
            Vector3 pos = transform.position;
            Combat.Enemies.Explode(pos, EncoreReach, EncoreDamage, StateType.None, blasted);
            Combat.Vfx.Shockwave(pos, EncoreReach, Tokens.PulseGreen, 60, 0.45f);
            Combat.Vfx.Sparks(pos + Vector3.up, Tokens.PulseGreen, 50, 16f, 1f, 0.5f);
            Combat.Vfx.Burst(pos + Vector3.up, Tokens.Chalk, 30, 10f, 0.3f, 0.8f, falls: true);
            Combat.Marks.Crack(pos, EncoreReach * 0.6f);
            if (Combat.Camera != null) Combat.Camera.Shake(0.7f);
            return true;
        }

        void Die(string source)
        {
            Hull = 0f;
            RunStats.LastHitBy = source;
            visuals.gameObject.SetActive(false);
            Combat.Vfx.Burst(transform.position + Vector3.up, Tokens.Chalk, 60, 11f, 0.4f, 1.2f, falls: true);
            Combat.Vfx.Sparks(transform.position + Vector3.up, Tokens.PulseGreen, 30, 14f, 0.9f, 0.5f);
            Combat.Vfx.Shockwave(transform.position, 8f, Tokens.Dust, 40, 0.45f);
            if (Combat.Camera != null) Combat.Camera.Shake(0.7f);
        }

        void Flash(float time)
        {
            flashLeft = time;
            foreach (var skin in skins) skin.renderer.sharedMaterials = skin.white;
        }

        // Slows the machine for a while: factor is the share of its speed it keeps. A weaker slow does
        // not replace a stronger one that is still running. The state only colours the effect.
        public void Slow(float factor, float seconds, StateType state)
        {
            if (!Exposed || (slowLeft > 0f && factor > slowFactor)) return;
            slowFactor = factor;
            slowLeft = seconds;
            slowState = state;
        }

        // Pushes the machine sideways from outside: a ram, a pull. The push fades by itself.
        public void Shove(Vector3 velocity)
        {
            if (!Exposed) return;
            velocity.y = 0f;
            shove = velocity;
        }

        // Called when the machine runs into something hard at speed. Hit squarely enough, it is thrown
        // back a little and its wheels lose their bite for a moment. Returns false for a glancing touch.
        public bool BounceOff(Vector3 obstacle)
        {
            Vector3 away = transform.position - obstacle;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) return false;
            away.Normalize();
            float headOn = Vector3.Dot(transform.forward * Mathf.Sign(speed), -away);
            if (headOn < HeadOn) return false;

            shove = away * Mathf.Max(MinBounce, Mathf.Abs(speed) * headOn * BounceBack);
            speed *= BounceKeep;
            stagger = StaggerTime;
            Launching = false;
            joltDirection = away;
            joltLeft = JoltTime;
            Combat.Vfx.Sparks(transform.position - away * (Radius * 0.8f) + Vector3.up * 0.7f, Tokens.Chalk, 10, 9f);
            if (Combat.Camera != null) Combat.Camera.Shake(0.2f);
            return true;
        }

        // Starts the machine far above its top speed. It brakes in a straight line until it is down to
        // its own top speed; then the driver takes over.
        public void Launch(float startSpeed, float brake)
        {
            speed = startSpeed;
            launchBrake = brake;
            Launching = true;
        }

        // The machine lands hard: it squashes for an instant.
        public void Thud() => squashLeft = SquashTime;

        public void SpinWheels(float distance)
        {
            float roll = distance / WheelRadius * Mathf.Rad2Deg;
            foreach (var wheel in wheels) wheel.Rotate(roll, 0f, 0f, Space.Self);
        }

        // Plays out the flash, the jolt and the squash of the last impact.
        void ShowImpacts(float dt)
        {
            if (flashLeft > 0f)
            {
                flashLeft -= dt;
                if (flashLeft <= 0f)
                    foreach (var skin in skins) skin.renderer.sharedMaterials = skin.normal;
            }
            if (squashLeft > 0f)
            {
                squashLeft -= dt;
                float squash = Mathf.Sin(Mathf.Clamp01(squashLeft / SquashTime) * Mathf.PI);
                visuals.localScale = new Vector3(1f + 0.12f * squash, 1f - 0.3f * squash, 1f + 0.12f * squash);
            }
            if (joltLeft <= 0f) return;
            joltLeft -= dt;
            float strength = Mathf.Max(0f, joltLeft / JoltTime);
            Vector3 local = transform.InverseTransformDirection(joltDirection);
            visuals.localPosition = local * (strength * JoltDistance);
            visuals.localRotation = Quaternion.AngleAxis(strength * JoltTilt, Vector3.Cross(Vector3.up, local));
        }

        // Driving is relative to the machine, not to the screen: forward and back move it along its own
        // nose, left and right turn it. Wheels need speed to turn and steer mirrored in reverse, like a car.
        void Drive(float dt)
        {
            var drive = Loadout.drive;
            Vector2 input = ReadMove();
            float throttle = input.y, steer = input.x;
            if (stagger > 0f) { stagger -= dt; throttle = 0f; }
            bool slowed = slowLeft > 0f;
            if (slowed)
            {
                slowLeft -= dt;
                if (slowLeft <= 0f) slowFactor = 1f;
            }

            Burn(drive, throttle, dt);
            if (Launching)
            {
                speed = Mathf.MoveTowards(speed, drive.speed, launchBrake * dt);
                steer = 0f;
                // The air it tears through.
                Combat.Vfx.Rush(transform.position + transform.forward * 12f + Vector3.up * 0.9f, new Vector3(9f, 0.7f, 12f),
                    transform.forward * -3f, Tokens.Dust, 2, 3f, 0.4f);
                if (speed <= drive.speed + 0.01f) Launching = false;
            }
            else
            {
                // Steering costs only a little speed, and the machine eases down to it instead of dropping.
                // Something that slows it bites at once.
                float top = drive.speed * (Boosting ? drive.nitroBoost : 1f);
                float wanted = throttle * top * slowFactor * (throttle < 0f ? ReverseFactor : 1f) * (1f - TurnSlowdown * Mathf.Abs(steer));
                bool easing = Mathf.Abs(wanted) < Mathf.Abs(speed) && !slowed;
                speed = Mathf.MoveTowards(speed, wanted, drive.acceleration * (easing ? CoastFactor : Boosting ? BoostPush : 1f) * dt);
            }
            float grip = drive.turnsOnSpot ? 1f : Mathf.Clamp(speed / (drive.speed * FullTurnAt), -1f, 1f);
            transform.Rotate(0f, steer * drive.turnRate * grip * dt, 0f);

            Vector3 before = transform.position;
            Vector3 offset = before + (transform.forward * speed + shove) * dt - boundsCenter;
            shove = Vector3.Lerp(shove, Vector3.zero, 1f - Mathf.Exp(-ShoveFade * dt));
            offset.y = 0f;
            if (boundsCircular) offset = Vector3.ClampMagnitude(offset, boundsRadius);
            else
            {
                offset.x = Mathf.Clamp(offset.x, -boundsRadius, boundsRadius);
                offset.z = Mathf.Clamp(offset.z, -boundsRadius, boundsRadius);
            }
            transform.position = Combat.Scenery.Resolve(boundsCenter + offset, Radius * 0.8f, Mathf.Abs(speed) / drive.speed);
            RunStats.Distance += (transform.position - before).magnitude;
            SpinWheels(speed * dt);

            // A trail of dust while moving fast, thicker while racing in. Whatever slows the machine
            // clings to its wheels in that state's colour.
            dustOwed += Mathf.Abs(speed) > 3f ? dt * (Launching ? 110f : Boosting ? 60f : 24f) : slowed ? dt * 14f : 0f;
            while (dustOwed >= 1f)
            {
                dustOwed -= 1f;
                Vector3 behind = transform.position - transform.forward * Mathf.Sign(speed) * 1.4f + Vector3.up * 0.2f;
                if (slowed) Combat.Vfx.Burst(behind + transform.right * Random.Range(-0.9f, 0.9f), Tokens.Of(slowState), 1, 1.5f, 0.18f, 0.45f);
                else Combat.Vfx.Burst(behind, DustColor, 1, Launching ? 4f : 1.2f, Launching ? 0.34f : 0.22f, 0.5f);
            }
        }

        // Nitro: held while driving forward, it raises the top speed and empties the tank. Let go, the
        // tank refills by itself after a short rest. Lighting it takes a fifth of a tank, so an empty
        // tank cannot be tapped for scraps.
        void Burn(DriveDef drive, float throttle, float dt)
        {
            bool held = ReadBoost() && throttle > 0f && !Launching && stagger <= 0f;
            Boosting = held && (Boosting ? Nitro > 0f : Nitro >= NitroStart);
            if (Boosting)
            {
                Nitro = Mathf.Max(0f, Nitro - dt / drive.nitroSeconds);
                nitroRest = NitroRest;
                // Two jets out of the back, in the colour of the Pulse it burns. Nothing in front of the machine.
                Vector3 back = transform.position - transform.forward * 1.7f + Vector3.up * 0.6f;
                Combat.Vfx.Trail(back + transform.right * 0.6f, transform.forward * -40f, Tokens.PulseGreen, 1.5f, 0.2f);
                Combat.Vfx.Trail(back - transform.right * 0.6f, transform.forward * -40f, Tokens.PulseGreen, 1.5f, 0.2f);
                // Afterburn: what comes out of the back sets the ground alight.
                if (Echoes.Has(EchoKind.Afterburn) && (burnTrail += Mathf.Abs(speed) * dt) >= BurnEvery)
                {
                    burnTrail = 0f;
                    Combat.Zones.Spawn(StateType.Heat, back, BurnRadius, BurnLasts, BurnDps);
                }
            }
            else if ((nitroRest -= dt) <= 0f)
                Nitro = Mathf.Min(1f, Nitro + dt / drive.nitroRecharge * (Echoes.Has(EchoKind.Afterburn) ? 2f : 1f));
            // The camera pulls back a little while it burns.
            if (Combat.Camera != null)
                Combat.Camera.zoom = Mathf.Lerp(Combat.Camera.zoom, Boosting ? BoostZoom : 1f, 1f - Mathf.Exp(-5f * dt));
        }

        // Lets tests hold the Nitro button without an input device.
        public static bool? BoostOverride;

        public static bool ReadBoost()
        {
            if (BoostOverride.HasValue) return BoostOverride.Value;
            var keys = Keyboard.current;
            var pad = Gamepad.current;
            return (keys != null && keys.spaceKey.isPressed)
                || (pad != null && (pad.rightTrigger.isPressed || pad.buttonWest.isPressed));
        }

        // Lets tests and scripted sequences steer the machine without an input device.
        // As with real input, x steers and y is the throttle.
        public static Vector2? MoveOverride;

        public static Vector2 ReadMove()
        {
            if (MoveOverride.HasValue) return Limit(MoveOverride.Value);
            Vector2 move = Vector2.zero;
            var keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.wKey.isPressed || keys.upArrowKey.isPressed) move.y += 1f;
                if (keys.sKey.isPressed || keys.downArrowKey.isPressed) move.y -= 1f;
                if (keys.dKey.isPressed || keys.rightArrowKey.isPressed) move.x += 1f;
                if (keys.aKey.isPressed || keys.leftArrowKey.isPressed) move.x -= 1f;
            }
            var pad = Gamepad.current;
            if (pad != null) move += pad.leftStick.ReadValue();
            return Limit(move);
        }

        // Each axis is limited by itself, so steering at full throttle does not take throttle away.
        static Vector2 Limit(Vector2 move) => new Vector2(Mathf.Clamp(move.x, -1f, 1f), Mathf.Clamp(move.y, -1f, 1f));
    }
}
