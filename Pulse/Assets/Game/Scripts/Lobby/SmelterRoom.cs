using UnityEngine;

namespace Pulse
{
    // The Smelter's foundry, where frames are cast. It is open from the start.
    //
    // Across the room runs a track. Behind it is the hot end, where nobody walks: the furnace with
    // its bellows, a bin of coal and the tools. A cart carries a crucible along the track, from
    // the mouth of the furnace to the bed of sand where a frame is cast, and back. On the near
    // side stand the bed, the rack where finished frames hang, a hammer that never stops, a
    // trough to quench in, ingots, and the Smelter.
    public sealed class SmelterRoom : LobbyRoom
    {
        // The cart: it stands at the furnace, rolls to the bed, pours, and rolls back.
        const float FillTime = 2.6f, RollTime = 2.8f, PourTime = 2.4f, TrackDepth = 6.1f;
        // The hammer: one blow every HammerCycle seconds.
        const float HammerCycle = 1.5f, HammerFall = 0.12f, HammerRise = 0.6f, HammerStroke = 1.0f;
        static readonly Color FireLight = new Color(1f, 0.5f, 0.2f), Smoke = new Color(0.2f, 0.19f, 0.19f), Steam = new Color(0.62f, 0.64f, 0.67f);

        Transform cart, head, bellows;
        Light fire, bed, blow;
        Material brick, basalt, basaltPale;
        Vector3 cartAtFurnace, cartAtBed, headRest, anvil, mouth, stack, bedPos, troughTop;
        float emberOwed, smokeOwed, steamOwed, pourOwed, lastBlow;

        protected override Material FloorBase => basalt != null ? basalt : basalt = Tinted(def.wallMaterial, new Color(0.16f, 0.16f, 0.18f));
        protected override Material FloorAccent => basaltPale != null ? basaltPale : basaltPale = Tinted(def.wallMaterial, new Color(0.25f, 0.23f, 0.22f));
        protected override Material WallBase => brick != null ? brick : brick = Tinted(def.wallMaterial, new Color(0.33f, 0.17f, 0.12f));

        protected override void Furnish()
        {
            StationPos = Side(0.3f, 2.9f);
            walk.Block(StationPos, 0.72f);
            Piece(room, "Smelter", def.smelter, StationPos, toHall * Quaternion.Euler(0f, wallSide * -15f, 0f), def.fireGlow, config.body, def.trim);

            // The hot end. The furnace, half as large again as it was in the hall, with its bellows
            // beside it pointing at it, coal, and the tools of the trade on the wall.
            Vector3 furnacePos = Side(1.3f, Back - 1.55f);
            var furnace = Piece(room, "Furnace", def.furnace, furnacePos, toHall, def.fireGlow, def.trim, WallBase);
            furnace.localScale = Vector3.one * 1.45f;
            mouth = furnacePos - outward * 1.25f + Vector3.up * 1.15f;
            stack = furnace.TransformPoint(new Vector3(0.5f, 4.4f, -0.3f));
            bellows = Piece(room, "Bellows", "SR_Bellows", Side(3.1f, Back - 1.5f), Quaternion.LookRotation(across * wallSide), def.fireGlow, def.trim, FloorAccent);
            Piece(room, "Coal", "SR_CoalBin", Side(-2.3f, Back - 0.85f), toHall, def.fireGlow, def.trim, def.trim);
            Piece(room, "Tools", "TR_ToolWall", Side(-3.4f, Back - 0.3f), toHall);
            Piece(room, "Barrel", "Barrel", Side(-4.3f, TrackDepth + 1.5f), Quaternion.identity);

            // The track, and the cart on it. The whole strip and everything behind it is out of bounds.
            Vector3 track = Side(-0.4f, TrackDepth);
            Piece(room, "Track", "SR_Rail", track, toRoom, def.fireGlow, def.trim, def.trim);
            walk.Block(Side(0f, (TrackDepth - 0.8f + Back) * 0.5f), toRoom, new Vector2(Width * 0.5f, (Back - TrackDepth + 0.8f) * 0.5f));
            cartAtFurnace = Side(1.3f, TrackDepth);
            cartAtBed = Side(-2.7f, TrackDepth);
            cart = Piece(room, "Cart", "SR_Cart", cartAtFurnace, toRoom, def.fireGlow, def.trim, config.part);

            // The near side. The bed of sand under the far end of the track, where the frame is cast.
            bedPos = Side(-2.7f, 3.95f);
            Solid("Casting bed", "SR_Moulds", bedPos, toHall, new Vector2(1.6f, 1.15f), def.fireGlow, FloorAccent, def.trim);
            // The rack where finished frames hang: a gantry, and plates stacked against its foot.
            Vector3 rackPos = Side(2.3f, 4.5f);
            walk.Block(rackPos, toHall, new Vector2(1.6f, 0.26f));
            walk.Block(rackPos, toHall, new Vector2(0.72f, 0.45f), new Vector2(0.2f, 0.5f));
            Piece(room, "Frame rack", def.frameRack, rackPos, toHall, config.pulse, def.trim, config.part);
            // The hammer against the side wall, with a billet on its anvil.
            Vector3 hammerPos = Side(4.25f, 2.6f);
            var hammer = Solid("Hammer", "SR_Hammer", hammerPos, fromWall, new Vector2(0.8f, 0.8f), def.fireGlow, def.trim, config.part);
            headRest = hammer.TransformPoint(new Vector3(0f, 2.25f, 0.15f));
            head = Piece(room, "Hammer head", "SR_HammerHead", headRest, fromWall, def.fireGlow, def.trim, config.part);
            anvil = hammer.TransformPoint(new Vector3(0f, 1.2f, 0.15f));
            // By the kerb: the trough, and what is waiting to be melted.
            Vector3 troughPos = Side(-4.3f, 1.9f);
            Solid("Trough", "SR_Quench", troughPos, fromKerb, new Vector2(0.95f, 0.5f), def.fireGlow, config.part, def.trim);
            troughTop = troughPos + Vector3.up * 0.85f;
            Solid("Ingots", "SR_Ingots", Side(3.1f, 1.75f), toHall * Quaternion.Euler(0f, wallSide * 12f, 0f), new Vector2(0.75f, 0.6f), def.fireGlow, config.part, def.trim);
            Round("Crate", "Crate", Side(-4.3f, 4.2f), Quaternion.Euler(0f, 14f, 0f), 0.5f);

            // Light: the fire, the metal in the bed, a flash at every blow of the hammer, and one cold lamp.
            fire = AddLight("Fire", mouth - outward * 0.6f, FireLight, 10f, 9f);
            bed = AddLight("Casting bed", bedPos + Vector3.up * 1.1f, FireLight, 5.5f, 3f);
            blow = AddLight("Blow", anvil + Vector3.up * 0.3f, new Color(1f, 0.75f, 0.45f), 4.5f, 0f);
            AddLight("Door lamp", Side(0.5f, 2f, 3f), new Color(0.8f, 0.86f, 1f), 9f, 4.5f);
            AddLight("Rack lamp", rackPos + Vector3.up * 2.6f - outward * 0.8f, new Color(0.45f, 1f, 0.7f), 4.5f, 1.8f);
        }

