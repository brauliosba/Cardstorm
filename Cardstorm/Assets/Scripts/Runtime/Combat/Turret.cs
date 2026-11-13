using UnityEngine;

namespace Cardstorm.Combat
{
    // Torreta fija de las cartas. Sin física: la mueve y la hace disparar TurretManager.
    [RequireComponent(typeof(SpriteRenderer))]
    public class Turret : MonoBehaviour
    {
        SpriteRenderer spriteRenderer;
        Color color;

        internal Vector2 Position { get; private set; }
        internal float Remaining { get; set; }
        internal float FireTimer { get; set; }
        internal float FireInterval { get; private set; }
        internal float Damage { get; private set; }
        internal float Range { get; private set; }
        internal float ProjectileSpeed { get; private set; }
        internal float ProjectileRadius { get; private set; }
        internal float ProjectileDistance { get; private set; }
        internal Color Color => color;
        // Tiempo que lleva desvaneciéndose; negativo mientras está activa.
        internal float Fade { get; set; }
        // Orden de colocación, para reciclar la más antigua.
        internal int Order { get; private set; }
        internal int ActiveIndex { get; set; } = -1;

        internal SpriteRenderer Renderer
        {
            get
            {
                if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
                return spriteRenderer;
            }
        }

        // Reinicia todo el estado: la instancia viene del pool. Los valores quedan fijos hasta que acaba.
        internal void Place(Vector2 position, float duration, float fireInterval, float damage, float range,
            float projectileSpeed, float projectileRadius, float projectileDistance, Color color, int order)
        {
            Position = position;
            Remaining = duration;
            FireTimer = 0f;
            FireInterval = fireInterval;
            Damage = damage;
            Range = range;
            ProjectileSpeed = projectileSpeed;
            ProjectileRadius = projectileRadius;
            ProjectileDistance = projectileDistance;
            Fade = -1f;
            Order = order;

            this.color = color;
            Renderer.color = color;
            transform.position = position;
            gameObject.SetActive(true);
        }

        internal void SetAlpha(float alpha)
        {
            var c = color;
            c.a *= alpha;
            Renderer.color = c;
        }

        internal void Deactivate()
        {
            ActiveIndex = -1;
            gameObject.SetActive(false);
        }
    }
}
