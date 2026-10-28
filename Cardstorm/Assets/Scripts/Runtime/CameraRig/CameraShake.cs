using Cardstorm.Player;
using Unity.Cinemachine;
using UnityEngine;

namespace Cardstorm.CameraRig
{
    // Sacudida de cámara con impulsos de Cinemachine. ShakeScale = 0 la desactiva.
    [RequireComponent(typeof(CinemachineImpulseSource))]
    public class CameraShake : MonoBehaviour
    {
        public static float ShakeScale = 1f;

        [SerializeField] PlayerHealth player;

        [Tooltip("Sacudida al recibir daño.")]
        [SerializeField] float damageShakeIntensity = 0.15f;
        [SerializeField] float damageShakeDuration = 0.15f;

        CinemachineImpulseSource source;

        void Awake()
        {
            source = GetComponent<CinemachineImpulseSource>();
        }

        void OnEnable()
        {
            if (player != null) player.Damaged += OnPlayerDamaged;
        }

        void OnDisable()
        {
            if (player != null) player.Damaged -= OnPlayerDamaged;
        }

        public void AddShake(float intensity, float duration)
        {
            float force = intensity * ShakeScale;
            if (force <= 0f) return;

            source.ImpulseDefinition.ImpulseDuration = duration;
            source.GenerateImpulseWithVelocity(Random.insideUnitCircle.normalized * force);
        }

        void OnPlayerDamaged(float _) => AddShake(damageShakeIntensity, damageShakeDuration);
    }
}
