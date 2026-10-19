using System.Collections.Generic;
using UnityEngine;

namespace Pulse
{
    // The Archivist's archive, where everything that was ever found is kept. It is open from the start.
    //
    // In it: walls of small drawers with a ladder that rolls along them, a cabinet of cards, a
    // machine that remembers on two reels of tape, glass cases with parts the player has found, a
    // table that shows them one after another as pictures of light, a reading desk, a bin of
    // rolled plans, and the Archivist among it.
    public sealed class ArchiveRoom : LobbyRoom
    {
        // The table shows each part for ShowTime seconds; it shrinks away and the next grows in SwapTime.
        const float ShowTime = 5f, SwapTime = 0.45f, LadderReach = 1.9f;
        static readonly Color WarmLight = new Color(1f, 0.82f, 0.6f), PulseLight = new Color(0.45f, 1f, 0.7f);

        readonly List<Transform> reels = new List<Transform>(), cased = new List<Transform>();
        readonly List<Mesh> found = new List<Mesh>();
        Transform ladder, picture;
        MeshFilter pictureMesh;
        MeshRenderer pictureLook;
        Light beam;
        Material wood, woodDark, green;
        Quaternion reelFacing;
        Vector3 ladderHome, pictureAt;
        int showing = -1;

        protected override Material FloorBase => wood != null ? wood : wood = Tinted(def.wallMaterial, new Color(0.36f, 0.26f, 0.18f));
        protected override Material FloorAccent => woodDark != null ? woodDark : woodDark = Tinted(def.wallMaterial, new Color(0.25f, 0.18f, 0.13f));
        protected override Material WallBase => green != null ? green : green = Tinted(def.wallMaterial, new Color(0.15f, 0.26f, 0.23f));

        protected override void Furnish()
        {
            StationPos = Side(1.7f, 3.1f);
            walk.Block(StationPos, 0.42f);
            Piece(room, "Archivist", def.archivist, StationPos, toHall * Quaternion.Euler(0f, wallSide * 18f, 0f), config.pulse, config.body, def.trim);

            // What the player has found, for the cases and the table: every part with a model of its own.
            foreach (var module in config.modules)
                if (module != null && module.mesh != null && Profile.IsFound(module) && !found.Contains(module.mesh)) found.Add(module.mesh);

            // The back wall: three walls of drawers, and a ladder that rolls along them.
            foreach (float side in new[] { -3.25f, -0.3f, 2.65f })
                Solid("Drawers", "AR_Stacks", Side(side, Back - 0.75f), toHall, new Vector2(1.45f, 0.42f), def.lampGlow, WallAccent, FloorBase);
            ladderHome = Side(-0.3f, Back - 1.5f);
            ladder = Piece(room, "Ladder", "AR_Ladder", ladderHome, toHall, config.pulse, def.trim, def.trim);
            // Nobody stands where the ladder rolls.
            walk.Block(ladderHome, toHall, new Vector2(LadderReach + 0.5f, 0.4f));

            // The side wall: the cabinet of cards, the old shelf with its reading place, the machine with the tapes.
            Solid("Cards", "AR_CardCabinet", Side(4.5f, 2.5f), fromWall, new Vector2(0.78f, 0.36f), def.lampGlow, FloorBase, def.trim);
            walk.Block(Side(4.5f, 4.7f), fromWall, new Vector2(1.15f, 0.3f));
            walk.Block(Side(4.5f, 4.7f), fromWall, new Vector2(0.5f, 0.25f), new Vector2(0f, 0.48f));
            Piece(room, "Shelf", def.shelf, Side(4.5f, 4.7f), fromWall, config.pulse, def.trim, FloorBase);
            var bank = Solid("Tapes", "AR_TapeBank", Side(4.4f, 7.2f), fromWall, new Vector2(1.05f, 0.75f), config.pulse, def.trim, config.part, new Vector2(0f, 0.3f));
            reelFacing = fromWall;
            foreach (float x in new[] { -0.42f, 0.42f })
                reels.Add(Piece(room, "Reel", "AR_Reel", bank.TransformPoint(new Vector3(x, 1.75f, 0.45f)), fromWall, config.pulse, config.part, def.trim));

            // The middle: the table of light.
            Vector3 tablePos = Side(-0.7f, 5.3f);
            Round("Table of light", "AR_Projector", tablePos, toHall, 1.2f, config.pulse, config.part, def.trim);
            pictureAt = tablePos + Vector3.up * 1.75f;
            picture = new GameObject("Picture of light").transform;
            picture.SetParent(room, true);
            picture.position = pictureAt;
            pictureMesh = picture.gameObject.AddComponent<MeshFilter>();
            pictureLook = picture.gameObject.AddComponent<MeshRenderer>();
            pictureLook.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // The kerb side: glass cases, each with a part in it. They are tall, but nearly all glass.
            float[] depths = { 2.5f, 4.3f, 6.1f, 7.9f };
            for (int i = 0; i < depths.Length; i++)
            {
                Vector3 pos = Side(-4.3f, depths[i]);
                Solid("Case", "AR_Case", pos, fromKerb, new Vector2(0.47f, 0.47f), config.pulse, config.part, def.trim);
                if (found.Count == 0) continue;
                var mesh = found[(i * 2 + 1) % found.Count];
                var shown = Piece(room, "Part in a case", mesh, pos + Vector3.up * 1.2f, fromKerb, config.pulse, config.part, config.body);
                shown.localScale = Vector3.one * 0.62f;
                cased.Add(shown);
            }

            // The door end: a reading desk, and a bin of rolled plans.
            Solid("Desk", "AR_Desk", Side(-1.5f, 2.3f), toHall * Quaternion.Euler(0f, wallSide * -25f, 0f), new Vector2(0.95f, 0.55f),
                def.lampGlow, FloorAccent, FloorBase);
            Round("Plans", "AR_Scrolls", Side(2.2f, 7.6f), toHall * Quaternion.Euler(0f, wallSide * 20f, 0f), 0.6f, config.pulse, config.body, def.trim);
            Round("Crate", "Crate", Side(3.9f, 1.7f), Quaternion.Euler(0f, 20f, 0f), 0.5f);

            // Light: warm lamps for reading, the Pulse over the table and in the cases.
            AddLight("Desk lamp", Side(-1.2f, 2.5f, 2.2f), WarmLight, 6.5f, 5.5f);
            AddLight("Shelf lamp", Side(3.4f, 4.6f, 2.4f), WarmLight, 6.5f, 4.5f);
            AddLight("Stack lamp", Side(-0.3f, Back - 2f, 3.2f), WarmLight, 8f, 5f);
            AddLight("Case lamp", Side(-3.6f, 5.2f, 2.2f), PulseLight, 6f, 2.2f);
            beam = AddLight("Table light", pictureAt, PulseLight, 6f, 3f);
        }

