using Cardstorm.Cards;
using Cardstorm.Player;
using UnityEngine;

namespace Cardstorm.Progression
{
    // Crea las estadísticas del personaje y copia sus valores a los componentes del jugador cuando cambian.
    // El radio de recogida y los multiplicadores de experiencia y oro se leen directamente de Stats.
    [RequireComponent(typeof(CardCaster), typeof(PlayerCards), typeof(PlayerHealth))]
    public class PlayerStatsBinder : MonoBehaviour
    {
        [SerializeField] ProgressionConfig config;

        CardCaster caster;
        PlayerCards cards;
        PlayerHealth health;
        PlayerMovement movement;

        public PlayerStats Stats { get; private set; }

        void Awake()
        {
            caster = GetComponent<CardCaster>();
            cards = GetComponent<PlayerCards>();
            health = GetComponent<PlayerHealth>();
            movement = GetComponent<PlayerMovement>();

            if (config == null) Debug.LogError("PlayerStatsBinder no tiene ProgressionConfig.", this);
            Stats = new PlayerStats(config != null ? config.rules : new ProgressionRules());
        }

        // En Start porque PlayerCards crea la energía en su Awake.
        void Start()
        {
            WarnIfBaseDiffers(StatType.MaxHealth, health.BaseMaxHealth);
            WarnIfBaseDiffers(StatType.MaxEnergy, cards.Energy.Max - cards.Energy.MaxBonus);
            WarnIfBaseDiffers(StatType.EnergyRegen, cards.Energy.RegenPerSecond / cards.Energy.RegenMultiplier);
            if (movement != null) WarnIfBaseDiffers(StatType.MoveSpeed, movement.Speed / movement.SpeedMultiplier);

            Stats.Changed += OnStatChanged;
            ApplyAll();
        }

        void OnDestroy()
        {
            if (Stats != null) Stats.Changed -= OnStatChanged;
        }

        void OnStatChanged(StatType stat) => ApplyAll();

        void ApplyAll()
        {
            caster.DamageMultiplier = Stats.Multiplier(StatType.Damage);
            cards.Energy.MaxBonus = Stats.Bonus(StatType.MaxEnergy);
            cards.Energy.RegenMultiplier = Stats.Multiplier(StatType.EnergyRegen);
            health.MaxHealthBonus = Stats.Bonus(StatType.MaxHealth);
            health.RegenPerSecond = Stats.Value(StatType.HealthRegen);
            if (movement != null) movement.SpeedMultiplier = Stats.Multiplier(StatType.MoveSpeed);
        }

        // Los componentes guardan su propia base; si no coincide con la de la configuración, los topes no cuadran.
        void WarnIfBaseDiffers(StatType stat, float componentBase)
        {
            if (Mathf.Abs(Stats.Base(stat) - componentBase) > 1e-3f)
                Debug.LogWarning($"PlayerStatsBinder: la base de {stat} es {Stats.Base(stat)} en la configuración y {componentBase} en el jugador.", this);
        }
    }
}
