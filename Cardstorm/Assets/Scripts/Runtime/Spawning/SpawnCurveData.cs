using System;
using UnityEngine;

namespace Cardstorm.Spawning
{
    // Cómo aparecen los enemigos de un tramo. Por ahora solo grupos por el borde de la pantalla.
    public enum SpawnPattern { ScreenEdgeGroups }

    public enum WaveFormation { Ring, Pincer, Stampede, Mixed }

    // Tramo de la partida: ritmo de aparición constante de cada tipo entre startTime y endTime.
    [Serializable]
    public class SpawnSegment
    {
        public string name;
        [Tooltip("Segundos de partida.")]
        public float startTime;
        [Tooltip("Segundos de partida (excluido).")]
        public float endTime;
        [Tooltip("Enemigos por minuto, uno por cada tipo de enemyTypes.")]
        public float[] ratePerMinute = Array.Empty<float>();
        [Tooltip("Multiplicador de vida y experiencia de los enemigos de este tramo.")]
        [Min(0.01f)] public float reinforcement = 1f;
        public SpawnPattern pattern = SpawnPattern.ScreenEdgeGroups;
    }

    // Oleada: enemigos extra en formación, repartidos en varios pulsos.
    [Serializable]
    public class WaveEvent
    {
        public string name;
        public float startTime;
        [Tooltip("Los pulsos se reparten a lo largo de esta duración.")]
        [Min(0f)] public float duration = 30f;
        [Tooltip("Enemigos extra, uno por cada tipo de enemyTypes.")]
        public int[] extraCount = Array.Empty<int>();
        public WaveFormation formation;
        [Min(1)] public int pulses = 2;

        [Header("Cofre")]
        public int chestGold;
        public float chestTime;
    }

    // Marcador de jefe: por ahora solo avisa al llegar su hora.
    [Serializable]
    public class BossEvent
    {
        public string name;
        public float time;
        public float health;
    }
}