        protected override void Run(float dt)
        {
            // The tapes: one reel gives, the other takes, and now and then both stop to think.
            bool reading = Mathf.Repeat(clock, 7f) < 5.4f;
            for (int i = 0; i < reels.Count; i++)
                if (reading) reels[i].rotation *= Quaternion.Euler(0f, 0f, (i == 0 ? 150f : -210f) * dt);
            // The ladder rolls along the drawers, slowly, and rests at either end.
            if (ladder != null) ladder.position = ladderHome + across * (Mathf.Sin(clock * 0.22f) * LadderReach);
            // The parts in the cases turn.
            for (int i = 0; i < cased.Count; i++)
                cased[i].rotation = Quaternion.Euler(0f, clock * (16f + i * 3f) + i * 70f, 0f);

            // The table shows what was found, one part after another, in the green of the Pulse.
            if (found.Count == 0) return;
            float at = Mathf.Repeat(clock, ShowTime);
            int which = (int)(clock / ShowTime) % found.Count;
            if (which != showing)
            {
                showing = which;
                var mesh = found[which];
                pictureMesh.sharedMesh = mesh;
                var materials = new Material[mesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++) materials[i] = config.pulse;
                pictureLook.sharedMaterials = materials;
            }
            // It grows out of the lens, turns, and sinks back into it.
            float size = Mathf.Min(Ui.EaseOut(at / SwapTime), Ui.EaseOut((ShowTime - at) / SwapTime));
            picture.localScale = Vector3.one * (1.15f * size);
            picture.position = pictureAt + Vector3.up * (Mathf.Sin(clock * 1.6f) * 0.07f - (1f - size) * 0.7f);
            picture.rotation = Quaternion.Euler(0f, clock * 38f, 0f);
            if (beam != null) beam.intensity = lit * (2.4f + 0.5f * Mathf.Sin(clock * 11f)) * (0.4f + 0.6f * size);
        }
    }
}
