using Cardstorm.Core;
using UnityEngine;

namespace Cardstorm.Combat
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class Turret : MonoBehaviour
    {
        SpriteRenderer spriteRenderer;

        internal Vector2 Position { get; private set; }
        internal float Remaining { get; set; }
        internal float FireTimer { get; set; }
        internal float FireInterval { get; private set; }
        internal float Damage { get; private set; }
        internal float Range { get; private set; }
        internal float ProjectileSpeed { get; private set; }
        internal float ProjectileRadius { get; private set; }
        internal float ProjectileDistance { get; private set; }
        // Color del proyectil cuando la carta no tiene VFX propio.
        internal Color Color { get; private set; }
        internal TurretManager.TurretLook Look { get; private set; }
        // Carta que la puso: sus proyectiles le atribuyen el daño.
        internal DamageSource Source { get; private set; }
        // Segundos desde que se puso: marca la caída.
        internal float Age { get; set; }
        internal bool Landed { get; set; }
        // Segundos que le quedan al retroceso del último disparo.
        internal float Recoil { get; set; }
        internal bool FacingLeft { get; set; }
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
            float projectileSpeed, float projectileRadius, float projectileDistance, Color color, in DamageSource source,
            in TurretManager.TurretLook look, int order)
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
            Color = color;
            Look = look;
            Source = source;
            Age = 0f;
            Landed = false;
            Recoil = 0f;
            FacingLeft = false;
            Order = order;

            Renderer.flipX = false;
            Renderer.enabled = true;
            transform.position = position;
            gameObject.SetActive(true);
        }

        // offset en píxeles respecto a Position.
        internal void SetView(Vector2 offset, bool visible)
        {
            transform.position = Position + offset / TurretManager.PixelsPerUnit;
            Renderer.flipX = FacingLeft;
            Renderer.enabled = visible;
        }

        internal void Deactivate()
        {
            ActiveIndex = -1;
            gameObject.SetActive(false);
        }
    }
}
