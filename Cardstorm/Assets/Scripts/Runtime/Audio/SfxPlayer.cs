using Cardstorm.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace Cardstorm.Audio
{
    // Reproduce los efectos desde un grupo fijo de fuentes creadas al empezar. No sabe nada del juego:
    // GameAudio y UiSounds deciden qué suena.
    public class SfxPlayer : MonoBehaviour
    {
        public static SfxPlayer Instance { get; private set; }

        [Tooltip("Opcional. Sin mixer, el volumen de efectos se aplica en cada fuente.")]
        [SerializeField] AudioMixer mixer;
        [SerializeField] string volumeParameter = "SfxVolume";
        [Tooltip("Grupo de los sonidos que no traen el suyo.")]
        [SerializeField] AudioMixerGroup sfxGroup;
        [Tooltip("Grupo de los sonidos de los menús.")]
        [SerializeField] AudioMixerGroup uiGroup;

        [SerializeField, Min(1)] int voices = 24;

        [Tooltip("Para probar desde el menú contextual.")]
        [SerializeField] SfxCue testCue;

        class Voice
        {
            public AudioSource Source;
            public int CueId;
            public int Priority;
            public float StartTime;
            public float BaseVolume;
        }

        readonly VoiceLimiter limiter = new VoiceLimiter();
        Voice[] pool;

        void Awake()
        {
            Instance = this;
            pool = new Voice[voices];
            for (int i = 0; i < voices; i++)
            {
                var go = new GameObject($"Voice {i}");
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;
                pool[i] = new Voice { Source = source };
            }
        }

        void OnEnable() => GameSettings.Changed += ApplyVolume;

        void OnDisable() => GameSettings.Changed -= ApplyVolume;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // El mixer no acepta valores en Awake.
        void Start() => ApplyVolume();

        void Update()
        {
            for (int i = 0; i < pool.Length; i++)
            {
                var v = pool[i];
                if (v.CueId != 0 && !v.Source.isPlaying) Free(v);
            }
        }

        public void Play(SfxCue cue, float pitchScale = 1f) => Play(cue, pitchScale, ui: false);

        public void PlayUi(SfxCue cue) => Play(cue, 1f, ui: true);

        [ContextMenu("Probar sonido")]
        void PlayTest() => PlayUi(testCue);

        void Play(SfxCue cue, float pitchScale, bool ui)
        {
            if (cue == null || !cue.HasClips || pool == null) return;
            int id = cue.GetInstanceID();
            if (!limiter.TryStart(id, Time.unscaledTimeAsDouble, cue.minInterval, cue.maxVoices)) return;

            var voice = PickVoice(cue.priority);
            if (voice == null)
            {
                limiter.Release(id);
                return;
            }
            if (voice.CueId != 0)
            {
                voice.Source.Stop();
                Free(voice);
            }

            var clip = cue.clips[Random.Range(0, cue.clips.Length)];
            if (clip == null)
            {
                limiter.Release(id);
                return;
            }

            var source = voice.Source;
            source.clip = clip;
            source.outputAudioMixerGroup = cue.group != null ? cue.group : ui && uiGroup != null ? uiGroup : sfxGroup;
            source.pitch = Random.Range(cue.pitchRange.x, cue.pitchRange.y) * pitchScale;
            source.priority = cue.priority;
            voice.BaseVolume = cue.volume;
            source.volume = VoiceVolume(cue.volume);
            voice.CueId = id;
            voice.Priority = cue.priority;
            voice.StartTime = Time.unscaledTime;
            source.Play();
        }

        // Una fuente libre; si no hay, la menos importante que lleve más tiempo sonando, siempre que no lo sea
        // más que la nueva.
        Voice PickVoice(int priority)
        {
            Voice steal = null;
            for (int i = 0; i < pool.Length; i++)
            {
                var v = pool[i];
                if (v.CueId == 0) return v;
                if (v.Priority < priority) continue;
                if (steal == null || v.Priority > steal.Priority ||
                    (v.Priority == steal.Priority && v.StartTime < steal.StartTime))
                    steal = v;
            }
            return steal;
        }

        void Free(Voice v)
        {
            limiter.Release(v.CueId);
            v.CueId = 0;
            v.Source.clip = null;
        }

        float VoiceVolume(float baseVolume) => mixer != null ? baseVolume : baseVolume * GameSettings.Current.SfxVolume;

        void ApplyVolume()
        {
            if (mixer != null)
            {
                mixer.SetFloat(volumeParameter, AudioMath.LinearToDb(GameSettings.Current.SfxVolume));
                return;
            }
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++)
                if (pool[i].CueId != 0) pool[i].Source.volume = VoiceVolume(pool[i].BaseVolume);
        }
    }
}
