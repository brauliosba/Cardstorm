using System.Collections;
using Cardstorm.Progression;
using UnityEngine;

namespace Cardstorm.UI
{
    // Pantalla de derrota: se abre poco después de morir el jugador.
    public class DefeatScreen : RunEndScreen
    {
        [SerializeField] RunManager run;

        [Tooltip("Segundos entre la muerte y la pantalla, en tiempo real.")]
        [SerializeField, Min(0f)] float showDelay = 0.8f;

        protected override void Awake()
        {
            base.Awake();
            if (run == null) run = FindFirstObjectByType<RunManager>();
        }

        void OnEnable()
        {
            if (health != null) health.Died += OnDied;
        }

        void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
        }

        void OnDied() => StartCoroutine(ShowAfterDelay());

        IEnumerator ShowAfterDelay()
        {
            // El reloj se para ya, para que el tiempo mostrado sea el de la muerte.
            if (clock != null) clock.Paused = true;
            double time = clock != null ? clock.Elapsed : Time.timeSinceLevelLoad;
            int level = run != null && run.State != null ? run.State.Level : 1;
            if (showDelay > 0f) yield return new WaitForSecondsRealtime(showDelay);
            Show(time, level);
        }
    }
}
