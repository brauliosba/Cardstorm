using System.Collections.Generic;
using Cardstorm.Enemies;
using UnityEngine;

namespace Cardstorm.Combat
{
    // Proyectil de las cartas. No tiene collider ni rigidbody: lo mueve ProjectileManager con CircleCast.
    [RequireComponent(typeof(SpriteRenderer))]
    public class Projectile : MonoBehaviour
    {
        SpriteRenderer spriteRenderer;

        // Enemigos ya golpeados, para no dañar dos veces al mismo al atravesarlo.
        internal readonly HashSet<Enemy> Hit = new();

        internal Vector2 Position { get; set; }
        internal Vector2 Direction { get; private set; }
        internal float Speed { get; private set; }
        internal float MaxDistance { get; private set; }
        internal float Travelled { get; set; }
        internal float Radius { get; private set; }
        internal float Damage { get; private set; }
        internal int PierceLeft { get; set; }
        internal int ActiveIndex { get; set; } = -1;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // Reinicia todo el estado: la instancia viene del pool.
        internal void Launch(Vector2 origin, Vector2 direction, float speed, float maxDistance, float radius, float damage, int pierce, Color color)
        {
            Position = origin;
            Direction = direction;
            Speed = speed;
            MaxDistance = maxDistance;
            Travelled = 0f;
            Radius = radius;
            Damage = damage;
            PierceLeft = pierce;
            Hit.Clear();

            spriteRenderer.color = color;
            transform.localScale = Vector3.one * (radius * 2f);  // El sprite mide 1 unidad.
            transform.position = origin;
            gameObject.SetActive(true);
        }

        internal void Deactivate()
        {
            Hit.Clear();
            ActiveIndex = -1;
            gameObject.SetActive(false);
        }
    }
}
