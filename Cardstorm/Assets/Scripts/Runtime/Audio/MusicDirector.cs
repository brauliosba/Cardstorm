using System.Collections.Generic;
using Cardstorm.Core;
using Cardstorm.Progression;
using Cardstorm.Spawning;
using UnityEngine;
using UnityEngine.Audio;

namespace Cardstorm.Audio
{
    // Elige la música: una pista por intensidad del tramo y la de jefe mientras haya uno vivo. Cambia con un
    // fundido cruzado en tiempo real, y solo al empezar un tramo de otra intensidad.
    public class MusicDirector : MonoBehaviour
    {
        [Tooltip("Vacío en el menú: entonces suena solo la pista del menú.")]
        [SerializeField] SpawnScheduler scheduler;

        [Header("Mixer (opcional)")]
        [SerializeField] AudioMixer mixer;
        [SerializeField] string volumeParameter = "MusicVolume";
        [SerializeField] AudioMixerGroup musicGroup;
        [Tooltip("Se activa mientras la partida está congelada (pausa, subida de nivel, fin).")]
        [SerializeField] AudioMixerSnapshot pausedSnapshot;
        [SerializeField] AudioMixerSnapshot normalSnapshot;
        [SerializeField, Min(0f)] float snapshotTime = 0.3f;

        [Header("Pistas")]
        [SerializeField] AudioClip menuTrack;
        [SerializeField] AudioClip lowTrack;
        [SerializeField] AudioClip midTrack;
        [SerializeField] AudioClip highTrack;
        [SerializeField] AudioClip bossTrack;
        [Tooltip("Opcional. Sin ella, el jefe final usa la pista de jefe.")]
        [SerializeField] AudioClip finalBossTrack;
        [SerializeField] string finalBossId = "ojo_tormenta";

        [SerializeField, Range(0f, 1f)] float volume = 1f;
        [Tooltip("Segundos del fundido cruzado, en tiempo real.")]
        [SerializeField, Min(0.01f)] float crossfade = 2f;

        readonly List<string> bosses = new List<string>();
        AudioSource[] sources;
        int activeSource;
        float fade = 1f;
        int segmentIndex = -1;
        MusicLevel level = MusicLevel.Low;
        bool paused;

        void Awake()
        {
            sources = new AudioSource[2];
            for (int i = 0; i < sources.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;
                source.priority = 0;
                source.volume = 0f;
                source.outputAudioMixerGroup = musicGroup;
                sources[i] = source;
            }
        }

        void OnEnable()
        {
            GameSettings.Changed += ApplyVolume;
            RunEvents.BossSpawned += OnBossSpawned;
            RunEvents.BossKilled += OnBossGone;
            RunEvents.BossRetreated += OnBossGone;
        }

        void OnDisable()
        {
            GameSettings.Changed -= ApplyVolume;
            RunEvents.BossSpawned -= OnBossSpawned;
            RunEvents.BossKilled -= OnBossGone;
            RunEvents.BossRetreated -= OnBossGone;
        }

        void Start()
        {
            ApplyVolume();
            if (normalSnapshot != null) normalSnapshot.TransitionTo(0f);
            ReadSegment();
            SwitchTo(Desired());
        }

        void Update()
        {
            ReadSegment();
            SwitchTo(Desired());
            UpdatePaused();

            if (fade < 1f)
            {
                fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / crossfade);
                ApplySourceVolumes();
                var old = sources[1 - activeSource];
                if (fade >= 1f && old.isPlaying)
                {
                    old.Stop();
                    old.clip = null;
                }
            }
        }

        // La intensidad solo se lee al empezar un tramo, para no cambiar de pista en mitad de una oleada.
        void ReadSegment()
        {
            if (scheduler == null || scheduler.CurrentSegmentIndex == segmentIndex) return;
            segmentIndex = scheduler.CurrentSegmentIndex;
            var segment = scheduler.CurrentSegment;
            level = MusicIntensity.ForSegment(segment != null ? segment.name : null);
        }

        AudioClip Desired()
        {
            if (scheduler == null) return menuTrack;
            if (bosses.Count > 0)
            {
                if (finalBossTrack != null && bosses.Contains(finalBossId)) return finalBossTrack;
                if (bossTrack != null) return bossTrack;
            }
            var track = level switch
            {
                MusicLevel.Low => lowTrack,
                MusicLevel.High => highTrack,
                _ => midTrack,
            };
            return track != null ? track : midTrack != null ? midTrack : lowTrack;
        }

        void SwitchTo(AudioClip clip)
        {
            if (sources[activeSource].clip == clip) return;

            // Si se cambia en mitad de un fundido, la pista que se iba se corta y la que entraba pasa a irse.
            var old = sources[1 - activeSource];
            old.Stop();
            old.clip = null;
            activeSource = 1 - activeSource;

            var next = sources[activeSource];
            next.clip = clip;
            next.volume = 0f;
            if (clip != null) next.Play();
            fade = 0f;
            ApplySourceVolumes();
        }

        void ApplySourceVolumes()
        {
            float scale = volume * (mixer != null ? 1f : GameSettings.Current.MusicVolume);
            sources[activeSource].volume = fade * scale;
            sources[1 - activeSource].volume = (1f - fade) * scale;
        }

        void ApplyVolume()
        {
            if (mixer != null) mixer.SetFloat(volumeParameter, AudioMath.LinearToDb(GameSettings.Current.MusicVolume));
            if (sources != null) ApplySourceVolumes();
        }

        // La partida está congelada cuando el tiempo está parado: pausa, subida de nivel, tienda y fin.
        void UpdatePaused()
        {
            if (pausedSnapshot == null || normalSnapshot == null || scheduler == null) return;
            bool now = Time.timeScale == 0f;
            if (now == paused) return;
            paused = now;
            (paused ? pausedSnapshot : normalSnapshot).TransitionTo(snapshotTime);
        }

        void OnBossSpawned(double time, string bossId) => bosses.Add(bossId ?? string.Empty);

        void OnBossGone(double time, string bossId) => bosses.Remove(bossId ?? string.Empty);
    }
}
