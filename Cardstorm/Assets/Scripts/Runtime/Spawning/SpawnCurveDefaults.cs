using System.Collections.Generic;

namespace Cardstorm.Spawning
{
    // Valores de la hoja de equilibrado (hojas Tramos y Oleadas). El builder los usa para crear el asset
    // y los tests para comprobar el planificador con la curva real, aunque el asset se haya editado.
    // Orden de los tipos: Rondador, Corredor, Tirador, Coloso.
    public static class SpawnCurveDefaults
    {
        public const int TypeCount = 4;
        public const float GroupInterval = 2.5f;
        public const float GroupSpread = 1.5f;

        public static List<SpawnSegment> Segments() => new()
        {
            Segment("Aprendizaje",    0,  60,  40,  0,  0, 0, 1.0f),
            Segment("Aprendizaje",   60, 120,  54,  0,  0, 0, 1.0f),
            Segment("Primer pico",  120, 165,  54, 10,  0, 0, 1.0f),
            Segment("Oleada 1",     165, 195,  54, 10,  0, 0, 1.0f),
            Segment("Respiro",      195, 240,  40,  6,  0, 0, 1.0f),
            Segment("Primer pico",  240, 300,  60, 10,  8, 0, 1.1f),
            Segment("Heraldo",      300, 360,  40,  6,  4, 0, 1.1f),
            Segment("Respiro",      360, 390,  36,  6,  4, 0, 1.1f),
            Segment("Construcción", 390, 480,  72, 28,  9, 2, 1.2f),
            Segment("Oleada 2",     480, 510,  72, 28,  9, 2, 1.2f),
            Segment("Respiro",      510, 540,  44, 12,  6, 1, 1.2f),
            Segment("Construcción", 540, 660,  88, 38, 12, 3, 1.3f),
            Segment("Oleada 3",     660, 690,  88, 38, 12, 3, 1.3f),
            Segment("Respiro",      690, 720,  54, 18,  8, 2, 1.3f),
            Segment("Clímax",       720, 840, 112, 50, 18, 4, 1.4f),
            Segment("Oleada 4",     840, 870, 112, 50, 18, 4, 1.4f),
            Segment("Calma",        870, 900,   0,  0,  0, 0, 1.4f),
        };

        public static List<WaveEvent> Waves() => new()
        {
            Wave("Oleada 1: Cerco",     165, 30,  0, 0, 0, WaveFormation.Ring,     chestGold: 15, chestTime: 195),
            Wave("Oleada 2: Pinza",     480,  0, 20, 8, 0, WaveFormation.Pincer,   chestGold: 20, chestTime: 510),
            Wave("Oleada 3: Estampida", 660, 40,  0, 0, 1, WaveFormation.Stampede, chestGold: 25, chestTime: 690),
            Wave("Oleada 4: Tormenta",  840, 30,  8, 4, 1, WaveFormation.Mixed,    chestGold: 25, chestTime: 870),
        };

        public static List<BossEvent> Bosses() => new()
        {
            new BossEvent { name = "Heraldo", time = 300, health = 800 },
            new BossEvent { name = "Ojo de la tormenta", time = 900, health = 2000 },
        };

        // Rellena la curva con los valores por defecto (sin tocar enemyTypes).
        public static void ApplyTo(SpawnCurve curve)
        {
            curve.groupInterval = GroupInterval;
            curve.groupSpread = GroupSpread;
            curve.segments = Segments();
            curve.waves = Waves();
            curve.bosses = Bosses();
        }

        static SpawnSegment Segment(string name, float start, float end,
            float rondador, float corredor, float tirador, float coloso, float reinforcement) => new()
        {
            name = name,
            startTime = start,
            endTime = end,
            ratePerMinute = new[] { rondador, corredor, tirador, coloso },
            reinforcement = reinforcement,
            pattern = SpawnPattern.ScreenEdgeGroups,
        };

        static WaveEvent Wave(string name, float start,
            int rondador, int corredor, int tirador, int coloso, WaveFormation formation,
            int chestGold, float chestTime) => new()
        {
            name = name,
            startTime = start,
            duration = 30f,
            extraCount = new[] { rondador, corredor, tirador, coloso },
            formation = formation,
            pulses = 2,
            chestGold = chestGold,
            chestTime = chestTime,
        };
    }
}
