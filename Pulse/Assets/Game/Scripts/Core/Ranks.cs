namespace Pulse
{
    // What a rank does to the numbers of a species.
    public static class Ranks
    {
        public readonly struct Stats
        {
            // Hull is the species' own times hpScale, plus hpFlat, so even a weak species makes a real boss.
            public readonly float hpScale, hpFlat, size, damage, speed;

            public Stats(float hpScale, float hpFlat, float size, float damage, float speed)
            {
                this.hpScale = hpScale;
                this.hpFlat = hpFlat;
                this.size = size;
                this.damage = damage;
                this.speed = speed;
            }
        }

        static readonly Stats[] table =
        {
            new Stats(0.4f, 0f, 0.62f, 0.6f, 1.15f),
            new Stats(1f, 0f, 1f, 1f, 1f),
            new Stats(5f, 120f, 1.7f, 1.6f, 0.92f),
            new Stats(12f, 500f, 2.5f, 2.2f, 0.85f),
            new Stats(1f, 0f, 1f, 1f, 1f),
        };

        public static Stats Of(Rank rank) => table[(int)rank];

        public static float Hull(EnemyDef def, Rank rank) => def.hp * Of(rank).hpScale + Of(rank).hpFlat;

        // Mini bosses and bosses carry their own names; without one, the species name is used.
        public static string Name(EnemyDef def, Rank rank)
        {
            string special = rank == Rank.Boss ? def.bossName : rank == Rank.MiniBoss ? def.miniBossName : null;
            return string.IsNullOrEmpty(special) ? def.displayName : special;
        }

        public static string Label(Rank rank) =>
            rank == Rank.Warden ? "Warden" : rank == Rank.Boss ? "Boss" : rank == Rank.MiniBoss ? "Mini boss" : "";
    }
}
