using System.Collections.Generic;
using Cardstorm.Core;
using Cardstorm.Enemies;
using UnityEngine;

namespace Cardstorm.Combat
{
    // Proyectil de las cartas o de los enemigos (Hostile). No tiene collider ni rigidbody: lo mueve ProjectileManager con CircleCast.
    [RequireComponent(typeof(SpriteRenderer))]
    public class Projectile : MonoBehaviour
    {
        SpriteRenderer spriteRenderer;
        Sprite defaultSprite;

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
        // Impactos con el daño completo; los siguientes hacen Damage · ExtraHitDamage.
        internal int FullHits { get; private set; }
        internal float ExtraHitDamage { get; private set; }
        // Disparado por un enemigo: daña al jugador en vez de a los enemigos.
        internal bool Hostile { get; private set; }
        // Carta que lo lanzó, o enemigo que lo disparó si es hostil.
        internal DamageSource Source { get; private set; }
        internal string HostileSourceId { get; private set; }
        internal int ActiveIndex { get; set; } = -1;
        internal ProjectileVfx Vfx { get; private set; }
        // Distancia recorrida desde la última partícula de la estela.
        internal float TrailCarry { get; set; }

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            defaultSprite = spriteRenderer.sprite;
        }

        // Reinicia todo el estado: la instancia viene del pool.
        internal void Launch(Vector2 origin, Vector2 direction, float speed, float maxDistance, float radius, float damage, int pierce, Color color, bool hostile,
            int fullHits, float extraHitDamage, in DamageSource source, string hostileSourceId, ProjectileVfx vfx)
        {
            Position = origin;
            Direction = direction;
            Speed = speed;
            MaxDistance = maxDistance;
            Travelled = 0f;
            Radius = radius;
            Damage = damage;
            PierceLeft = pierce;
            FullHits = fullHits < 0 ? pierce + 1 : fullHits;
            ExtraHitDamage = extraHitDamage;
            Hostile = hostile;
            Source = source;
            HostileSourceId = hostileSourceId;
            Vfx = vfx;
            TrailCarry = 0f;
            Hit.Clear();

            if (vfx != null && vfx.sprite != null)
            {
                spriteRenderer.sprite = vfx.sprite;
                spriteRenderer.color = Color.white;
                transform.localScale = Vector3.one;
            }
            else
            {
                spriteRenderer.sprite = defaultSprite;
                spriteRenderer.color = color;
                transform.localScale = Vector3.one * (radius * 2f);  // El sprite mide 1 unidad.
            }
            float angle = vfx != null && vfx.orient && vfx.sprite != null ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg : 0f;
            transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, 0f, angle));
            gameObject.SetActive(true);
        }

        internal void Deactivate()
        {
            Hit.Clear();
            Vfx = null;
            ActiveIndex = -1;
            gameObject.SetActive(false);
        }
    }
}
