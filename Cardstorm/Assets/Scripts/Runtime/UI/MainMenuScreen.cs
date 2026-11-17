using Cardstorm.Core;
using Cardstorm.Localization;
using Cardstorm.Telemetry;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Menú principal.
    public class MainMenuScreen : ModalScreen
    {
        [SerializeField] Button playButton;
        [SerializeField] Button quitButton;

        [Header("Opcionales")]
        [Tooltip("Se ve pero no se puede pulsar.")]
        [SerializeField] Button unlocksButton;
        [SerializeField] TMP_Text comingSoonText;
        [SerializeField] Button optionsButton;
        [Tooltip("Código de jugador, solo para leerlo.")]
        [SerializeField] TMP_Text codeText;
        [SerializeField] TMP_Text versionText;

        protected override void Awake()
        {
            base.Awake();
            if (playButton != null) playButton.onClick.AddListener(Play);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            if (unlocksButton != null) unlocksButton.interactable = false;
            L10n.Changed += ShowTexts;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            L10n.Changed -= ShowTexts;
            if (playButton != null) playButton.onClick.RemoveListener(Play);
            if (quitButton != null) quitButton.onClick.RemoveListener(Quit);
        }

        void Start()
        {
            ShowTexts();
            Open(lockInput: false);
        }

        protected override Selectable DefaultSelection() => playButton;

        void ShowTexts()
        {
            if (comingSoonText != null) comingSoonText.text = L10n.Get(L10nKeys.MenuComingSoon);
            if (versionText != null) versionText.text = L10n.Format(L10nKeys.MenuVersion, Application.version);
            TelemetryUi.ShowCode(codeText, PlayerCodeStore.GetOrCreate());
        }

        void Play()
        {
            if (AcceptsInput) SceneFlow.LoadGameplay();
        }

        void Quit()
        {
            if (AcceptsInput) SceneFlow.Quit();
        }
    }
}
