using UnityEngine;
using UnityEngine.Audio;

namespace Cardstorm.Audio
{
    // Un efecto de sonido: varios clips para que no suene siempre igual, y los límites para que no se amontone.
    [CreateAssetMenu(menuName = "Cardstorm/Sonido", fileName = "Sfx_")]
    public class SfxCue : ScriptableObject
    {
        [Tooltip("Se elige uno al azar en cada disparo.")]
        public AudioClip[] clips;

        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("Tono al azar entre estos dos valores.")]
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

        [Tooltip("Segundos mínimos entre dos disparos de este sonido.")]
        [Min(0f)] public float minInterval;

        [Tooltip("Copias de este sonido a la vez. 0 = sin límite.")]
        [Min(0)] public int maxVoices;

        [Tooltip("0 es lo más importante y 256 lo menos. Sin fuentes libres, se quita la de menos importancia.")]
        [Range(0, 256)] public int priority = 128;

        [Tooltip("Opcional. Si se deja vacío, el grupo de efectos del reproductor.")]
        public AudioMixerGroup group;

        public bool HasClips => clips != null && clips.Length > 0;
    }
}
