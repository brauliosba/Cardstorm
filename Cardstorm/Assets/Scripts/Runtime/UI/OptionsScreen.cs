using System;
using Cardstorm.Audio;
using Cardstorm.Core;
using Cardstorm.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Opciones, las mismas en el menú y en la pausa. Cada ajuste se nota en el momento y se guarda al volver.
    public class OptionsScreen : ModalScreen
    {
        // Los deslizadores van de 0 a 20 en números enteros: pasos de 0,05.
        const int VolumeSteps = 20;

        static readonly ShakeLevel[] ShakeOrder = { ShakeLevel.Normal, ShakeLevel.Reduced, ShakeLevel.Off };

        [SerializeField] Button backButton;

        [Header("Opcionales")]
        [SerializeField] Slider musicSlider;
        [SerializeField] TMP_Text musicValue;
        [SerializeField] Slider sfxSlider;
        [SerializeField] TMP_Text sfxValue;
        [SerializeField] OptionStepper shakeStepper;
        [SerializeField] Toggle flashesToggle;
        [SerializeField] Button controlsButton;
        [SerializeField] RebindScreen controls;
        [Tooltip("Suena al dejar de mover el volumen de efectos, para oír cómo queda.")]
        [SerializeField] SfxCue previewCue;
        [SerializeField, Min(0f)] float previewDelay = 0.25f;

        Action closed;
        bool showing;
        float previewAt = -1f;

        protected override void Awake()
        {
            base.Awake();
            if (controls == null) controls = GetComponentInChildren<RebindScreen>(true);
            if (backButton != null) backButton.onClick.AddListener(Back);
            if (controlsButton != null) controlsButton.onClick.AddListener(OpenControls);
            SetupSlider(musicSlider, OnMusicChanged);
            SetupSlider(sfxSlider, OnSfxChanged);
            if (shakeStepper != null) shakeStepper.Changed += OnShakeChanged;
            if (flashesToggle != null) flashesToggle.onValueChanged.AddListener(OnFlashesChanged);
            L10n.Changed += ShowTexts;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            L10n.Changed -= ShowTexts;
            if (backButton != null) backButton.onClick.RemoveListener(Back);
            if (controlsButton != null) controlsButton.onClick.RemoveListener(OpenControls);
            if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
            if (shakeStepper != null) shakeStepper.Changed -= OnShakeChanged;
            if (flashesToggle != null) flashesToggle.onValueChanged.RemoveListener(OnFlashesChanged);
        }

        static void SetupSlider(Slider slider, UnityEngine.Events.UnityAction<float> listener)
        {
            if (slider == null) return;
            slider.wholeNumbers = true;
            slider.minValue = 0;
            slider.maxValue = VolumeSteps;
            slider.onValueChanged.AddListener(listener);
        }

        // onClosed se llama al volver, con los ajustes ya guardados.
        public void Show(Action onClosed)
        {
            closed = onClosed;
            ShowValues();
            Open(lockInput: false);
        }

        public override void Hide()
        {
            if (controls != null && controls.IsVisible) controls.Hide();
            base.Hide();
        }

        protected override void Update()
        {
            base.Update();
            if (previewAt >= 0f && Time.unscaledTime >= previewAt)
            {
                previewAt = -1f;
                var player = SfxPlayer.Instance;
                if (player != null) player.PlayUi(previewCue);
            }
            if (CancelPressed()) Back();
        }

        protected override Selectable DefaultSelection()
        {
            if (musicSlider != null && IsSelectable(musicSlider)) return musicSlider;
            return backButton;
        }

        void Back()
        {
            if (!AcceptsInput) return;
            SettingsStore.Save();
            Hide();
            var callback = closed;
            closed = null;
            callback?.Invoke();
        }

        void OpenControls()
        {
            if (!AcceptsInput || controls == null) return;
            SetSuspended(true);
            controls.Show(() => SetSuspended(false));
        }

        // Pone los controles en los valores guardados sin lanzar sus eventos.
        void ShowValues()
        {
            var s = GameSettings.Current;
            showing = true;
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(Mathf.Round(s.MusicVolume * VolumeSteps));
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(Mathf.Round(s.SfxVolume * VolumeSteps));
            if (flashesToggle != null) flashesToggle.SetIsOnWithoutNotify(s.ReduceFlashes);
            showing = false;
            ShowTexts();
        }

        void ShowTexts()
        {
            var s = GameSettings.Current;
            if (musicValue != null) musicValue.text = Percent(s.MusicVolume);
            if (sfxValue != null) sfxValue.text = Percent(s.SfxVolume);
            if (shakeStepper != null)
            {
                shakeStepper.SetOptions(new[]
                {
                    L10n.Get(L10nKeys.OptionsShakeNormal),
                    L10n.Get(L10nKeys.OptionsShakeReduced),
                    L10n.Get(L10nKeys.OptionsShakeOff),
                }, Array.IndexOf(ShakeOrder, s.Shake));
            }
        }

        static string Percent(float v) => L10n.Format(L10nKeys.StatPercent, Mathf.RoundToInt(v * 100f));

        void OnMusicChanged(float value)
        {
            if (showing) return;
            GameSettings.Current.MusicVolume = value / VolumeSteps;
            if (musicValue != null) musicValue.text = Percent(GameSettings.Current.MusicVolume);
            SettingsStore.ApplyCurrent();
        }

        void OnSfxChanged(float value)
        {
            if (showing) return;
            GameSettings.Current.SfxVolume = value / VolumeSteps;
            if (sfxValue != null) sfxValue.text = Percent(GameSettings.Current.SfxVolume);
            SettingsStore.ApplyCurrent();
            if (previewCue != null) previewAt = Time.unscaledTime + previewDelay;
        }

        void OnShakeChanged(int index)
        {
            if (index < 0 || index >= ShakeOrder.Length) return;
            GameSettings.Current.Shake = ShakeOrder[index];
            SettingsStore.ApplyCurrent();
        }

        void OnFlashesChanged(bool value)
        {
            if (showing) return;
            GameSettings.Current.ReduceFlashes = value;
            SettingsStore.ApplyCurrent();
        }
    }
}
