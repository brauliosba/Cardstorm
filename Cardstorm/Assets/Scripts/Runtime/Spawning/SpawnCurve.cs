using System.Collections.Generic;
using Cardstorm.Enemies;
using UnityEngine;

namespace Cardstorm.Spawning
{
    // La partida completa: tramos, oleadas y jefes. Se edita en el inspector.
    [CreateAssetMenu(fileName = "SpawnCurve_", menuName = "Cardstorm/Spawn Curve")]
    public class SpawnCurve : ScriptableObject
    {
        const float TimeEpsilon = 0.001f;

        [Tooltip("Orden de los arrays de tramos y oleadas: Rondador, Corredor, Tirador, Coloso.")]
        public EnemyDefinition[] enemyTypes = System.Array.Empty<EnemyDefinition>();

        [Tooltip("Cada cuántos segundos llega un grupo de los tramos.")]
        [Min(0.1f)] public float groupInterval = 2.5f;

        [Tooltip("Radio del grupo alrededor de su punto de aparición.")]
        [Min(0f)] public float groupSpread = 1.5f;

        public List<SpawnSegment> segments = new();
        public List<WaveEvent> waves = new();
        public List<BossEvent> bosses = new();

        public int TypeCount => enemyTypes.Length;

        public float Duration => segments.Count > 0 ? segments[^1].endTime : 0f;

        public SpawnSegment SegmentAt(float time)
        {
            int index = SegmentIndexAt(segments, time);
            return index >= 0 ? segments[index] : null;
        }

        public List<string> Validate()
        {
            var issues = Validate(enemyTypes.Length, groupInterval, segments, waves);
            for (int i = 0; i < enemyTypes.Length; i++)
            {
                if (enemyTypes[i] == null)
                    issues.Add($"enemyTypes[{i}] está vacío.");
            }
            return issues;
        }

        void OnValidate()
        {
            foreach (var issue in Validate())
                Debug.LogWarning($"[SpawnCurve] {name}: {issue}", this);
        }

        // Tramo vigente en time. Antes del primero devuelve 0; después del último, el último.
        public static int SegmentIndexAt(IReadOnlyList<SpawnSegment> segments, float time)
        {
            if (segments == null || segments.Count == 0) return -1;
            for (int i = 0; i < segments.Count; i++)
            {
                if (time < segments[i].endTime)
                    return i;
            }
            return segments.Count - 1;
        }

        // Comprobaciones de los datos. Devuelve una lista de problemas en lenguaje natural (vacía si todo está bien).
        public static List<string> Validate(int typeCount, float groupInterval,
            IReadOnlyList<SpawnSegment> segments, IReadOnlyList<WaveEvent> waves)
        {
            var issues = new List<string>();

            if (groupInterval <= 0f)
                issues.Add("groupInterval debe ser mayor que 0.");

            if (segments == null || segments.Count == 0)
            {
                issues.Add("No hay tramos.");
            }
            else
            {
                if (Mathf.Abs(segments[0].startTime) > TimeEpsilon)
                    issues.Add($"El primer tramo empieza en {segments[0].startTime} s; debe empezar en 0.");

                for (int i = 0; i < segments.Count; i++)
                {
                    var s = segments[i];
                    string label = $"Tramo {i} ({s.name})";
                    if (s.endTime - s.startTime <= TimeEpsilon)
                        issues.Add($"{label}: el fin ({s.endTime} s) debe ser mayor que el inicio ({s.startTime} s).");
                    if (s.ratePerMinute == null || s.ratePerMinute.Length != typeCount)
                        issues.Add($"{label}: ratePerMinute tiene {s.ratePerMinute?.Length ?? 0} valores y hay {typeCount} tipos.");
                    else if (System.Array.Exists(s.ratePerMinute, r => r < 0f))
                        issues.Add($"{label}: hay ritmos negativos.");

                    if (i == 0) continue;
                    float previousEnd = segments[i - 1].endTime;
                    if (s.startTime > previousEnd + TimeEpsilon)
                        issues.Add($"{label}: hay un hueco entre {previousEnd} s y {s.startTime} s.");
                    else if (s.startTime < previousEnd - TimeEpsilon)
                        issues.Add($"{label}: se solapa con el anterior ({s.startTime} s < {previousEnd} s).");
                }
            }

            if (waves != null)
            {
                float duration = segments != null && segments.Count > 0 ? segments[^1].endTime : 0f;
                for (int i = 0; i < waves.Count; i++)
                {
                    var w = waves[i];
                    string label = $"Oleada {i} ({w.name})";
                    if (w.extraCount == null || w.extraCount.Length != typeCount)
                        issues.Add($"{label}: extraCount tiene {w.extraCount?.Length ?? 0} valores y hay {typeCount} tipos.");
                    if (w.pulses < 1)
                        issues.Add($"{label}: debe tener al menos 1 pulso.");
                    if (w.startTime < 0f || w.startTime >= duration)
                        issues.Add($"{label}: empieza en {w.startTime} s, fuera de la partida (0-{duration} s).");
                    if (i > 0 && w.startTime < waves[i - 1].startTime)
                        issues.Add($"{label}: las oleadas deben estar ordenadas por inicio.");
                }
            }

            return issues;
        }
    }
}