        protected override void Run(float dt)
        {
            // The cart: fill, roll, pour, roll back.
            float lap = FillTime + RollTime + PourTime + RollTime, at = Mathf.Repeat(clock, lap), along;
            if (at < FillTime) along = 0f;
            else if (at < FillTime + RollTime) along = Mathf.SmoothStep(0f, 1f, (at - FillTime) / RollTime);
            else if (at < FillTime + RollTime + PourTime) along = 1f;
            else along = 1f - Mathf.SmoothStep(0f, 1f, (at - FillTime - RollTime - PourTime) / RollTime);
            bool filling = at < FillTime, pouring = along >= 1f;
            if (cart != null) cart.position = Vector3.Lerp(cartAtFurnace, cartAtBed, along);

            // The fire breathes with the bellows, and throws embers out of its mouth and smoke out of its stack.
            float breath = 0.5f + 0.5f * Mathf.Sin(clock * 2.1f);
            if (bellows != null) bellows.localScale = new Vector3(1f, Mathf.Lerp(0.62f, 1f, breath), 1f);
            if (fire != null) fire.intensity = lit * (7f + 3.5f * (1f - breath) + Random.Range(-0.6f, 0.6f));
            emberOwed += dt * (filling ? 16f : 6f);
            int embers = (int)emberOwed;
            emberOwed -= embers;
            if (embers > 0) Combat.Vfx.Rise(mouth, 0.4f, 0f, Random.value < 0.5f ? Tokens.Heat : Tokens.Alert, embers, 2.6f, 0.12f, 0.8f);
            smokeOwed += dt * 5f;
            int puffs = (int)smokeOwed;
            smokeOwed -= puffs;
            if (puffs > 0) Combat.Vfx.Rise(stack, 0.25f, 0f, Smoke, puffs, 1.8f, 0.45f, 2.2f);

            // Pouring: metal runs into the bed, which glows up, and sparks jump off it.
            if (pouring)
            {
                pourOwed += dt * 28f;
                int sparks = (int)pourOwed;
                pourOwed -= sparks;
                if (sparks > 0) Combat.Vfx.Sparks(bedPos + outward * 1.2f + Vector3.up * 0.6f, Random.value < 0.6f ? Tokens.Heat : Tokens.Alert, sparks, 5f, 0.35f, 0.4f);
            }
            if (bed != null) bed.intensity = lit * Mathf.Lerp(bed.intensity / Mathf.Max(0.01f, lit), pouring ? 7f : 2.6f + 0.4f * Mathf.Sin(clock * 3f), 1f - Mathf.Exp(-3f * dt));

            // The hammer: up slowly, down at once, and sparks where it lands.
            float beat = Mathf.Repeat(clock, HammerCycle), raised;
            if (beat < HammerFall) raised = 1f - beat / HammerFall;
            else if (beat < HammerFall + 0.1f) raised = 0f;
            else raised = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((beat - HammerFall - 0.1f) / HammerRise));
            if (head != null) head.position = headRest + Vector3.down * (HammerStroke * (1f - raised));
            if (beat >= HammerFall && lastBlow < HammerFall)
            {
                Combat.Vfx.Sparks(anvil, Tokens.Alert, 14, 7f, 0.35f, 0.3f);
                Combat.Vfx.Sparks(anvil, Tokens.Heat, 8, 5f, 0.3f, 0.35f);
                if (blow != null) blow.intensity = lit * 7f;
            }
            lastBlow = beat;
            if (blow != null) blow.intensity = Mathf.Lerp(blow.intensity, 0f, 1f - Mathf.Exp(-9f * dt));

            // Steam off the trough, without a break.
            steamOwed += dt * 5f;
            int wisps = (int)steamOwed;
            steamOwed -= wisps;
            if (wisps > 0) Combat.Vfx.Rise(troughTop, 0.5f, 0f, Steam, wisps, 1.2f, 0.2f, 1.2f);
        }
    }
}
