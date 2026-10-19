using System.Collections.Generic;
using UnityEngine;

namespace Pulse
{
    // The Mechanic's shop, where the machine is put together. It is open from the start.
    //
    // In it: a lift with a bare frame on it that is being welded, a crane with a reactor block
    // swinging on its chain, the weapons the player owns on stands, gears turning on the back wall,
    // a board of tools, a bench, a roll cabinet, a compressor, a welding set, a stack of wheels,
    // barrels, bins, and the Mechanic in the middle of it.
    public sealed class MechanicRoom : LobbyRoom
    {
        // The welding on the lift: a burst of WeldTime seconds, then a pause.
        const float WeldTime = 1.6f, WeldPause = 2.4f;
        static readonly Color WeldLight = new Color(0.7f, 0.86f, 1f), WarmLight = new Color(1f, 0.8f, 0.55f);

        readonly List<Transform> gears = new List<Transform>(), weapons = new List<Transform>();
        readonly List<float> gearSpeeds = new List<float>();
        Transform load, compressor;
        Light weld;
        Material steel, slate, brass, brassDark;
        Quaternion gearFacing;
        Vector3 weldAt, compressorAt, loadAt;
        float sparkOwed, weldClock;

        protected override Material FloorBase => steel != null ? steel : steel = Tinted(def.wallMaterial, new Color(0.25f, 0.29f, 0.34f));
        protected override Material WallBase => slate != null ? slate : slate = Tinted(def.wallMaterial, new Color(0.2f, 0.27f, 0.31f));

        protected override void Furnish()
        {
            StationPos = Side(2.4f, 3.2f);
            walk.Block(StationPos, 0.62f);
            // Paint, not light: the red of the cabinet, the yellow of the crane, the brass of the gears.
            var red = Tinted(def.wallMaterial, new Color(0.6f, 0.17f, 0.12f));
            var yellow = Tinted(def.wallMaterial, new Color(0.74f, 0.56f, 0.1f));
            var plate = Tinted(def.wallMaterial, new Color(0.4f, 0.43f, 0.47f));
            brass = Tinted(def.wallMaterial, new Color(0.68f, 0.5f, 0.2f));
            brassDark = Tinted(def.wallMaterial, new Color(0.42f, 0.31f, 0.14f));
            Piece(room, "Mechanic", def.mechanic, StationPos, toHall * Quaternion.Euler(0f, wallSide * 20f, 0f), config.pulse, config.body, def.trim);

            // The lift, in the middle of the back half, its ramps toward the door. On it stands a
            // frame the player does not have yet, or the largest there is: what is being built.
            Vector3 liftPos = Side(0.9f, 5.7f);
            Solid("Lift", "MR_Lift", liftPos, toRoom, new Vector2(1.7f, 2.3f), config.shock, def.trim, plate);
            walk.Block(liftPos - outward * 2.8f, toRoom, new Vector2(1.2f, 0.55f));
            FrameDef building = null;
            foreach (var frame in config.frames)
                if (frame != null && frame.bodyMesh != null && (building == null || !Profile.IsFound(frame))) building = frame;
            if (building != null)
                Piece(room, "Frame on the lift", building.bodyMesh, liftPos + Vector3.up * 1.05f, toHall, config.pulse, config.body, config.part);
            weldAt = liftPos + Vector3.up * 1.55f + across * (wallSide * -0.7f) + outward * 0.6f;

            // The crane behind it, its boom over the end of the lift, with a reactor block on the chain.
            Vector3 hoistPos = Side(-2.3f, 7.3f);
            var hoistFacing = toHall * Quaternion.Euler(0f, wallSide * 30f, 0f);
            var hoist = Solid("Crane", "MR_Hoist", hoistPos, hoistFacing, new Vector2(0.65f, 1.15f), config.shock, def.trim, yellow, new Vector2(0f, 0.5f));
            loadAt = hoist.TransformPoint(new Vector3(0f, 2.82f, 1.34f));
            load = Piece(room, "Load", "MR_Load", loadAt, hoistFacing, config.pulse, config.part, def.trim);

            // The back wall: the player's weapons on stands, gears that turn, a board of tools.
            var stand = Solid("Weapon stand", "MR_WeaponStand", Side(-2.9f, Back - 0.78f), toHall, new Vector2(1.5f, 0.4f), config.pulse, config.body, def.trim);
            var owned = new List<ModuleDef>();
            foreach (var item in Profile.Items)
                if (item.def is WeaponDef && item.def.mesh != null && !owned.Contains(item.def)) owned.Add(item.def);
            for (int i = 0; i < 3 && i < owned.Count; i++)
            {
                var mesh = owned[i].mesh;
                var shown = Piece(room, owned[i].displayName, mesh, stand.TransformPoint(new Vector3(-1f + i, 1.02f, 0f)), toHall,
                    config.pulse, config.part, config.body);
                weapons.Add(shown);
            }
            Piece(room, "Tools", "TR_ToolWall", Side(3.0f, Back - 0.3f), toHall);
            walk.Block(Side(3.0f, Back - 0.4f), toHall, new Vector2(1.65f, 0.3f));
            gearFacing = toHall;
            Gear(Side(0.2f, Back - 0.34f, 3.35f), 1f, 14f);
            Gear(Side(1.45f, Back - 0.34f, 2.55f), 0.55f, -14f / 0.55f);
            Gear(Side(-0.95f, Back - 0.34f, 2.2f), 0.7f, -14f / 0.7f);

            // The side wall: a bench, a roll cabinet, a barrel of oil by the door.
            walk.Block(Side(4.45f, 6.5f), fromWall, new Vector2(1.15f, 0.5f));
            Piece(room, "Bench", def.workbench, Side(4.45f, 6.5f), fromWall, def.lampGlow, def.trim, def.wallMaterial);
            Solid("Tool chest", "MR_ToolChest", Side(4.5f, 4.2f), fromWall, new Vector2(0.65f, 0.36f), config.shock, config.body, red);
            Round("Barrel", "Barrel", Side(4.45f, 1.7f), Quaternion.identity, 0.34f);
            Round("Barrel", "Barrel", Side(3.75f, 1.55f), Quaternion.Euler(0f, 40f, 0f), 0.34f);

            // The kerb side, low enough to look over: a compressor, wheels, bins, a welding set.
            compressorAt = Side(-4.3f, 7.4f);
            compressor = Solid("Compressor", "MR_Compressor", compressorAt, fromKerb, new Vector2(1f, 0.48f), config.shock, config.body, red);
            Vector3 wheels = Side(-4.3f, 5.2f);
            var wheel = Profile.Machine.frame.wheelMesh;
            if (wheel != null)
            {
                for (int i = 0; i < 3; i++)
                    Piece(room, "Wheel", wheel, wheels + Vector3.up * (0.2f + i * 0.36f), Quaternion.Euler(0f, i * 25f, 90f), config.pulse, config.dark, config.part);
                Piece(room, "Wheel", wheel, wheels + outward * 0.9f + Vector3.up * 0.45f, fromKerb * Quaternion.Euler(-14f, 90f, 0f), config.pulse, config.dark, config.part);
                walk.Block(wheels + outward * 0.35f, toHall, new Vector2(0.55f, 0.85f));
            }
            Solid("Bins", "TR_Bins", Side(-4.25f, 2.6f), fromKerb, new Vector2(1f, 0.42f));
            Round("Welding set", "MR_Welder", Side(-1.5f, 4.1f), toHall * Quaternion.Euler(0f, wallSide * -50f, 0f), 0.5f, config.shock, def.trim, red);
            Solid("Things on the floor", "MR_Clutter", Side(-1.9f, 1.9f), toHall * Quaternion.Euler(0f, wallSide * 15f, 0f), new Vector2(1.1f, 0.5f), config.shock, def.trim, config.body);

            // Light: white over the lift, warm at the bench, the Pulse at the stands, and the welding.
            AddLight("Lift lamp", liftPos + Vector3.up * 4.4f + outward * -1.5f, new Color(0.95f, 0.97f, 1f), 12f, 4.2f);
            AddLight("Bench lamp", Side(3.6f, 6.4f, 2f), WarmLight, 6.5f, 5f);
            AddLight("Stand lamp", Side(-2.9f, Back - 1.6f, 2.6f), new Color(0.45f, 1f, 0.7f), 5f, 1.3f);
            AddLight("Door lamp", Side(-1f, 2.2f, 2.6f), WarmLight, 7f, 3.5f);
            weld = AddLight("Weld", weldAt, WeldLight, 5f, 0f);
        }

