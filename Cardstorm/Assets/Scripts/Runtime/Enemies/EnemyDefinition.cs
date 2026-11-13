using UnityEngine;
using UnityEngine.Serialization;

namespace Cardstorm.Enemies
{
    public enum EnemyShape { Circle, Square, Triangle, Diamond, Ring }

    // Chase: persigue. KeepDistance: se mantiene a distancia y dispara (Tirador).
    // Charge: embiste tras un aviso cuando el jugador está cerca (Coloso).
    public enum EnemyBehaviour { Chase, KeepDistance, Charge }

    // Datos de un tipo de enemigo. Se editan en el inspector.
    [CreateAssetMenu(fileName = "EnemyDefinition_", menuName = "Cardstorm/Enemy Definition")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Identidad")]
        public string id;
        public string displayName;

        [Header("Combate")]
        [Min(1f)] public float maxHealth = 10f;
        [Min(0f)] public float contactDamage = 10f;

        [Header("Recompensas")]
        [Tooltip("Experiencia al morir. Se multiplica por el refuerzo del tramo.")]
        [Min(0)] public int xp = 1;
        [Tooltip("Oro de la moneda que suelta siempre. No se multiplica por el refuerzo.")]
        [Min(0)] public int coinValue = 1;
        [Tooltip("Probabilidad (0-1) de que la moneda sea un dime, que vale el doble.")]
        [FormerlySerializedAs("goldChance")]
        [Range(0f, 1f)] public float doubleCoinChance = 0.3f;
        [Tooltip("Probabilidad (0-1) de soltar un orbe de energía.")]
        [Range(0f, 1f)] public float energyChance = 0.1f;

        [Header("Movimiento y físicas")]
        [Tooltip("Unidades/s. El jugador va a 5.")]
        [Min(0f)] public float moveSpeed = 3f;
        [Min(0.05f)] public float radius = 0.4f;
        [Min(0.01f)] public float mass = 1f;
        public EnemyBehaviour behaviour = EnemyBehaviour.Chase;

        [Header("Distancia (solo KeepDistance)")]
        [Tooltip("Distancia al jugador a la que intenta quedarse.")]
        [Min(0f)] public float preferredDistance = 6f;
        [Tooltip("Margen alrededor de la distancia preferida en el que se queda quieto.")]
        [Min(0f)] public float distanceTolerance = 1f;

        [Header("Disparo (solo KeepDistance)")]
        [Tooltip("Segundos entre disparos.")]
        [Min(0.1f)] public float fireInterval = 2f;
        [Tooltip("Solo dispara si el jugador está a esta distancia o menos y sin obstáculos en medio.")]
        [Min(0f)] public float fireRange = 9f;
        [Min(0f)] public float projectileDamage = 8f;
        [Tooltip("Unidades/s.")]
        [Min(0.1f)] public float projectileSpeed = 7f;
        [Tooltip("Distancia máxima que recorre el proyectil.")]
        [Min(0.1f)] public float projectileRange = 12f;
        [Min(0.02f)] public float projectileRadius = 0.15f;
        public Color projectileColor = new(1f, 0.55f, 0.9f, 1f);

        [Header("Embestida (solo Charge)")]
        [Tooltip("Empieza el aviso si el jugador está a esta distancia o menos y sin obstáculos en medio.")]
        [Min(0f)] public float chargeTriggerRange = 6f;
        [Tooltip("Segundos de aviso: se queda quieto y muestra la trayectoria.")]
        [Min(0f)] public float chargeWindup = 0.9f;
        [Tooltip("Unidades/s durante la embestida.")]
        [Min(0.1f)] public float chargeSpeed = 12f;
        [Tooltip("Recorrido máximo de la embestida.")]
        [Min(0.1f)] public float chargeDistance = 9f;
        [Tooltip("Segundos quieto después de embestir.")]
        [Min(0f)] public float chargeRecover = 0.6f;
        [Tooltip("Segundos desde el final de la recuperación hasta que puede volver a avisar.")]
        [Min(0f)] public float chargeCooldown = 3f;

        public ChargeSettings ChargeSettings =>
            new(chargeTriggerRange, chargeWindup, chargeSpeed, chargeDistance, chargeRecover, chargeCooldown);

        [Header("Jefe")]
        [Tooltip("Un jefe no se recicla por distancia, atraviesa los obstáculos y persigue en línea recta.")]
        public bool isBoss;

        [Header("Aspecto")]
        [Tooltip("Se distinguen por forma además de por color.")]
        public EnemyShape shape = EnemyShape.Circle;
        public Color color = Color.red;

        [Header("Pool")]
        [Tooltip("Instancias creadas al empezar la partida.")]
        [Min(0)] public int poolPrewarm = 50;
    }
}
