using UnityEngine;
using UnityEngine.Serialization;

namespace Cardstorm.Enemies
{
    public enum EnemyShape { Circle, Square, Triangle, Diamond, Ring }

    // Solo Chase está implementado; KeepDistance y Charge quedan para los comportamientos propios de cada tipo.
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

        [Header("Movimiento y físicas")]
        [Tooltip("Unidades/s. El jugador va a 5.")]
        [Min(0f)] public float moveSpeed = 3f;
        [Min(0.05f)] public float radius = 0.4f;
        [Min(0.01f)] public float mass = 1f;
        public EnemyBehaviour behaviour = EnemyBehaviour.Chase;

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