        // A gear on the back wall. speed: degrees a second; gears that mesh turn against each other,
        // the smaller the faster.
        void Gear(Vector3 pos, float size, float speed)
        {
            var gear = Piece(room, "Gear", "MR_Gear", pos, gearFacing, config.pulse, brassDark, brass);
            gear.localScale = Vector3.one * size;
            gears.Add(gear);
            gearSpeeds.Add(speed);
        }

        protected override void Run(float dt)
        {
            for (int i = 0; i < gears.Count; i++)
                gears[i].rotation = gearFacing * Quaternion.Euler(0f, 0f, clock * gearSpeeds[i] + i * 11f);
            // The weapons turn slowly on their stands, so all of each is seen.
            for (int i = 0; i < weapons.Count; i++)
                weapons[i].rotation = Quaternion.Euler(0f, clock * 24f + i * 120f, 0f);
            // What hangs on the chain never hangs quite still.
            if (load != null)
                load.rotation = Quaternion.Euler(Mathf.Sin(clock * 1.3f) * 3.5f, clock * 6f, Mathf.Sin(clock * 0.9f + 1f) * 2.5f);
            // The compressor shakes on its feet.
            if (compressor != null) compressor.position = compressorAt + new Vector3(Mathf.Sin(clock * 47f), 0f, Mathf.Cos(clock * 53f)) * 0.006f;

            // Welding on the lift: a burst of sparks and a hard blue light, then a pause.
            weldClock += dt;
            bool welding = Mathf.Repeat(weldClock, WeldTime + WeldPause) < WeldTime;
            if (welding)
            {
                sparkOwed += dt * 38f;
                int sparks = (int)sparkOwed;
                sparkOwed -= sparks;
                if (sparks > 0) Combat.Vfx.Sparks(weldAt, Random.value < 0.6f ? Tokens.Chalk : Tokens.Shock, sparks, 6f, 0.3f, 0.3f);
            }
            if (weld != null) weld.intensity = lit * (welding ? Random.Range(3f, 9f) : 0f);
        }
    }
}
