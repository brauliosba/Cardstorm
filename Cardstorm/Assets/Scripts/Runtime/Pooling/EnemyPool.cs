using System.Collections.Generic;
using Cardstorm.Enemies;
using UnityEngine;
using UnityEngine.Pool;

namespace Cardstorm.Pooling
{
    // Un ObjectPool<Enemy> por definición, precalentado al empezar.
    // Después del precalentamiento no debe haber Instantiate ni Destroy de enemigos durante la partida.
    public class EnemyPool : MonoBehaviour
    {
        [SerializeField] Enemy prefab;
        [SerializeField] EnemyDefinition[] definitions;
        [SerializeField, Min(1)] int maxSize = 600;

        readonly Dictionary<EnemyDefinition, ObjectPool<Enemy>> pools = new();
        readonly Dictionary<EnemyDefinition, int> created = new();
        readonly List<Enemy> prewarmBuffer = new();

        public IReadOnlyList<EnemyDefinition> Definitions => definitions;

        // Instancias creadas en total (activas + en el pool). No debe crecer tras el precalentamiento.
        public int InstanceCount { get; private set; }

        void Awake()
        {
            foreach (var definition in definitions)
                Prewarm(definition, definition.poolPrewarm);
        }

        public Enemy Get(EnemyDefinition definition) => PoolFor(definition).Get();

        public void Release(Enemy enemy) => PoolFor(enemy.Definition).Release(enemy);

        public int CreatedFor(EnemyDefinition definition) => created.TryGetValue(definition, out int n) ? n : 0;

        public int InactiveFor(EnemyDefinition definition) => pools.TryGetValue(definition, out var pool) ? pool.CountInactive : 0;

        void Prewarm(EnemyDefinition definition, int count)
        {
            var pool = PoolFor(definition);
            prewarmBuffer.Clear();
            for (int i = 0; i < count; i++)
                prewarmBuffer.Add(pool.Get());
            foreach (var enemy in prewarmBuffer)
                pool.Release(enemy);
            prewarmBuffer.Clear();
        }

        ObjectPool<Enemy> PoolFor(EnemyDefinition definition)
        {
            if (pools.TryGetValue(definition, out var pool))
                return pool;

            pool = new ObjectPool<Enemy>(
                createFunc: () => Create(definition),
                actionOnGet: null,                       // Enemy.Activate configura y activa la instancia
                actionOnRelease: enemy => enemy.Deactivate(),
                actionOnDestroy: enemy => { InstanceCount--; created[definition]--; Destroy(enemy.gameObject); },
                collectionCheck: Application.isEditor,
                defaultCapacity: Mathf.Max(1, definition.poolPrewarm),
                maxSize: maxSize);
            pools.Add(definition, pool);
            created[definition] = 0;
            return pool;
        }

        Enemy Create(EnemyDefinition definition)
        {
            var enemy = Instantiate(prefab, transform);
            enemy.name = $"{prefab.name}_{definition.id}_{created[definition]:000}";
            enemy.gameObject.SetActive(false);
            InstanceCount++;
            created[definition]++;
            return enemy;
        }
    }
}
