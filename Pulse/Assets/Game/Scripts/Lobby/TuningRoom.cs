using UnityEngine;

namespace Pulse
{
    // The Tuner's room, where parts are raised to a higher level. Its door is locked until the
    // player buys it open, and until then nothing of the room exists to be seen.
    //
    // In it: a press that comes down every few seconds on one of the player's own parts, the Tuner
    // next to it, racks of parts and a board of tools along the back wall, a desk with screens, a
    // bench, bottles of gas, bins, crates, a reel of cable and a vent that lets off steam.
    public sealed class TuningRoom : LobbyRoom
    {
        public const string Id = "tuning-room";

        // The press: one stroke every Cycle seconds. It comes down, stays down while the part is
        // worked, and goes back up.
        const float Cycle = 5.2f, DownTime = 0.5f, HoldTime = 1.3f, UpTime = 0.9f, Stroke = 0.62f;
        static readonly Color Steam = new Color(0.62f, 0.64f, 0.67f);

        Transform head, part, armLeft, armRight;
        Light glow, screen;
        Vector3 ventTop, partRest;
        float steamOwed, sparkOwed;

        // Where the Tuner stands.
        public Vector3 TunerPos => StationPos;

        protected override string FloorModel => "TR_Floor";

        protected override void Furnish()
        {
            StationPos = At(2.7f, 4.5f);

            // The press stands in the middle of the back half, facing the door. Nothing hangs over it:
            // the camera looks down into the room, and a beam across it would only be in the way.
            Vector3 pressPos = At(0f, 6.3f);
            var press = Solid("Press", "TR_Press", pressPos, toHall, new Vector2(1.7f, 1.15f));
            head = Piece(press, "Press head", "TR_PressHead", pressPos + toHall * new Vector3(0f, 3.55f, -0.55f), toHall);

            // The part on the press: one of the player's own, turning slowly in the light.
            partRest = pressPos + toHall * new Vector3(0f, 1.2f, 0.1f);
            Mesh shown = null;
            foreach (var item in Profile.Items)
                if (item.def is WeaponDef && item.def.mesh != null) { shown = item.def.mesh; break; }
            part = new GameObject("Part on the press").transform;
            part.SetParent(room, true);
            part.position = partRest;
            if (shown != null)
            {
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = shown;
                part.gameObject.AddComponent<MeshRenderer>().sharedMaterials = Slots.For(shown, config.part, config.body, config.dark, config.pulse);
            }

            // The Tuner next to it.
            var tuner = Round("Tuner", "NPC_Tuner", StationPos, toHall * Quaternion.Euler(0f, 25f, 0f), 0.62f, config.pulse, config.body, def.trim);
            armLeft = Piece(tuner, "Arm", "NPC_Tuner_Arm", tuner.TransformPoint(new Vector3(-0.58f, 1.05f, 0.1f)), tuner.rotation, config.pulse, config.body, def.trim);
            armRight = Piece(tuner, "Arm", "NPC_Tuner_Arm", tuner.TransformPoint(new Vector3(0.58f, 1.05f, 0.1f)), tuner.rotation, config.pulse, config.body, def.trim);

            // Along the back wall: two racks of parts, tools between them, a desk with screens, a vent.
            foreach (float side in new[] { -3.35f, 3.2f })
                Solid("Shelf", "TR_Shelf", At(side, Back - 0.75f), toHall, new Vector2(1.3f, 0.42f), def.lampGlow);
            Piece(room, "Tools", "TR_ToolWall", At(0f, Back - 0.3f), toHall);
            walk.Block(At(0f, Back - 0.4f), toHall, new Vector2(1.65f, 0.3f));
            Solid("Console", "TR_Console", Side(2.9f, 5.2f), toHall * Quaternion.Euler(0f, wallSide * -28f, 0f), new Vector2(1.2f, 0.5f), config.flash);
            Vector3 ventPos = Side(-4.55f, Back - 0.5f);
            Solid("Vent", "TR_Vent", ventPos, toHall, new Vector2(0.55f, 0.35f));
            ventTop = ventPos + Vector3.up * 0.75f + toHall * new Vector3(-0.2f, 0f, 0.05f);

            // Along the side wall: a bench with a vise, bottles of gas, and a barrel by the door.
            Solid("Bench", "TR_Bench", Side(4.4f, 3.3f), fromWall, new Vector2(1.3f, 0.52f), def.lampGlow);
            Solid("Tanks", "TR_Tanks", Side(4.55f, 7.2f), fromWall, new Vector2(0.8f, 0.36f));
            Round("Barrel", "Barrel", Side(4.45f, 1.6f), Quaternion.identity, 0.34f);

            // Along the kerb, low enough to look over: bins, crates, a reel of cable.
            Solid("Bins", "TR_Bins", Side(-4.25f, 2.3f), fromKerb, new Vector2(1f, 0.42f));
            Vector3 crates = Side(-4.3f, 4.5f);
            Piece(room, "Crate", "Crate", crates, Quaternion.Euler(0f, 12f, 0f));
            Piece(room, "Crate", "Crate", crates + outward * 0.95f, Quaternion.Euler(0f, -8f, 0f));
            Piece(room, "Crate", "Crate", crates + outward * 0.45f + Vector3.up * 0.8f, Quaternion.Euler(0f, 30f, 0f));
            walk.Block(crates + outward * 0.47f, toHall, new Vector2(0.5f, 0.95f));
            Solid("Reel", "TR_Reel", Side(-4.2f, 7.0f), fromKerb * Quaternion.Euler(0f, 18f, 0f), new Vector2(0.55f, 0.65f));

            // Light: white from the lamps over the press, warm at the bench, cold from the screens,
            // and the green of the Pulse at the press itself.
            AddLight("Press lamp", At(0f, 5.75f, 3.1f), new Color(0.95f, 0.97f, 1f), 11f, 9f);
            AddLight("Bench lamp", Side(3.6f, 3.3f, 2f), new Color(1f, 0.8f, 0.55f), 6.5f, 5f);
            screen = AddLight("Screens", Side(2.6f, 5f, 1.7f), new Color(0.55f, 0.8f, 1f), 4.5f, 2.2f);
            glow = AddLight("Press glow", partRest, new Color(0.45f, 1f, 0.7f), 4f, 1.2f);
        }

