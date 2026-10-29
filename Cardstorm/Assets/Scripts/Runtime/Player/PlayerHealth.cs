using System;
using Cardstorm.Core;
using UnityEngine;

namespace Cardstorm.Player
{
    [RequireComponent(typeof(CircleCollider2D))]
    public class PlayerHealth : MonoBehaviour
    {
        [Tooltip("Vida máxima.")]
        [SerializeField] float maxHealth = 100f;

        [Tooltip("Invulnerabilidad tras recibir un golpe.")]
        [SerializeField] float invulnerabilityTime = 0.5f;

        [Tooltip("Intervalo del parpadeo durante la invulnerabilidad.")]
        [SerializeField] float blinkInterval = 0.08f;

        [SerializeField] SpriteRenderer[] blinkRenderers;

        // Margen sobre el radio del collider para detectar el contacto.
        const float ContactMargin = 0.05f;

        readonly Collider2D[] contacts = new Collider2D[16];
        ContactFilter2D enemyFilter;
        CircleCollider2D body;
        float invulnerableUntil;

        public float MaxHealth => maxHealth;
        public float Current { get; private set; }
        public bool IsDead { get; private set; }

        // Truco de depuración (F6)
        public bool GodMode { get; set; }

        public bool IsInvulnerable => Time.time < invulnerableUntil;

        public event Action<float> Damaged;
        public event Action Died;

        void Awake()
        {
            body = GetComponent<CircleCollider2D>();
            enemyFilter = new ContactFilter2D { useTriggers = false };
            enemyFilter.SetLayerMask(Layers.EnemyMask);
            if (blinkRenderers == null || blinkRenderers.Length == 0)
                blinkRenderers = GetComponentsInChildren<SpriteRenderer>();
            Current = maxHealth;
        }

        void FixedUpdate()
        {
            if (IsDead || IsInvulnerable) return;

            float radius = body.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y) + ContactMargin;
            int count = Physics2D.OverlapCircle(transform.position, radius, enemyFilter, contacts);
            for (int i = 0; i < count; i++)
            {
                if (contacts[i].TryGetComponent(out IContactDamageSource source))
                {
                    TakeDamage(source.ContactDamage);
                    break;  // Un solo golpe por contacto; después, invulnerabilidad.
                }
            }
        }

        void Update()
        {
            bool visible = !IsInvulnerable || Mathf.FloorToInt((invulnerableUntil - Time.time) / blinkInterval) % 2 == 0;
            for (int i = 0; i < blinkRenderers.Length; i++)
                blinkRenderers[i].enabled = visible;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || IsInvulnerable || GodMode || amount <= 0f) return;

            Current = Mathf.Max(0f, Current - amount);
            invulnerableUntil = Time.time + invulnerabilityTime;
            Damaged?.Invoke(amount);

            if (Current <= 0f)
            {
                IsDead = true;
                Died?.Invoke();
            }
        }

        // Invulnerabilidad sin recibir daño (Esquivar). Si ya había una más larga, se conserva.
        public void GrantInvulnerability(float seconds)
        {
            if (seconds <= 0f) return;
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);
        }

        public void ResetHealth()
        {
            Current = maxHealth;
            IsDead = false;
            invulnerableUntil = 0f;
        }
    }
}
