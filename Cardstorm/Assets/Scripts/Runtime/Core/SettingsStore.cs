using System;
using Cardstorm.CameraRig;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardstorm.Core
{
    // Ajustes en PlayerPrefs.
    public static class SettingsStore
    {
        const string MusicKey = "settings.music";
        const string SfxKey = "settings.sfx";
        const string ShakeKey = "settings.shake";
        const string FlashesKey = "settings.flashes";
        const string BindingsKey = "settings.bindings";

        // Al reasignar o restablecer controles, para que las etiquetas de botones se reescriban.
        public static event Action BindingsChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => BindingsChanged = null;

        public static void NotifyBindingsChanged() => BindingsChanged?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            var s = new GameSettings();
            s.MusicVolume = PlayerPrefs.GetFloat(MusicKey, s.MusicVolume);
            s.SfxVolume = PlayerPrefs.GetFloat(SfxKey, s.SfxVolume);
            s.Shake = (ShakeLevel)PlayerPrefs.GetInt(ShakeKey, (int)s.Shake);
            s.ReduceFlashes = PlayerPrefs.GetInt(FlashesKey, 0) != 0;
            s.BindingOverrides = PlayerPrefs.GetString(BindingsKey, "");
            s.Clamp();

            LoadBindings(s);
            GameSettings.Current = s;
            Apply(s);
        }

        // Escribe los ajustes actuales, con los controles tal como están ahora, y avisa.
        public static void Save()
        {
            var s = GameSettings.Current;
            var actions = InputSystem.actions;
            if (actions != null) s.BindingOverrides = actions.SaveBindingOverridesAsJson();
            s.Clamp();

            PlayerPrefs.SetFloat(MusicKey, s.MusicVolume);
            PlayerPrefs.SetFloat(SfxKey, s.SfxVolume);
            PlayerPrefs.SetInt(ShakeKey, (int)s.Shake);
            PlayerPrefs.SetInt(FlashesKey, s.ReduceFlashes ? 1 : 0);
            PlayerPrefs.SetString(BindingsKey, s.BindingOverrides);
            PlayerPrefs.Save();

            Apply(s);
            GameSettings.NotifyChanged();
        }

        // Aplica los ajustes actuales sin guardarlos, para que se noten mientras se cambian.
        public static void ApplyCurrent()
        {
            Apply(GameSettings.Current);
            GameSettings.NotifyChanged();
        }

        static void Apply(GameSettings s)
        {
            CameraShake.ShakeScale = s.ShakeScale;
        }

        // Si el JSON no se puede leer, se descarta y quedan los controles de serie.
        static void LoadBindings(GameSettings s)
        {
            var actions = InputSystem.actions;
            if (actions == null || string.IsNullOrEmpty(s.BindingOverrides)) return;

            try
            {
                actions.LoadBindingOverridesFromJson(s.BindingOverrides);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SettingsStore] Controles guardados no válidos, se descartan: {e.Message}");
                actions.RemoveAllBindingOverrides();
                s.BindingOverrides = "";
                PlayerPrefs.DeleteKey(BindingsKey);
            }
        }
    }
}