        protected override void Run(float dt)
        {
            // The press: down, hold, up, rest.
            float at = Mathf.Repeat(clock, Cycle), down;
            if (at < DownTime) down = Mathf.SmoothStep(0f, 1f, at / DownTime);
            else if (at < DownTime + HoldTime) down = 1f;
            else if (at < DownTime + HoldTime + UpTime) down = 1f - Mathf.SmoothStep(0f, 1f, (at - DownTime - HoldTime) / UpTime);
            else down = 0f;
            bool working = at > DownTime && at < DownTime + HoldTime;
            if (head != null) head.localPosition = new Vector3(0f, 3.55f - Stroke * down, -0.55f);

            // The part turns all the time, and shakes and throws sparks while the press is on it.
            if (part != null)
            {
                part.rotation = Quaternion.Euler(0f, clock * 40f, 0f);
                part.position = partRest + Vector3.up * (Mathf.Sin(clock * 1.7f) * 0.05f - 0.12f * down)
                    + (working ? Random.insideUnitSphere * 0.012f : Vector3.zero);
            }
            if (working)
            {
                sparkOwed += dt * 30f;
                int sparks = (int)sparkOwed;
                sparkOwed -= sparks;
                if (sparks > 0) Combat.Vfx.Sparks(partRest + Vector3.up * 0.2f, Random.value < 0.7f ? Tokens.PulseGreen : Tokens.Chalk, sparks, 5f, 0.3f, 0.25f);
            }
            if (glow != null) glow.intensity = lit * (working ? Random.Range(2.2f, 4.5f) : 1.1f + 0.3f * Mathf.Sin(clock * 2.1f));
            if (screen != null) screen.intensity = lit * (2f + 0.25f * Mathf.Sin(clock * 9f) + (Random.value < 0.02f ? 0.8f : 0f));

            // Steam out of the vent, without a break, so it never puffs.
            steamOwed += dt * 7f;
            int wisps = (int)steamOwed;
            steamOwed -= wisps;
            if (wisps > 0) Combat.Vfx.Rise(ventTop, 0.14f, 0f, Steam, wisps, 1.5f, 0.2f, 1.3f);

            // The Tuner's arms never stop.
            if (armLeft != null)
            {
                armLeft.localRotation = Quaternion.Euler(-20f + Mathf.Sin(clock * 3.1f) * 22f, -18f + Mathf.Sin(clock * 1.3f) * 10f, 0f);
                armRight.localRotation = Quaternion.Euler(-28f + Mathf.Sin(clock * 2.3f + 1f) * 26f, 16f + Mathf.Sin(clock * 1.7f) * 12f, 0f);
            }
        }
    }
}
